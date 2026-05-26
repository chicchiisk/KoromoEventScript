using KoromoEventScript.Cli.Diagnostics;
using KoromoEventScript.Cli.Lexing;

namespace KoromoEventScript.Cli.Parsing;

public sealed class KeParser
{
    private readonly IReadOnlyList<Token> _tokens;
    private int _position;
    private bool _seenNonImportStatement;

    private KeParser(IReadOnlyList<Token> tokens)
    {
        _tokens = tokens;
    }

    public static ScriptSyntax Parse(string source)
    {
        return Parse(KeLexer.Lex(source));
    }

    public static ScriptSyntax Parse(LexerResult lexerResult)
    {
        return new KeParser(lexerResult.Tokens).Parse();
    }

    public ScriptSyntax Parse()
    {
        var statements = new List<StatementSyntax>();

        SkipNewlines();

        while (!IsAtEnd())
        {
            statements.Add(ParseStatement());
            SkipNewlines();
        }

        return new ScriptSyntax(statements);
    }

    private StatementSyntax ParseStatement()
    {
        if (IsKeyword("import"))
        {
            if (_seenNonImportStatement)
            {
                ThrowCurrent("KES2005", "Import statements must appear at the top of the file.");
            }

            return ParseImportStatement();
        }

        _seenNonImportStatement = true;

        if (IsKeyword("var"))
        {
            return ParseVarStatement();
        }

        if (IsKeyword("label"))
        {
            return ParseLabelStatement();
        }

        if (IsKeyword("jump"))
        {
            return ParseJumpStatement();
        }

        if (IsKeyword("say"))
        {
            return ParseSayStatement();
        }

        if (IsKeyword("nar"))
        {
            return ParseNarStatement();
        }

        if (IsKeyword("select"))
        {
            return ParseSelectStatement();
        }

        if (IsKeyword("case"))
        {
            ThrowCurrent("KES2006", "Case statements can only appear inside a select block.");
        }

        if (Current.Kind != TokenKind.Identifier)
        {
            ThrowCurrent("KES2001", $"Unexpected token '{Current.Lexeme}'.");
        }

        return ParseCommandOrLessStatement();
    }

    private ImportStatementSyntax ParseImportStatement()
    {
        ConsumeKeyword("import");
        var moduleToken = Consume(TokenKind.Identifier, "KES2001", "Expected a module name after import.");
        EnsureLineEndsNow("KES2001", "Import statements only support a single module name.");
        return new ImportStatementSyntax(moduleToken.Lexeme);
    }

    private VarStatementSyntax ParseVarStatement()
    {
        ConsumeKeyword("var");
        var nameToken = Consume(TokenKind.Identifier, "KES2001", "Expected a variable name after var.");

        List<Token> typeTokens = [];
        List<Token> valueTokens = [];

        if (Match(TokenKind.Colon))
        {
            typeTokens = ReadUntil(TokenKind.Equals, TokenKind.Newline);
            if (typeTokens.Count == 0)
            {
                ThrowCurrent("KES2001", "Expected a type after ':'.");
            }
        }

        if (Match(TokenKind.Equals))
        {
            valueTokens = ReadUntil(TokenKind.Newline);
            if (valueTokens.Count == 0)
            {
                ThrowCurrent("KES2001", "Expected an initializer after '='.");
            }
        }
        else if (typeTokens.Count == 0)
        {
            ThrowCurrent("KES2001", "Variable declarations without an initializer must include a type annotation.");
        }

        ExpectLineTerminator();
        return new VarStatementSyntax(nameToken.Lexeme, typeTokens, valueTokens, ToLocation(nameToken));
    }

    private LabelStatementSyntax ParseLabelStatement()
    {
        ConsumeKeyword("label");
        var tagToken = Consume(TokenKind.Tag, "KES2001", "Expected a tag after label.");
        EnsureLineEndsNow("KES2001", "Label statements only support a single tag.");
        return new LabelStatementSyntax(tagToken.Lexeme, ToLocation(tagToken));
    }

