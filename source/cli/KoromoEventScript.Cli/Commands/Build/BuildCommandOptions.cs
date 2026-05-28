using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Commands.Build;

public sealed record BuildCommandOptions(
    string? ProjectDirectory,
    DiagnosticOutputFormat OutputFormat,
    bool WarningsAsErrors = false);
