using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Localization;

public sealed record ScriptRewriteResult(
    CliExitCode ExitCode,
    IReadOnlyList<Diagnostic> Diagnostics,
    IReadOnlyList<string> ChangedFiles)
{
    public bool Succeeded => ExitCode == CliExitCode.Success;
}
