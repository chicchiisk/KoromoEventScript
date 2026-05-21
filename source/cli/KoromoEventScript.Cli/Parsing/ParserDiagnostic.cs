namespace KoromoEventScript.Cli.Parsing;

public sealed record ParserDiagnostic(
    string Code,
    string Message,
    int Line,
    int Column);