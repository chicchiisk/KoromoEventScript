using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.Commands.Build;
using KoromoEventScript.Cli.Diagnostics;
using KoromoEventScript.Cli.ProjectSystem;
using KoromoEventScript.Cli.Semantics;

namespace KoromoEventScript.Cli.Build;

public sealed class ScriptPreparationService
{
    private readonly ProjectRootResolver projectRootResolver;
    private readonly ProjectConfigLoader projectConfigLoader;
    private readonly SourceFileParser sourceFileParser;
    private readonly KelScriptReferenceResolver scriptReferenceResolver;
    private readonly SemanticAnalyzer semanticAnalyzer;

    public ScriptPreparationService()
        : this(
            new ProjectRootResolver(),
            new ProjectConfigLoader(),
            new SourceFileParser(),
            new KelScriptReferenceResolver(),
            new SemanticAnalyzer())
    {
    }

    public ScriptPreparationService(
        ProjectRootResolver projectRootResolver,
        ProjectConfigLoader projectConfigLoader,
        SourceFileParser sourceFileParser,
        KelScriptReferenceResolver scriptReferenceResolver,
        SemanticAnalyzer semanticAnalyzer)
    {
        this.projectRootResolver = projectRootResolver;
        this.projectConfigLoader = projectConfigLoader;
        this.sourceFileParser = sourceFileParser;
        this.scriptReferenceResolver = scriptReferenceResolver;
        this.semanticAnalyzer = semanticAnalyzer;
    }

    public ScriptPreparationResult Prepare(ScriptPreparationRequest request, string currentDirectory)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentDirectory);

        var diagnostics = new List<Diagnostic>();
        var rootResult = projectRootResolver.Resolve(request.ProjectDirectory, currentDirectory);
        if (!rootResult.Succeeded)
        {
            diagnostics.Add(rootResult.Diagnostic!);
            return ScriptPreparationResult.Failure(CliExitCode.FileOrDirectoryError, diagnostics);
        }

        var configResult = projectConfigLoader.Load(rootResult.ProjectRoot!);
        if (!configResult.Succeeded)
        {
            diagnostics.Add(configResult.Diagnostic!);
            return ScriptPreparationResult.Failure(CliExitCode.FileOrDirectoryError, diagnostics);
        }

        var config = configResult.Config!;
        var entryPath = string.IsNullOrWhiteSpace(request.EntryPath) ? config.EntryPath : request.EntryPath;
        var entryAbsolutePath = ResolveProjectPath(config.ProjectRoot, entryPath);
        var entryDisplayPath = NormalizeDisplayPath(entryPath);
        var kelResult = sourceFileParser.ParseKel(entryAbsolutePath, entryDisplayPath);
        if (kelResult.Status != SourceParseStatus.Success)
        {
            diagnostics.Add(kelResult.Diagnostic!);
            return ScriptPreparationResult.Failure(MapParseStatus(kelResult.Status), diagnostics);
        }

        var entryDocuments = new List<ScriptDocument>();
        foreach (var scriptReference in scriptReferenceResolver.ResolveScriptReferences(kelResult.Syntax!))
        {
            var scriptAbsolutePath = ResolveProjectPath(config.ProjectRoot, scriptReference);
            var scriptDisplayPath = NormalizeDisplayPath(scriptReference);
            var scriptResult = sourceFileParser.ParseKe(scriptAbsolutePath, scriptDisplayPath);
            if (scriptResult.Status == SourceParseStatus.Success)
            {
                entryDocuments.Add(new ScriptDocument(
                    scriptDisplayPath,
                    Path.GetFileNameWithoutExtension(scriptDisplayPath),
                    scriptResult.Syntax!));
                continue;
            }

            diagnostics.Add(scriptResult.Diagnostic!);
            if (scriptResult.Status == SourceParseStatus.FileError)
            {
                return ScriptPreparationResult.Failure(CliExitCode.FileOrDirectoryError, diagnostics);
            }
        }

        if (diagnostics.Count > 0)
        {
            return ScriptPreparationResult.Failure(CliExitCode.SyntaxError, diagnostics);
        }

        var semanticResult = semanticAnalyzer.Analyze(config, entryDocuments);
        var exitCode = WarningPolicy.Apply(
            semanticResult.ExitCode,
            semanticResult.Diagnostics,
            request.WarningsAsErrors || config.WarningsAsErrors);

        return new ScriptPreparationResult(config, semanticResult, entryDisplayPath, exitCode, semanticResult.Diagnostics);
    }

    private static CliExitCode MapParseStatus(SourceParseStatus status)
    {
        return status switch
        {
            SourceParseStatus.Success => CliExitCode.Success,
            SourceParseStatus.FileError => CliExitCode.FileOrDirectoryError,
            SourceParseStatus.SyntaxError => CliExitCode.SyntaxError,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
        };
    }

    private static string ResolveProjectPath(string projectRoot, string projectRelativePath)
    {
        return Path.GetFullPath(Path.Combine(projectRoot, projectRelativePath));
    }

    private static string NormalizeDisplayPath(string path)
    {
        return path.Replace('\\', '/');
    }
}
