using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.Commands.Build;
using KoromoEventScript.Cli.Diagnostics;
using KoromoEventScript.Cli.ProjectSystem;
using KoromoEventScript.Cli.Semantics;

namespace KoromoEventScript.Cli.Build;

public sealed class BuildPreparationService
{
    private readonly ScriptPreparationService scriptPreparationService;

    public BuildPreparationService()
        : this(new ScriptPreparationService())
    {
    }

    public BuildPreparationService(
        ProjectRootResolver projectRootResolver,
        ProjectConfigLoader projectConfigLoader,
        SourceFileParser sourceFileParser,
        KelScriptReferenceResolver scriptReferenceResolver,
        SemanticAnalyzer semanticAnalyzer)
        : this(new ScriptPreparationService(
            projectRootResolver,
            projectConfigLoader,
            sourceFileParser,
            scriptReferenceResolver,
            semanticAnalyzer))
    {
    }

    public BuildPreparationService(ScriptPreparationService scriptPreparationService)
    {
        this.scriptPreparationService = scriptPreparationService;
    }

    public BuildPreparationResult Prepare(BuildCommandOptions options, string currentDirectory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentDirectory);

        var result = scriptPreparationService.Prepare(
            new ScriptPreparationRequest(
                options.ProjectDirectory,
                options.EntryPath,
                options.WarningsAsErrors),
            currentDirectory);

        return result.Succeeded
            ? new BuildPreparationResult(result.Config, result.SemanticResult, result.EntryDisplayPath, result.ExitCode, result.Diagnostics)
            : BuildPreparationResult.Failure(result.ExitCode, result.Diagnostics);
    }
}

public sealed record BuildPreparationResult(
    ProjectConfig? Config,
    SemanticAnalysisResult? SemanticResult,
    string? EntryPath,
    CliExitCode ExitCode,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool Succeeded => ExitCode == CliExitCode.Success && Config is not null && SemanticResult is not null && EntryPath is not null;

    public static BuildPreparationResult Failure(CliExitCode exitCode, IReadOnlyList<Diagnostic> diagnostics)
    {
        return new BuildPreparationResult(null, null, null, exitCode, diagnostics.ToArray());
    }
}
