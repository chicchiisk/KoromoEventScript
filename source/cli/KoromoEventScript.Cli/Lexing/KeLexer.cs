using System.Text;

namespace KoromoEventScript.Cli.Lexing;

public sealed class KeLexer
{
    private static readonly HashSet<string> Keywords =
    [
        "import", "var", "fn", "class", "enum", "actor", "public", "private",
        "if", "else", "while", "for", "in", "break", "continue", "using",
        "as", "return", "new", "say", "nar", "select", "case", "label", "jump",
        "true", "false", "null",
    ];

    private readonly string _source;
    private readonly List<Token> _tokens = [];
    private readonly Stack<int> _indentStack = new();
    private readonly Stack<int> _textBlockIndentStack = new();

    private int _position;
    private int _line = 1;
    private int _column = 1;
    private bool _atLineStart = true;
    private int _significantTokensOnLine;
    private bool _currentLineStartsTextStatement;
    private bool _pendingTextBlock;

    public KeLexer(string source)
    {
        _source = source.Replace("\r\n", "\n").Replace('\r', '\n');
        _indentStack.Push(0);
    }

    public static LexerResult Lex(string source)
    {
        return new KeLexer(source).Lex();
    }

    public LexerResult Lex()
    {
        while (!IsAtEnd())
        {
            if (_atLineStart)
            {
                ConsumeIndentation();
                if (IsAtEnd())
                {
                    break;
                }

                if (!_atLineStart && IsInsideTextBlock())
                {
                    LexTextLine();
                    continue;
                }
            }

            var current = Peek();

            if (current == '\n')
            {
                AddToken(TokenKind.Newline, "\n");
                Advance();
                _atLineStart = true;
                continue;
            }

            if (current == ' ' || current == '\t')
            {
                if (current == '\t')
                {
                    ThrowSyntax("KES1003", "Tabs are not allowed for indentation.", _line, _column);
                }

                Advance();
                continue;
            }

            if (current == '/' && PeekNext() == '/')
            {
                SkipLineComment();
                continue;
            }

            if (current == '/' && PeekNext() == '*')
            {
                SkipBlockComment();
                continue;
            }

            if (current == '"')
            {
                LexString();
                continue;
            }

            if (current == '#' && IsIdentifierStart(PeekNext()))
            {
                LexTag();
                continue;
            }

            if (char.IsDigit(current))
            {
                LexNumber();
                continue;
            }

            if (IsIdentifierStart(current))
            {
                LexIdentifierOrKeyword();
                continue;
            }

            if (!TryLexPunctuation())
            {
                ThrowSyntax("KES1004", $"Unexpected character '{current}'.", _line, _column);
            }
        }

        if (_tokens.Count > 0 && _tokens[^1].Kind != TokenKind.Newline)
        {
            AddToken(TokenKind.Newline, "\n", _line, _column);
        }

        while (_indentStack.Count > 1)
        {
            _indentStack.Pop();
            AddToken(TokenKind.Dedent, string.Empty, _line, 1);
        }

        AddToken(TokenKind.EndOfFile, string.Empty, _line, _column);
        return new LexerResult(_tokens);
    }

    private void ConsumeIndentation()
    {
        var indent = 0;
        var indentColumn = _column;

        while (true)
        {
            while (!IsAtEnd())
            {
                var current = Peek();
                if (current == ' ')
                {
                    indent++;
                    Advance();
                    continue;
                }

                if (current == '\t')
                {
                    ThrowSyntax("KES1003", "Tabs are not allowed for indentation.", _line, _column);
                }

                break;
            }

            if (IsAtEnd())
            {
                return;
            }

            if (Peek() == '\n')
            {
                AddToken(TokenKind.Newline, "\n");
                Advance();
                _atLineStart = true;
                indent = 0;
                indentColumn = _column;
                continue;
            }

            if (Peek() == '/' && PeekNext() == '/')
            {
                SkipLineComment();
                if (!IsAtEnd() && Peek() == '\n')
                {
                    AddToken(TokenKind.Newline, "\n");
                    Advance();
                    _atLineStart = true;
                    indent = 0;
                    indentColumn = _column;
                    continue;
                }

                return;
            }

            if (Peek() == '/' && PeekNext() == '*')
            {
                var commentStartLine = _line;
                SkipBlockComment();

                if (_line != commentStartLine)
                {
                    indent = 0;
                    indentColumn = _column;
                    continue;
                }
            }

            EmitIndentation(indent, indentColumn);
            _atLineStart = false;
            return;
        }
    }

