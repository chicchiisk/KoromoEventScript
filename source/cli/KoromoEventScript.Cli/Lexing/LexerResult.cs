namespace KoromoEventScript.Cli.Lexing;

public sealed class LexerResult
{
    public LexerResult(IReadOnlyList<Token> tokens)
    {
        Tokens = tokens;
    }

    public IReadOnlyList<Token> Tokens { get; }
}
