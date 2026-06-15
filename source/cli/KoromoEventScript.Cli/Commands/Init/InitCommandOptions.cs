using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Commands.Init;

public sealed record InitCommandOptions(
    string? ProjectDirectory,
    string? ProjectName,
    InitTemplate Template,
    bool Force,
    bool NoSample,
    DiagnosticOutputFormat OutputFormat);
