using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.Diagnostics;
using KoromoEventScript.Cli.ProjectSystem;
using KoromoEventScript.Cli.Semantics;

namespace KoromoEventScript.Cli.Build;

public sealed record ScriptPreparationResult(
    ProjectConfig? Config,
    SemanticAnalysisResult? SemanticResult,
    string? EntryDisplayPath,
    CliExitCode ExitCode,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool Succeeded => ExitCode == CliExitCode.Success && Config is not null && SemanticResult is not null;

    public static ScriptPreparationResult Failure(CliExitCode exitCode, IReadOnlyList<Diagnostic> diagnostics)
    {
        return new ScriptPreparationResult(null, null, null, exitCode, diagnostics.ToArray());
    }
}
