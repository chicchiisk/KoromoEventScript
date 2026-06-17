using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Commands.Correct;

public sealed record CorrectCommandOptions(
    string? ProjectDirectory,
    string? EntryPath,
    bool CheckOnly,
    DiagnosticOutputFormat OutputFormat);
