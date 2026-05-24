using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Lexing;

public sealed class LexerException : Exception
{
    public LexerException(Diagnostic diagnostic)
        : base($"{diagnostic.Code}: {diagnostic.Message} ({diagnostic.Line},{diagnostic.Column})")
    {
        Diagnostic = diagnostic;
    }

    public Diagnostic Diagnostic { get; }
}
