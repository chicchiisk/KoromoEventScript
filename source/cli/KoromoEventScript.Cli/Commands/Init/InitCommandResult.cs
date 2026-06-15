using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Commands.Init;

public sealed record InitCommandResult(
    CliExitCode ExitCode,
    IReadOnlyList<Diagnostic> Diagnostics,
    string? SuccessMessage,
    string? ProjectRoot);
