using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.Commands.Build;
using KoromoEventScript.Cli.Compilation;
using KoromoEventScript.Cli.Diagnostics;
using KoromoEventScript.Cli.Localization;
using KoromoEventScript.Cli.Parsing;
using KoromoEventScript.Cli.ProjectSystem;
using KoromoEventScript.Cli.Semantics;

namespace KoromoEventScript.Cli.Build;

public sealed record BuildPipelineRequest(
    BuildCommandOptions Options,
    string CurrentDirectory,
    bool ValidateOnly);

public sealed record BuildPipelineResult(
    CliExitCode ExitCode,
    IReadOnlyList<Diagnostic> Diagnostics,
    BuildManifestDocument? Manifest = null,
    string? ManifestPath = null);

public sealed class BuildPipelineService
{
    private readonly BuildPreparationService preparationService;
    private readonly TagAssignmentPlanner tagAssignmentPlanner;
    private readonly ScriptRewriteService scriptRewriteService;
    private readonly BuildLocalizationService localizationService;
    private readonly BuildOutputPlanner outputPlanner;
    private readonly KlibCompiler compiler;
    private readonly KlibArtifactWriter artifactWriter;
    private readonly BuildDiagnosticsWriter diagnosticsWriter;
    private readonly BuildManifestWriter manifestWriter;

    public BuildPipelineService()
        : this(
            new BuildPreparationService(),
            new TagAssignmentPlanner(),
            new ScriptRewriteService(),
            new BuildLocalizationService(),
            new BuildOutputPlanner(),
            new KlibCompiler(),
            new KlibArtifactWriter(),
            new BuildDiagnosticsWriter(),
            new BuildManifestWriter())
    {
    }

    public BuildPipelineService(
        BuildPreparationService preparationService,
        TagAssignmentPlanner tagAssignmentPlanner,
        ScriptRewriteService scriptRewriteService,
        BuildLocalizationService localizationService,
        BuildOutputPlanner outputPlanner,
        KlibCompiler compiler,
        KlibArtifactWriter artifactWriter,
        BuildDiagnosticsWriter diagnosticsWriter,
        BuildManifestWriter manifestWriter)
    {
        this.preparationService = preparationService;
        this.tagAssignmentPlanner = tagAssignmentPlanner;
        this.scriptRewriteService = scriptRewriteService;
        this.localizationService = localizationService;
        this.outputPlanner = outputPlanner;
        this.compiler = compiler;
        this.artifactWriter = artifactWriter;
        this.diagnosticsWriter = diagnosticsWriter;
        this.manifestWriter = manifestWriter;
    }