    private void EmitIndentation(int indent, int column)
    {
        while (_textBlockIndentStack.Count > 0 && indent < _textBlockIndentStack.Peek())
        {
            _textBlockIndentStack.Pop();
        }

        var currentIndent = _indentStack.Peek();
        if (indent == currentIndent)
        {
            _pendingTextBlock = false;
            return;
        }

        if (indent > currentIndent)
        {
            _indentStack.Push(indent);
            AddToken(TokenKind.Indent, string.Empty, _line, column);
            if (_pendingTextBlock)
            {
                _textBlockIndentStack.Push(indent);
            }

            _pendingTextBlock = false;
            return;
        }

        while (_indentStack.Count > 1 && indent < _indentStack.Peek())
        {
            _indentStack.Pop();
            AddToken(TokenKind.Dedent, string.Empty, _line, column);
        }

        if (_indentStack.Peek() != indent)
        {
            ThrowSyntax("KES1005", "Indentation does not match any outer block.", _line, column);
        }

        _pendingTextBlock = false;
    }

    private void SkipLineComment()
    {
        Advance();
        Advance();

        while (!IsAtEnd() && Peek() != '\n')
        {
            Advance();
        }
    }

    private void SkipBlockComment()
    {
        var startLine = _line;
        var startColumn = _column;

        Advance();
        Advance();

        while (!IsAtEnd())
        {
            if (Peek() == '*' && PeekNext() == '/')
            {
                Advance();
                Advance();
                return;
            }

            if (Peek() == '\n')
            {
                Advance();
                continue;
            }

            Advance();
        }

        ThrowSyntax("KES1002", "Block comment is not terminated.", startLine, startColumn);
    }

    private void LexString()
    {
        var startLine = _line;
        var startColumn = _column;
        var builder = new StringBuilder();

        Advance();

        while (!IsAtEnd())
        {
            var current = Peek();
            if (current == '"')
            {
                Advance();
                AddToken(TokenKind.StringLiteral, builder.ToString(), startLine, startColumn);
                return;
            }

            if (current == '\n')
            {
                ThrowSyntax("KES1001", "String literal is not terminated.", startLine, startColumn);
            }

            if (current == '\\')
            {
                Advance();
                if (IsAtEnd())
                {
                    break;
                }

                var escaped = Peek();
                builder.Append(escaped switch
                {
                    '"' => '"',
                    '\\' => '\\',
                    'n' => '\n',
                    't' => '\t',
                    _ => escaped,
                });
                Advance();
                continue;
            }

            builder.Append(current);
            Advance();
        }

        ThrowSyntax("KES1001", "String literal is not terminated.", startLine, startColumn);
    }

    private void LexTextLine()
    {
        var startLine = _line;
        var startColumn = _column;
        var builder = new StringBuilder();

        while (!IsAtEnd() && Peek() != '\n')
        {
            builder.Append(Peek());
            Advance();
        }

        AddToken(TokenKind.StringLiteral, builder.ToString(), startLine, startColumn);
    }

    private void LexTag()
    {
        var startLine = _line;
        var startColumn = _column;
        Advance();

        var builder = new StringBuilder("#");
        builder.Append(ReadIdentifier());
        AddToken(TokenKind.Tag, builder.ToString(), startLine, startColumn);
    }

    private void LexNumber()
    {
        var startLine = _line;
        var startColumn = _column;
        var builder = new StringBuilder();

        while (!IsAtEnd() && char.IsDigit(Peek()))
        {
            builder.Append(Peek());
            Advance();
        }

        if (!IsAtEnd() && Peek() == '.' && char.IsDigit(PeekNext()))
        {
            builder.Append('.');
            Advance();

            while (!IsAtEnd() && char.IsDigit(Peek()))
            {
                builder.Append(Peek());
                Advance();
            }
        }

        AddToken(TokenKind.NumberLiteral, builder.ToString(), startLine, startColumn);
    }

    private void LexIdentifierOrKeyword()
    {
        var startLine = _line;
        var startColumn = _column;
        var text = ReadIdentifier();
        var kind = Keywords.Contains(text) ? TokenKind.Keyword : TokenKind.Identifier;
        AddToken(kind, text, startLine, startColumn);
    }

    private string ReadIdentifier()
    {
        var builder = new StringBuilder();
        builder.Append(Peek());
        Advance();

        while (!IsAtEnd() && IsIdentifierPart(Peek()))
        {
            builder.Append(Peek());
            Advance();
        }

        return builder.ToString();
    }

