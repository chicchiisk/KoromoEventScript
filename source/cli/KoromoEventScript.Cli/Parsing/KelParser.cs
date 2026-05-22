using KoromoEventScript.Cli.Lexing;

namespace KoromoEventScript.Cli.Parsing;

public sealed class KelParser
{
    private readonly IReadOnlyList<Token> _tokens;
    private int _position;

    private KelParser(IReadOnlyList<Token> tokens)
    {
        _tokens = tokens;
    }

    public static EventListSyntax Parse(string source)
    {
        return Parse(KeLexer.Lex(source));
    }

    public static EventListSyntax Parse(LexerResult lexerResult)
    {
        return new KelParser(lexerResult.Tokens).Parse();
    }

    public EventListSyntax Parse()
    {
        SkipNewlines();

        var entryEventId = ParseEntryStatement();
        SkipNewlines();

        var events = new List<EventDeclarationSyntax>();
        while (!IsAtEnd())
        {
            events.Add(ParseEventStatement());
            SkipNewlines();
        }

        if (events.Count == 0)
        {
            ThrowCurrent("KES2001", "Expected at least one event after entry.");
        }

        return new EventListSyntax(entryEventId, events);
    }

    private string ParseEntryStatement()
    {
        ConsumeWord("entry", "Expected an entry statement at the top of the file.");
        var entryToken = Consume(TokenKind.StringLiteral, "KES2001", "Expected an event ID string after entry.");
        EnsureLineEndsNow("KES2001", "Entry statements only support a single event ID string.");
        return entryToken.Lexeme;
    }

    private EventDeclarationSyntax ParseEventStatement()
    {
        ConsumeWord("event", "Expected an event statement after entry.");
        var eventIdToken = Consume(TokenKind.StringLiteral, "KES2001", "Expected an event ID string after event.");
        var scriptPathToken = Consume(TokenKind.StringLiteral, "KES2001", "Expected a .ke file path after the event ID.");
        var entryTagToken = Consume(TokenKind.Tag, "KES2001", "Expected an entry tag after the .ke file path.");
        EnsureLineEndsNow("KES2001", "Event statements only support an ID, a .ke file path, and an entry tag.");
        return new EventDeclarationSyntax(eventIdToken.Lexeme, scriptPathToken.Lexeme, entryTagToken.Lexeme);
    }

    private void ConsumeWord(string expectedLexeme, string message)
    {
        if (!IsWord(expectedLexeme))
        {
            ThrowCurrent("KES2001", message);
        }

        Advance();
    }

    private Token Consume(TokenKind kind, string code, string message)
    {
        if (Check(kind))
        {
            return Advance();
        }

        ThrowCurrent(code, message);
        return Current;
    }

    private void EnsureLineEndsNow(string code, string message)
    {
        if (!Check(TokenKind.Newline) && !IsAtEnd())
        {
            ThrowCurrent(code, message);
        }

        if (Match(TokenKind.Newline))
        {
            return;
        }

        if (!IsAtEnd())
        {
            ThrowCurrent(code, message);
        }
    }

    private void SkipNewlines()
    {
        while (Match(TokenKind.Newline))
        {
        }
    }

    private bool IsWord(string lexeme)
    {
        return (Check(TokenKind.Identifier) || Check(TokenKind.Keyword))
            && string.Equals(Current.Lexeme, lexeme, StringComparison.Ordinal);
    }

    private bool Match(TokenKind kind)
    {
        if (!Check(kind))
        {
            return false;
        }

        Advance();
        return true;
    }

    private bool Check(TokenKind kind)
    {
        if (IsAtEnd())
        {
            return kind == TokenKind.EndOfFile;
        }

        return Current.Kind == kind;
    }

    private Token Advance()
    {
        if (!IsAtEnd())
        {
            _position++;
        }

        return Previous;
    }

    private bool IsAtEnd()
    {
        return Current.Kind == TokenKind.EndOfFile;
    }

    private Token Current => _tokens[_position];

    private Token Previous => _tokens[_position - 1];

    private void ThrowCurrent(string code, string message)
    {
        throw new ParserException(new ParserDiagnostic(code, message, Current.Line, Current.Column));
    }
}