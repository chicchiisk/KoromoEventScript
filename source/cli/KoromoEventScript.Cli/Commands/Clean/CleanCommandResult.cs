using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Commands.Clean;

public sealed record CleanCommandResult(
    CliExitCode ExitCode,
    IReadOnlyList<Diagnostic> Diagnostics,
    IReadOnlyList<string> DeletedPaths);
