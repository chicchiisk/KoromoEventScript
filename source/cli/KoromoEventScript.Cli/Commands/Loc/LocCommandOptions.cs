using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Commands.Loc;

public sealed record LocCommandOptions(
    string? ProjectDirectory,
    IReadOnlyList<string> RequestedLocales,
    string? OutputPath,
    DiagnosticOutputFormat OutputFormat);
