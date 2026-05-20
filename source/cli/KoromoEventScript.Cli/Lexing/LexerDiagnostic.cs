namespace KoromoEventScript.Cli.Lexing;

public sealed record LexerDiagnostic(
    string Code,
    string Message,
    int Line,
    int Column);
