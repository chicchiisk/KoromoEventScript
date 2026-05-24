using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Commands;

public sealed record CliInvocationResult(
    CliExitCode ExitCode,
    IReadOnlyList<Diagnostic> Diagnostics,
    DiagnosticOutputFormat OutputFormat);
