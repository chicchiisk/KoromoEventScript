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

        if (IsKeyword("fn"))
        {
            return ParseFunctionDeclaration();
        }

        if (IsKeyword("class"))
        {
            return ParseClassDeclaration();
        }

        if (IsKeyword("enum"))
        {
            return ParseEnumDeclaration();
        }

        if (IsKeyword("actor"))
        {
            return ParseActorDeclaration();
        }

        if (IsKeyword("standby"))
        {
            return ParseStandbyStatement();
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

        if (IsKeyword("if"))
        {
            return ParseIfStatement();
        }

        if (IsKeyword("while"))
        {
            return ParseWhileStatement();
        }

        if (IsKeyword("for"))
        {
            return ParseForStatement();
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

    private FunctionDeclarationSyntax ParseFunctionDeclaration()
    {
        ConsumeKeyword("fn");
        var nameToken = Consume(TokenKind.Identifier, "KES2001", "Expected a function name after fn.");
        Consume(TokenKind.OpenParen, "KES2001", "Expected '(' after function name.");
        var parameters = ParseParameterList();
        Consume(TokenKind.Colon, "KES2002", "Function declarations must include a body separator ':'.");

        IReadOnlyList<Token> returnTypeTokens = [];
        if (!Check(TokenKind.Newline))
        {
            returnTypeTokens = ReadUntil(TokenKind.Colon, TokenKind.Newline);
            if (returnTypeTokens.Count == 0 || !Match(TokenKind.Colon))
            {
                ThrowCurrent("KES2001", "Expected ':' after function return type.");
            }
        }

        var body = ParseStatementBlock("KES2004", "Function declarations must have an indented body.");
        return new FunctionDeclarationSyntax(
            nameToken.Lexeme,
            ToLocation(nameToken),
            parameters,
            returnTypeTokens,
            body);
    }

    private IReadOnlyList<ParameterSyntax> ParseParameterList()
    {
        var parameters = new List<ParameterSyntax>();
        if (Match(TokenKind.CloseParen))
        {
            return parameters;
        }

        while (!IsAtEnd())
        {
            var nameToken = Consume(TokenKind.Identifier, "KES2001", "Expected a parameter name.");
            Consume(TokenKind.Colon, "KES2001", "Expected ':' after parameter name.");
            var typeTokens = ReadUntil(TokenKind.Comma, TokenKind.CloseParen);
            if (typeTokens.Count == 0)
            {
                ThrowCurrent("KES2001", "Expected a parameter type.");
            }

            parameters.Add(new ParameterSyntax(nameToken.Lexeme, typeTokens, ToLocation(nameToken)));

            if (Match(TokenKind.Comma))
            {
                continue;
            }

            Consume(TokenKind.CloseParen, "KES2001", "Expected ')' after parameter list.");
            return parameters;
        }

        ThrowCurrent("KES2001", "Expected ')' after parameter list.");
        return parameters;
    }

    private ClassDeclarationSyntax ParseClassDeclaration()
    {
        ConsumeKeyword("class");
        var nameToken = Consume(TokenKind.Identifier, "KES2001", "Expected a class name after class.");
        Consume(TokenKind.Colon, "KES2002", "Class declarations must end with ':'.");
        ExpectIndentedBlock("KES2004", "Class declarations must have an indented body.");

        var members = new List<ClassMemberSyntax>();
        while (!IsAtEnd() && !Check(TokenKind.Dedent))
        {
            if (Match(TokenKind.Newline))
            {
                continue;
            }

            var accessModifier = TryConsumeAccessModifier();
            if (IsKeyword("var"))
            {
                members.Add(new ClassFieldSyntax(accessModifier, ParseVarStatement()));
                continue;
            }

            if (IsKeyword("fn"))
            {
                members.Add(new ClassMethodSyntax(accessModifier, ParseFunctionDeclaration()));
                continue;
            }

            ThrowCurrent("KES2001", "Class members must be var or fn declarations.");
        }

        Consume(TokenKind.Dedent, "KES2004", "Class declarations must end their body with a dedent.");
        if (members.Count == 0)
        {
            ThrowPrevious("KES2004", "Class declarations must contain at least one member.");
        }

        return new ClassDeclarationSyntax(nameToken.Lexeme, ToLocation(nameToken), members);
    }

    private EnumDeclarationSyntax ParseEnumDeclaration()
    {
        ConsumeKeyword("enum");
        var nameToken = Consume(TokenKind.Identifier, "KES2001", "Expected an enum name after enum.");
        Consume(TokenKind.Colon, "KES2002", "Enum declarations must end with ':'.");
        ExpectIndentedBlock("KES2004", "Enum declarations must have an indented body.");

        var members = new List<EnumMemberSyntax>();
        while (!IsAtEnd() && !Check(TokenKind.Dedent))
        {
            if (Match(TokenKind.Newline))
            {
                continue;
            }

            var memberToken = Consume(TokenKind.Identifier, "KES2001", "Enum members must be identifiers.");
            EnsureLineEndsNow("KES2001", "Enum members only support a single identifier.");
            members.Add(new EnumMemberSyntax(memberToken.Lexeme, ToLocation(memberToken)));
        }

        Consume(TokenKind.Dedent, "KES2004", "Enum declarations must end their body with a dedent.");
        if (members.Count == 0)
        {
            ThrowPrevious("KES2004", "Enum declarations must contain at least one member.");
        }

        return new EnumDeclarationSyntax(nameToken.Lexeme, ToLocation(nameToken), members);
    }

    private ActorDeclarationSyntax ParseActorDeclaration()
    {
        ConsumeKeyword("actor");
        var nameToken = Consume(TokenKind.Identifier, "KES2001", "Expected an actor name after actor.");
        Consume(TokenKind.Colon, "KES2002", "Actor declarations must end with ':'.");
        var body = ParseStatementBlock("KES2004", "Actor declarations must have an indented body.");
        return new ActorDeclarationSyntax(nameToken.Lexeme, ToLocation(nameToken), body);
    }

    private StandbyStatementSyntax ParseStandbyStatement()
    {
        var standbyToken = ConsumeKeyword("standby");
        Consume(TokenKind.Colon, "KES2002", "Standby statements must end with ':'.");
        ExpectIndentedBlock("KES2004", "Standby statements must have an indented body.");

        var entries = new List<StandbyEntrySyntax>();
        while (!IsAtEnd() && !Check(TokenKind.Dedent))
        {
            if (Match(TokenKind.Newline))
            {
                continue;
            }

            var instanceToken = Consume(TokenKind.Identifier, "KES2001", "Standby entries require an instance name.");
            Consume(TokenKind.Colon, "KES2001", "Standby entries require ':' between instance and actor type.");
            var actorTypeToken = Consume(TokenKind.Identifier, "KES2001", "Standby entries require an actor type name.");
            EnsureLineEndsNow("KES2001", "Standby entries only support '<identifier> : <actor_type>'.");
            entries.Add(new StandbyEntrySyntax(
                instanceToken.Lexeme,
                actorTypeToken.Lexeme,
                ToLocation(instanceToken),
                ToLocation(actorTypeToken)));
        }

        Consume(TokenKind.Dedent, "KES2004", "Standby statements must end their block with a dedent.");
        if (entries.Count == 0)
        {
            ThrowPrevious("KES2004", "Standby blocks must contain at least one entry.");
        }

        return new StandbyStatementSyntax(entries, ToLocation(standbyToken));
    }

    private string? TryConsumeAccessModifier()
    {
        if (IsKeyword("public") || IsKeyword("private"))
        {
            return Advance().Lexeme;
        }

        return null;
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
        return new SayStatementSyntax(speakerToken.Lexeme, tag, lines, tagToken is null ? null : ToLocation(tagToken), ToLocation(speakerToken));
    }

    private NarStatementSyntax ParseNarStatement()
    {
        var narToken = ConsumeKeyword("nar");
        var tagToken = Match(TokenKind.Tag) ? Previous : null;
        var tag = tagToken?.Lexeme;

        if (!Match(TokenKind.Colon))
        {
            ThrowCurrent("KES2002", "Nar statements must end with ':'.");
        }

        var lines = ParseTextBlock("KES2003", "Nar blocks must contain at least one text line.");
        return new NarStatementSyntax(tag, lines, tagToken is null ? null : ToLocation(tagToken), ToLocation(narToken));
    }

    private SelectStatementSyntax ParseSelectStatement()
    {
        var selectToken = ConsumeKeyword("select");
        var tagToken = Match(TokenKind.Tag) ? Previous : null;
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
            var caseTagToken = Consume(TokenKind.Tag, "KES2001", "Case statements require a jump target tag.");
            EnsureLineEndsNow("KES2001", "Case statements only support a string literal and a tag.");
            cases.Add(new CaseClauseSyntax(textToken.Lexeme, caseTagToken.Lexeme, ToLocation(caseTagToken)));
        }

        Consume(TokenKind.Dedent, "KES2004", "Select statements must end their block with a dedent.");

        if (cases.Count == 0)
        {
            ThrowPrevious("KES2004", "Select blocks must contain at least one case.");
        }

        return new SelectStatementSyntax(tagToken?.Lexeme, cases, tagToken is null ? null : ToLocation(tagToken), ToLocation(selectToken));
    }

    private IfStatementSyntax ParseIfStatement()
    {
        var ifToken = ConsumeKeyword("if");
        var conditionTokens = ReadUntil(TokenKind.Colon, TokenKind.Newline);
        if (conditionTokens.Count == 0)
        {
            ThrowCurrent("KES2001", "If statements require a condition.");
        }

        Consume(TokenKind.Colon, "KES2002", "If statements must end with ':'.");
        var body = ParseStatementBlock("KES2004", "If statements must have an indented body.");
        var elseIfClauses = new List<ElseIfClauseSyntax>();
        BlockSyntax? elseBody = null;

        while (IsKeyword("else"))
        {
            var elseToken = ConsumeKeyword("else");
            if (IsKeyword("if"))
            {
                ConsumeKeyword("if");
                var elseIfCondition = ReadUntil(TokenKind.Colon, TokenKind.Newline);
                if (elseIfCondition.Count == 0)
                {
                    ThrowCurrent("KES2001", "Else-if statements require a condition.");
                }

                Consume(TokenKind.Colon, "KES2002", "Else-if statements must end with ':'.");
                var elseIfBody = ParseStatementBlock("KES2004", "Else-if statements must have an indented body.");
                elseIfClauses.Add(new ElseIfClauseSyntax(elseIfCondition, elseIfBody, ToLocation(elseToken)));
                continue;
            }

            Consume(TokenKind.Colon, "KES2002", "Else statements must end with ':'.");
            elseBody = ParseStatementBlock("KES2004", "Else statements must have an indented body.");
            break;
        }

        return new IfStatementSyntax(conditionTokens, body, elseIfClauses, elseBody, ToLocation(ifToken));
    }

    private WhileStatementSyntax ParseWhileStatement()
    {
        var whileToken = ConsumeKeyword("while");
        var conditionTokens = ReadUntil(TokenKind.Colon, TokenKind.Newline);
        if (conditionTokens.Count == 0)
        {
            ThrowCurrent("KES2001", "While statements require a condition.");
        }

        Consume(TokenKind.Colon, "KES2002", "While statements must end with ':'.");
        var body = ParseStatementBlock("KES2004", "While statements must have an indented body.");
        return new WhileStatementSyntax(conditionTokens, body, ToLocation(whileToken));
    }

    private ForStatementSyntax ParseForStatement()
    {
        var forToken = ConsumeKeyword("for");
        var variableToken = ConsumeName("KES2001", "Expected a loop variable after for.");
        if (!IsKeyword("in"))
        {
            ThrowCurrent("KES2001", "For statements require 'in'.");
        }

        ConsumeKeyword("in");
        var iterableTokens = ReadUntil(TokenKind.Colon, TokenKind.Newline);
        if (iterableTokens.Count == 0)
        {
            ThrowCurrent("KES2001", "For statements require an iterable expression.");
        }

        Consume(TokenKind.Colon, "KES2002", "For statements must end with ':'.");
        var body = ParseStatementBlock("KES2004", "For statements must have an indented body.");
        return new ForStatementSyntax(variableToken.Lexeme, iterableTokens, body, ToLocation(variableToken), ToLocation(forToken));
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
        if (lineTokens.Count > 2 && lineTokens[1].Kind == TokenKind.Equals)
        {
            return new AssignmentStatementSyntax(nameToken.Lexeme, lineTokens.Skip(2).ToArray(), ToLocation(nameToken));
        }

        var assignmentIndex = FindArrayAssignmentEquals(lineTokens);
        if (assignmentIndex > 3)
        {
            return new AssignmentStatementSyntax(
                nameToken.Lexeme,
                lineTokens.Skip(assignmentIndex + 1).ToArray(),
                ToLocation(nameToken),
                lineTokens.Skip(2).Take(assignmentIndex - 3).ToArray());
        }

        if (lineTokens[^1].Kind == TokenKind.Colon)
        {
            var sharedArguments = lineTokens.Skip(1).Take(lineTokens.Count - 2).ToArray();
            var items = ParseLessBlock();
            return new LessStatementSyntax(nameToken.Lexeme, sharedArguments, items, ToLocation(nameToken));
        }

        var arguments = lineTokens.Skip(1).ToArray();
        return new CommandStatementSyntax(nameToken.Lexeme, arguments, ToLocation(nameToken));
    }

    private static int FindArrayAssignmentEquals(IReadOnlyList<Token> tokens)
    {
        if (tokens.Count < 6 || tokens[1].Kind != TokenKind.OpenBracket)
        {
            return -1;
        }

        var depth = 0;
        for (var index = 1; index < tokens.Count; index++)
        {
            switch (tokens[index].Kind)
            {
                case TokenKind.OpenBracket:
                    depth++;
                    break;
                case TokenKind.CloseBracket:
                    depth--;
                    if (depth == 0)
                    {
                        return index + 1 < tokens.Count && tokens[index + 1].Kind == TokenKind.Equals
                            ? index + 1
                            : -1;
                    }
                    break;
            }
        }

        return -1;
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
                items.Add(new LessNestedStatementSyntax(new LessStatementSyntax(nameToken.Lexeme, sharedArguments, nestedItems, ToLocation(nameToken))));
                continue;
            }

            items.Add(new LessCommandItemSyntax(itemTokens));
        }

        Consume(TokenKind.Dedent, "KES2004", "LESS statements must end their block with a dedent.");
        return items;
    }

    private BlockSyntax ParseStatementBlock(string errorCode, string message)
    {
        ExpectIndentedBlock(errorCode, message);

        var statements = new List<StatementSyntax>();
        while (!IsAtEnd() && !Check(TokenKind.Dedent))
        {
            if (Match(TokenKind.Newline))
            {
                continue;
            }

            statements.Add(ParseStatement());
            SkipNewlines();
        }

        Consume(TokenKind.Dedent, errorCode, "Block statements must end their body with a dedent.");
        return new BlockSyntax(statements);
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

    private Token ConsumeName(string errorCode, string message)
    {
        if (Current.Kind is not (TokenKind.Identifier or TokenKind.Keyword))
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
