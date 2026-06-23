using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Commands.Publish;

public sealed record PublishCommandOptions(
    string? ProjectDirectory,
    DiagnosticOutputFormat OutputFormat,
    string Target = "windows",
    string Configuration = "release",
    string? OutputDirectory = null,
    string Archive = "none",
    bool IncludeSource = false,
    string? Locale = null,
    bool Clean = false);
