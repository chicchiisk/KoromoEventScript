using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.Commands.Build;
using KoromoEventScript.Cli.Compilation;
using KoromoEventScript.Cli.Diagnostics;
using KoromoEventScript.Cli.Localization;
using KoromoEventScript.Cli.Semantics;

namespace KoromoEventScript.Cli.Build;

public sealed record BuildPipelineRequest(
    BuildCommandOptions Options,
    string CurrentDirectory,
    bool ValidateOnly);

public sealed record BuildPipelineResult(
    CliExitCode ExitCode,
    IReadOnlyList<Diagnostic> Diagnostics,
    BuildManifestDocument? Manifest = null);

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
        foreach (var document in documents)
        {
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
                    : Path.GetRelativePath(Path.GetDirectoryName(artifactPaths.ManifestPath)!, artifactPaths.KlibTextPath).Replace('\\', '/')));
        }

        var manifestDocument = BuildManifest(activePreparation.EntryPath!, request.Options, artifactRecords);
        var outputPaths = outputPlanner.Resolve(config, request.Options, documents[0].ProjectRelativePath);
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

        return new BuildPipelineResult(CliExitCode.Success, activePreparation.Diagnostics, manifestDocument);
    }

    private static BuildManifestDocument BuildManifest(
        string entryEventListPath,
        BuildCommandOptions options,
        IReadOnlyList<BuildManifestScriptArtifact> artifacts)
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

        return new BuildManifestDocument(
            typeof(BuildPipelineService).Assembly.GetName().Version?.ToString() ?? "0.0.0",
            options.Target,
            entryEventListPath,
            inputs,
            string.IsNullOrWhiteSpace(options.Locale) ? artifacts : [],
            localizations);
    }
}