    private JumpStatementSyntax ParseJumpStatement()
    {
        ConsumeKeyword("jump");
        var tagToken = Consume(TokenKind.Tag, "KES2001", "Expected a tag after jump.");
        EnsureLineEndsNow("KES2001", "Jump statements only support a single tag.");
        return new JumpStatementSyntax(tagToken.Lexeme, ToLocation(tagToken));
    }

    private SayStatementSyntax ParseSayStatement()
    {
        ConsumeKeyword("say");
        var speakerToken = Consume(TokenKind.Identifier, "KES2001", "Expected an actor identifier after say.");
        var tagToken = Match(TokenKind.Tag) ? Previous : null;
        var tag = tagToken?.Lexeme;

        if (!Match(TokenKind.Colon))
        {
            ThrowCurrent("KES2002", "Say statements must end with ':'.");
        }

        var lines = ParseTextBlock("KES2003", "Say blocks must contain at least one text line.");
        return new SayStatementSyntax(speakerToken.Lexeme, tag, lines, tagToken is null ? null : ToLocation(tagToken));
    }

    private NarStatementSyntax ParseNarStatement()
    {
        ConsumeKeyword("nar");
        var tagToken = Match(TokenKind.Tag) ? Previous : null;
        var tag = tagToken?.Lexeme;

        if (!Match(TokenKind.Colon))
        {
            ThrowCurrent("KES2002", "Nar statements must end with ':'.");
        }

        var lines = ParseTextBlock("KES2003", "Nar blocks must contain at least one text line.");
        return new NarStatementSyntax(tag, lines, tagToken is null ? null : ToLocation(tagToken));
    }

    private SelectStatementSyntax ParseSelectStatement()
    {
        ConsumeKeyword("select");
        if (!Match(TokenKind.Colon))
        {
            ThrowCurrent("KES2002", "Select statements must end with ':'.");
        }

        ExpectIndentedBlock("KES2004", "Select statements must have an indented block.");

        var cases = new List<CaseClauseSyntax>();
        while (!IsAtEnd() && !Check(TokenKind.Dedent))
        {
            if (Match(TokenKind.Newline))
            {
                continue;
            }

            ConsumeKeyword("case");
            var textToken = Consume(TokenKind.StringLiteral, "KES2001", "Case statements require a string literal.");
            var tagToken = Consume(TokenKind.Tag, "KES2001", "Case statements require a jump target tag.");
            EnsureLineEndsNow("KES2001", "Case statements only support a string literal and a tag.");
            cases.Add(new CaseClauseSyntax(textToken.Lexeme, tagToken.Lexeme, ToLocation(tagToken)));
        }

        Consume(TokenKind.Dedent, "KES2004", "Select statements must end their block with a dedent.");

        if (cases.Count == 0)
        {
            ThrowPrevious("KES2004", "Select blocks must contain at least one case.");
        }

        return new SelectStatementSyntax(cases);
    }

    private StatementSyntax ParseCommandOrLessStatement()
    {
        var lineTokens = ReadUntil(TokenKind.Newline);
        if (lineTokens.Count == 0)
        {
            ThrowCurrent("KES2001", "Expected a command statement.");
        }

        ExpectLineTerminator();

        var nameToken = lineTokens[0];
        if (lineTokens[^1].Kind == TokenKind.Colon)
        {
            var sharedArguments = lineTokens.Skip(1).Take(lineTokens.Count - 2).ToArray();
            var items = ParseLessBlock();
            return new LessStatementSyntax(nameToken.Lexeme, sharedArguments, items);
        }

        var arguments = lineTokens.Skip(1).ToArray();
        return new CommandStatementSyntax(nameToken.Lexeme, arguments);
    }