    private bool TryLexPunctuation()
    {
        var kind = Peek() switch
        {
            ':' => TokenKind.Colon,
            ',' => TokenKind.Comma,
            '.' => TokenKind.Dot,
            '(' => TokenKind.OpenParen,
            ')' => TokenKind.CloseParen,
            '[' => TokenKind.OpenBracket,
            ']' => TokenKind.CloseBracket,
            ';' => TokenKind.Semicolon,
            '@' => TokenKind.At,
            '+' => TokenKind.Plus,
            '-' => TokenKind.Minus,
            '*' => TokenKind.Star,
            '/' => TokenKind.Slash,
            _ => TokenKind.EndOfFile,
        };

        if (kind != TokenKind.EndOfFile)
        {
            AddToken(kind, Peek().ToString());
            Advance();
            return true;
        }

        if (Peek() == '=')
        {
            var tokenKind = PeekNext() == '=' ? TokenKind.DoubleEquals : TokenKind.Equals;
            var lexeme = tokenKind == TokenKind.DoubleEquals ? "==" : "=";
            AddToken(tokenKind, lexeme);
            Advance();
            if (tokenKind == TokenKind.DoubleEquals)
            {
                Advance();
            }

            return true;
        }

        if (Peek() == '!')
        {
            var tokenKind = PeekNext() == '=' ? TokenKind.NotEquals : TokenKind.Bang;
            var lexeme = tokenKind == TokenKind.NotEquals ? "!=" : "!";
            AddToken(tokenKind, lexeme);
            Advance();
            if (tokenKind == TokenKind.NotEquals)
            {
                Advance();
            }

            return true;
        }

        if (Peek() == '<')
        {
            var tokenKind = PeekNext() == '=' ? TokenKind.LessOrEqual : TokenKind.Less;
            var lexeme = tokenKind == TokenKind.LessOrEqual ? "<=" : "<";
            AddToken(tokenKind, lexeme);
            Advance();
            if (tokenKind == TokenKind.LessOrEqual)
            {
                Advance();
            }

            return true;
        }

        if (Peek() == '>')
        {
            var tokenKind = PeekNext() == '=' ? TokenKind.GreaterOrEqual : TokenKind.Greater;
            var lexeme = tokenKind == TokenKind.GreaterOrEqual ? ">=" : ">";
            AddToken(tokenKind, lexeme);
            Advance();
            if (tokenKind == TokenKind.GreaterOrEqual)
            {
                Advance();
            }

            return true;
        }

        if (Peek() == '&' && PeekNext() == '&')
        {
            AddToken(TokenKind.AndAnd, "&&");
            Advance();
            Advance();
            return true;
        }

        if (Peek() == '|' && PeekNext() == '|')
        {
            AddToken(TokenKind.OrOr, "||");
            Advance();
            Advance();
            return true;
        }

        return false;
    }

    private void AddToken(TokenKind kind, string lexeme)
    {
        AddToken(kind, lexeme, _line, _column);
    }

    private void AddToken(TokenKind kind, string lexeme, int line, int column)
    {
        _tokens.Add(new Token(kind, lexeme, line, column));
        RecordToken(kind, lexeme);
    }

    private void RecordToken(TokenKind kind, string lexeme)
    {
        if (kind == TokenKind.Newline)
        {
            ResetLineState();
            return;
        }

        if (kind is TokenKind.Indent or TokenKind.Dedent or TokenKind.EndOfFile)
        {
            return;
        }

        if (_significantTokensOnLine == 0)
        {
            _currentLineStartsTextStatement = kind == TokenKind.Keyword && (lexeme == "say" || lexeme == "nar");
        }

        _significantTokensOnLine++;

        if (kind == TokenKind.Colon && _currentLineStartsTextStatement)
        {
            _pendingTextBlock = true;
        }
    }

    private void ResetLineState()
    {
        _significantTokensOnLine = 0;
        _currentLineStartsTextStatement = false;
    }

    private bool IsInsideTextBlock()
    {
        return _textBlockIndentStack.Count > 0 && _indentStack.Peek() >= _textBlockIndentStack.Peek();
    }

    private void ThrowSyntax(string code, string message, int line, int column)
    {
        throw new LexerException(new LexerDiagnostic(code, message, line, column));
    }

    private bool IsAtEnd() => _position >= _source.Length;

    private char Peek() => IsAtEnd() ? '\0' : _source[_position];

    private char PeekNext() => _position + 1 >= _source.Length ? '\0' : _source[_position + 1];

    private void Advance()
    {
        if (IsAtEnd())
        {
            return;
        }

        var current = _source[_position];
        _position++;

        if (current == '\n')
        {
            _line++;
            _column = 1;
            _atLineStart = true;
            return;
        }

        _column++;
    }

    private static bool IsIdentifierStart(char value)
    {
        if (value == '_')
        {
            return true;
        }

        if (value == '\0' || char.IsDigit(value))
        {
            return false;
        }

        return char.IsLetter(value);
    }

    private static bool IsIdentifierPart(char value)
    {
        if (value == '_')
        {
            return true;
        }

        if (char.IsDigit(value))
        {
            return true;
        }

        return char.IsLetter(value);
    }
}
