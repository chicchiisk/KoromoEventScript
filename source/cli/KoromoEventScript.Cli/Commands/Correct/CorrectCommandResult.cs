using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Commands.Correct;

public sealed record CorrectCommandResult(
    CliExitCode ExitCode,
    IReadOnlyList<Diagnostic> Diagnostics,
    string? StandardOutput = null);