    public BuildPipelineResult Run(BuildPipelineRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Options);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CurrentDirectory);

        var preparation = preparationService.Prepare(request.Options, request.CurrentDirectory);
        if (!preparation.Succeeded)
        {
            return new BuildPipelineResult(preparation.ExitCode, preparation.Diagnostics);
        }

        if (request.ValidateOnly)
        {
            return new BuildPipelineResult(preparation.ExitCode, preparation.Diagnostics);
        }

        var activePreparation = preparation;
        if (!request.ValidateOnly)
        {
            var plan = tagAssignmentPlanner.BuildPlan(preparation.Config!, preparation.SemanticResult!.ImportGraph?.OrderedDocuments ?? []);
            if (plan.HasChanges)
            {
                var rewriteResult = scriptRewriteService.Apply(preparation.Config!, plan);
                if (!rewriteResult.Succeeded)
                {
                    return new BuildPipelineResult(rewriteResult.ExitCode, rewriteResult.Diagnostics);
                }

                activePreparation = preparationService.Prepare(request.Options, request.CurrentDirectory);
                if (!activePreparation.Succeeded)
                {
                    return new BuildPipelineResult(activePreparation.ExitCode, activePreparation.Diagnostics);
                }
            }
        }

        var config = activePreparation.Config!;
        var semanticResult = activePreparation.SemanticResult!;
        IReadOnlyList<ScriptDocument> documents = semanticResult.ImportGraph?.OrderedDocuments ?? [];
        if (!string.IsNullOrWhiteSpace(request.Options.Locale))
        {
            var localization = localizationService.Resolve(config, documents, request.Options.Locale);
            if (!localization.Succeeded)
            {
                return new BuildPipelineResult(localization.ExitCode, localization.Diagnostics);
            }

            documents = localization.Documents;
        }

        var artifactRecords = new List<BuildManifestScriptArtifact>();
        for (var index = 0; index < documents.Count; index++)
        {
            var document = documents[index];
            var compilation = compiler.Compile(
                config,
                semanticResult,
                document,
                embedLocalizedText: true);
            if (!compilation.Succeeded)
            {
                return new BuildPipelineResult(CliExitCode.CompileError, compilation.Diagnostics);
            }

            var artifactPaths = outputPlanner.Resolve(config, request.Options, document.ProjectRelativePath);
            try
            {
                artifactWriter.WriteBinary(artifactPaths.KlibPath, compilation.Document!);
                if (artifactPaths.KlibTextPath is not null)
                {
                    artifactWriter.WriteText(artifactPaths.KlibTextPath, compilation.Document!);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return new BuildPipelineResult(
                    CliExitCode.FileOrDirectoryError,
                    [new Diagnostic(DiagnosticLevel.Error, "KES9004", document.ProjectRelativePath, 1, 1, $"Could not write build artifact: {exception.Message}")]);
            }

            artifactRecords.Add(new BuildManifestScriptArtifact(
                document.ProjectRelativePath,
                Path.GetRelativePath(Path.GetDirectoryName(artifactPaths.ManifestPath)!, artifactPaths.KlibPath).Replace('\\', '/'),
                artifactPaths.KlibTextPath is null
                    ? null
                    : Path.GetRelativePath(Path.GetDirectoryName(artifactPaths.ManifestPath)!, artifactPaths.KlibTextPath).Replace('\\', '/'),
                compilation.Document!.Module.ScriptId,
                string.IsNullOrWhiteSpace(request.Options.Locale) ? "ja-JP" : request.Options.Locale!,
                index == 0,
                compilation.Document.Module.EntryLabel));
        }

        var outputPaths = outputPlanner.Resolve(config, request.Options, documents[0].ProjectRelativePath);
        var events = BuildEventEntries(config, activePreparation.EntryPath!, artifactRecords);
        var manifestDocument = BuildManifest(config, activePreparation.EntryPath!, request.Options, artifactRecords, events, outputPaths.ManifestPath);
        var diagnosticsResult = diagnosticsWriter.Write(outputPaths.DiagnosticsPath, activePreparation.Diagnostics);
        if (!diagnosticsResult.Succeeded)
        {
            return new BuildPipelineResult(diagnosticsResult.ExitCode, diagnosticsResult.Diagnostics);
        }

        var manifestPath = outputPaths.ManifestPath;
        var manifestResult = manifestWriter.Write(manifestPath, manifestDocument);
        if (!manifestResult.Succeeded)
        {
            return new BuildPipelineResult(manifestResult.ExitCode, manifestResult.Diagnostics);
        }

        return new BuildPipelineResult(CliExitCode.Success, activePreparation.Diagnostics, manifestDocument, manifestPath);
    }

    private static BuildManifestDocument BuildManifest(
        ProjectConfig config,
        string entryEventListPath,
        BuildCommandOptions options,
        IReadOnlyList<BuildManifestScriptArtifact> artifacts,
        IReadOnlyList<BuildManifestEventEntry> events,
        string manifestPath)
    {
        var inputs = new List<BuildManifestInputFile>
        {
            new(entryEventListPath, "kel"),
        };

        inputs.AddRange(artifacts
            .Select(static artifact => artifact.SourcePath)
            .Distinct(StringComparer.Ordinal)
            .Select(static path => new BuildManifestInputFile(path, "kc")));

        var localizations = string.IsNullOrWhiteSpace(options.Locale)
            ? Array.Empty<BuildManifestLocalizationArtifact>()
            : [new BuildManifestLocalizationArtifact(options.Locale!, artifacts)];

        var cliVersion = CliVersionInfo.Current;

        return new BuildManifestDocument(
            cliVersion,
            options.Target,
            NormalizeIdentifier(config.ProjectName),
            config.ProjectName,
            string.IsNullOrWhiteSpace(options.Locale) ? "ja-JP" : options.Locale!,
            entryEventListPath,
            inputs,
            artifacts,
            events,
            BuildAssetArtifacts(config, manifestPath),
            new BuildManifestRuntimeDefaults(config.RuntimeWindowWidth, config.RuntimeWindowHeight, false),
            new BuildManifestBuildInfo($"{options.Target}-{NormalizeIdentifier(config.ProjectName)}-{artifacts.Count}", cliVersion),
            localizations);
    }

    private static IReadOnlyList<BuildManifestEventEntry> BuildEventEntries(
        ProjectConfig config,
        string entryEventListPath,
        IReadOnlyList<BuildManifestScriptArtifact> artifacts)
    {
        var kelPath = Path.GetFullPath(Path.Combine(config.ProjectRoot, entryEventListPath));
        var source = File.ReadAllText(kelPath);
        var syntax = KelParser.Parse(source);
        return new KelEventManifestBuilder().BuildEvents(syntax, artifacts);
    }

    private static IReadOnlyList<BuildManifestAssetArtifact> BuildAssetArtifacts(ProjectConfig config, string manifestPath)
    {
        var assetsRoot = Path.GetFullPath(Path.Combine(config.ProjectRoot, config.AssetsPath));
        if (!Directory.Exists(assetsRoot))
        {
            return [];
        }

        var manifestDirectory = Path.GetDirectoryName(manifestPath)!;
        return Directory.EnumerateFiles(assetsRoot, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(path =>
            {
                var projectRelativePath = Path.GetRelativePath(config.ProjectRoot, path).Replace('\\', '/');
                var manifestRelativePath = Path.GetRelativePath(manifestDirectory, path).Replace('\\', '/');
                return new BuildManifestAssetArtifact(
                    Path.ChangeExtension(projectRelativePath, null)!.Replace('/', '.'),
                    ResolveAssetKind(projectRelativePath),
                    manifestRelativePath,
                    null);
            })
            .ToArray();
    }

    private static string ResolveAssetKind(string projectRelativePath)
    {
        var normalized = projectRelativePath.Replace('\\', '/');
        if (normalized.StartsWith("assets/bg/", StringComparison.OrdinalIgnoreCase))
        {
            return "background";
        }

        if (normalized.StartsWith("assets/bgm/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("assets/audio/bgm/", StringComparison.OrdinalIgnoreCase))
        {
            return "bgm";
        }

        if (normalized.StartsWith("assets/se/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("assets/audio/se/", StringComparison.OrdinalIgnoreCase))
        {
            return "se";
        }

        if (normalized.StartsWith("assets/voice/", StringComparison.OrdinalIgnoreCase))
        {
            return "voice";
        }

        if (normalized.StartsWith("assets/actor/", StringComparison.OrdinalIgnoreCase))
        {
            return "actor";
        }

        return "asset";
    }

    private static string NormalizeIdentifier(string value)
    {
        return string.Concat(value.Select(static character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-')).Trim('-');
    }
}