    private IReadOnlyList<LessBlockItemSyntax> ParseLessBlock()
    {
        SkipNewlines();
        if (!Match(TokenKind.Indent))
        {
            ThrowCurrent("KES2004", "LESS statements must have an indented block.");
        }

        var items = new List<LessBlockItemSyntax>();
        while (!IsAtEnd() && !Check(TokenKind.Dedent))
        {
            if (Match(TokenKind.Newline))
            {
                continue;
            }

            var itemTokens = ReadUntil(TokenKind.Newline);
            ExpectLineTerminator();

            if (itemTokens.Count == 0)
            {
                continue;
            }

            if (itemTokens[^1].Kind == TokenKind.Colon)
            {
                var nameToken = itemTokens[0];
                if (nameToken.Kind != TokenKind.Identifier)
                {
                    ThrowPrevious("KES2004", "Nested LESS statements must start with a command name.");
                }

                var sharedArguments = itemTokens.Skip(1).Take(itemTokens.Count - 2).ToArray();
                var nestedItems = ParseLessBlock();
                items.Add(new LessNestedStatementSyntax(new LessStatementSyntax(nameToken.Lexeme, sharedArguments, nestedItems)));
                continue;
            }

            items.Add(new LessCommandItemSyntax(itemTokens));
        }

        Consume(TokenKind.Dedent, "KES2004", "LESS statements must end their block with a dedent.");
        return items;
    }

    private IReadOnlyList<TextLineSyntax> ParseTextBlock(string errorCode, string emptyBlockMessage)
    {
        ExpectIndentedBlock(errorCode, emptyBlockMessage);

        var lines = new List<TextLineSyntax>();
        while (!IsAtEnd() && !Check(TokenKind.Dedent))
        {
            if (Match(TokenKind.Newline))
            {
                continue;
            }

            if (Check(TokenKind.Indent))
            {
                ThrowCurrent(errorCode, "Nested blocks are not supported inside text blocks.");
            }

            var textToken = Consume(TokenKind.StringLiteral, errorCode, "Text blocks may only contain text lines.");
            lines.Add(new TextLineSyntax(textToken.Lexeme, textToken.Lexeme.StartsWith('@')));
            ExpectLineTerminator();
        }

        Consume(TokenKind.Dedent, errorCode, "Text blocks must end their block with a dedent.");

        if (lines.Count == 0)
        {
            ThrowPrevious(errorCode, emptyBlockMessage);
        }

        return lines;
    }

    private void ExpectIndentedBlock(string errorCode, string message)
    {
        ExpectLineTerminator();
        SkipNewlines();

        if (!Match(TokenKind.Indent))
        {
            ThrowCurrent(errorCode, message);
        }
    }

    private List<Token> ReadUntil(params TokenKind[] terminators)
    {
        var results = new List<Token>();
        while (!IsAtEnd() && Array.IndexOf(terminators, Current.Kind) < 0)
        {
            results.Add(Advance());
        }

        return results;
    }

    private void EnsureLineEndsNow(string errorCode, string message)
    {
        if (!Check(TokenKind.Newline))
        {
            ThrowCurrent(errorCode, message);
        }

        ExpectLineTerminator();
    }

    private void ExpectLineTerminator()
    {
        Consume(TokenKind.Newline, "KES2001", "Expected the statement to end at the current line.");
    }

    private Token ConsumeKeyword(string keyword)
    {
        if (!IsKeyword(keyword))
        {
            ThrowCurrent("KES2001", $"Expected '{keyword}'.");
        }

        return Advance();
    }

    private Token Consume(TokenKind kind, string errorCode, string message)
    {
        if (!Check(kind))
        {
            ThrowCurrent(errorCode, message);
        }

        return Advance();
    }

    private static SourceLocation ToLocation(Token token)
    {
        return new SourceLocation(token.Line, token.Column);
    }

    private void SkipNewlines()
    {
        while (Match(TokenKind.Newline))
        {
        }
    }

    private bool IsKeyword(string lexeme)
    {
        return Check(TokenKind.Keyword) && string.Equals(Current.Lexeme, lexeme, StringComparison.Ordinal);
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
        return !IsAtEnd() && Current.Kind == kind;
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

    private void ThrowCurrent(string code, string message)
    {
        throw new ParserException(new Diagnostic(DiagnosticLevel.Error, code, string.Empty, Current.Line, Current.Column, message));
    }

    private void ThrowPrevious(string code, string message)
    {
        throw new ParserException(new Diagnostic(DiagnosticLevel.Error, code, string.Empty, Previous.Line, Previous.Column, message));
    }

    private Token Current => _tokens[_position];

    private Token Previous => _tokens[Math.Max(_position - 1, 0)];
}
