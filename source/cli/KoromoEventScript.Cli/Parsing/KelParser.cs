using KoromoEventScript.Cli.Diagnostics;
using KoromoEventScript.Cli.Lexing;

namespace KoromoEventScript.Cli.Parsing;

public sealed class KelParser
{
    private sealed class PropertyBuilder
    {
        public PropertyBuilder(string key, KelValueSyntax value)
        {
            Key = key;
            Values = [value];
        }

        public string Key { get; }

        public List<KelValueSyntax> Values { get; }
    }

    private readonly IReadOnlyList<Token> _tokens;
    private int _position;

    private KelParser(IReadOnlyList<Token> tokens)
    {
        _tokens = tokens;
    }

    public static KelDocumentSyntax Parse(string source)
    {
        return Parse(KeLexer.Lex(source));
    }

    public static KelDocumentSyntax Parse(LexerResult lexerResult)
    {
        return new KelParser(lexerResult.Tokens).Parse();
    }

    public KelDocumentSyntax Parse()
    {
        return new KelDocumentSyntax(ParseObjectBody(TokenKind.EndOfFile));
    }

    private KelObjectSyntax ParseObjectBody(TokenKind terminator)
    {
        SkipTrivia();

        var properties = new List<PropertyBuilder>();
        var propertyIndexByKey = new Dictionary<string, int>(StringComparer.Ordinal);

        while (!Check(terminator) && !IsAtEnd())
        {
            var key = ParseKey();
            Consume(TokenKind.Equals, "KES2001", "Expected '=' after key.");
            var value = ParseValue();
            EnsurePairEndsNow("KES2001", "Expected the pair to end after its value.");

            if (propertyIndexByKey.TryGetValue(key, out var existingIndex))
            {
                properties[existingIndex].Values.Add(value);
            }
            else
            {
                propertyIndexByKey[key] = properties.Count;
                properties.Add(new PropertyBuilder(key, value));
            }

            SkipTrivia();
        }

        return new KelObjectSyntax(properties
            .Select(static property => new KelPropertySyntax(property.Key, property.Values.ToArray()))
            .ToArray());
    }

    private KelValueSyntax ParseValue()
    {
        if (Match(TokenKind.OpenBrace))
        {
            SkipTrivia();
            var nestedObject = ParseObjectBody(TokenKind.CloseBrace);
            Consume(TokenKind.CloseBrace, "KES2001", "Expected '}' to close the object.");
            return new KelObjectValueSyntax(nestedObject);
        }

        if (Check(TokenKind.StringLiteral))
        {
            return new KelStringValueSyntax(Advance().Lexeme);
        }

        if (Check(TokenKind.NumberLiteral))
        {
            return new KelNumberValueSyntax(Advance().Lexeme);
        }

        if (IsNameSegment())
        {
            var name = ParseIdentifier();
            return name switch
            {
                "true" => new KelBooleanValueSyntax(true),
                "false" => new KelBooleanValueSyntax(false),
                _ => new KelIdentifierValueSyntax(name),
            };
        }

        ThrowCurrent("KES2001", "Expected an object, string, identifier, number, or boolean value.");
        return new KelStringValueSyntax(string.Empty);
    }

    private string ParseKey()
    {
        return ParseName(allowLeadingNumber: true, terminalDescription: "key");
    }

    private string ParseIdentifier()
    {
        return ParseName(allowLeadingNumber: false, terminalDescription: "identifier");
    }

    private string ParseName(bool allowLeadingNumber, string terminalDescription)
    {
        if (!IsNameSegment(allowLeadingNumber))
        {
            ThrowCurrent("KES2001", $"Expected a {terminalDescription}.");
        }

        var builder = new List<string> { Advance().Lexeme };

        while (Match(TokenKind.Dot))
        {
            if (!IsNameSegment(allowLeadingNumber: true))
            {
                ThrowCurrent("KES2001", $"Expected a {terminalDescription} segment after '.'.");
            }

            builder.Add(Advance().Lexeme);
        }

        return string.Join('.', builder);
    }

    private bool IsNameSegment(bool allowLeadingNumber = false)
    {
        return Check(TokenKind.Identifier)
            || Check(TokenKind.Keyword)
            || (allowLeadingNumber && Check(TokenKind.NumberLiteral));
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

    private void EnsurePairEndsNow(string code, string message)
    {
        if (!Check(TokenKind.Newline) && !Check(TokenKind.CloseBrace) && !IsAtEnd())
        {
            ThrowCurrent(code, message);
        }
    }

    private void SkipTrivia()
    {
        while (Match(TokenKind.Newline) || Match(TokenKind.Indent) || Match(TokenKind.Dedent))
        {
        }
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
        throw new ParserException(new Diagnostic(DiagnosticLevel.Error, code, string.Empty, Current.Line, Current.Column, message));
    }
}