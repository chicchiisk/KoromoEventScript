using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Commands.Build;

public sealed record BuildCheckOnlyResult(
    CliExitCode ExitCode,
    IReadOnlyList<Diagnostic> Diagnostics);
