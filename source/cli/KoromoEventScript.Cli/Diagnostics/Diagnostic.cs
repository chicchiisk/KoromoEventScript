namespace KoromoEventScript.Cli.Diagnostics;

public sealed record Diagnostic(
    DiagnosticLevel Level,
    string Code,
    string File,
    int Line,
    int Column,
    string Message);