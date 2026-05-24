using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Parsing;

public sealed class ParserException : Exception
{
    public ParserException(Diagnostic diagnostic)
        : base($"{diagnostic.Code}: {diagnostic.Message} ({diagnostic.Line},{diagnostic.Column})")
    {
        Diagnostic = diagnostic;
    }

    public Diagnostic Diagnostic { get; }
}