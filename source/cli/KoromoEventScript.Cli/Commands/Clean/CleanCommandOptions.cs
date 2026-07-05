using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Commands.Clean;

public sealed record CleanCommandOptions(
    string? ProjectDirectory,
    DiagnosticOutputFormat OutputFormat,
    string? Target = null,
    bool IncludeDist = false,
    bool DryRun = false);
