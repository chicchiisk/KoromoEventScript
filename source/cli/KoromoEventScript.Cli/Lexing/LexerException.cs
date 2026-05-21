namespace KoromoEventScript.Cli.Lexing;

public sealed class LexerException : Exception
{
    public LexerException(LexerDiagnostic diagnostic)
        : base($"{diagnostic.Code}: {diagnostic.Message} ({diagnostic.Line},{diagnostic.Column})")
    {
        Diagnostic = diagnostic;
    }

    public LexerDiagnostic Diagnostic { get; }
}
