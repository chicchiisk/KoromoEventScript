using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Commands.Build;

public sealed record BuildCommandOptions(
    string? ProjectDirectory,
    DiagnosticOutputFormat OutputFormat,
    bool WarningsAsErrors = false,
    string? EntryPath = null,
    bool CheckOnly = false,
    bool EmitTextIr = false,
    string Target = "windows",
    string? OutputDirectory = null,
    string? Locale = null);
