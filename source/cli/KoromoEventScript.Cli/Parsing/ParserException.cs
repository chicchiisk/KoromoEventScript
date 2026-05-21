namespace KoromoEventScript.Cli.Parsing;

public sealed class ParserException : Exception
{
    public ParserException(ParserDiagnostic diagnostic)
        : base($"{diagnostic.Code}: {diagnostic.Message} ({diagnostic.Line},{diagnostic.Column})")
    {
        Diagnostic = diagnostic;
    }

    public ParserDiagnostic Diagnostic { get; }
}