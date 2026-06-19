using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Commands.Loc;

public sealed record LocCommandResult(
    CliExitCode ExitCode,
    IReadOnlyList<Diagnostic> Diagnostics,
    string? SuccessMessage = null);
