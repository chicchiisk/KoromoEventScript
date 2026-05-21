using KoromoEventScript.Cli.Lexing;

namespace KoromoEventScript.Cli.Tests.Lexing;

public class KeLexerTests
{
    private static readonly string[] ReservedKeywords =
    [
        "import", "var", "fn", "class", "enum", "actor", "public", "private",
        "if", "else", "while", "for", "in", "break", "continue", "using",
        "as", "return", "new", "say", "nar", "select", "case", "label", "jump",
        "true", "false", "null",
    ];

    [Test]
    public void Lex_ProducesExpectedTokenStreamForValidScript()
    {
        const string source = """
label #start
// a comment
say Riku:
    hello
jump #end
/* block comment */
label #end
""";

        var result = KeLexer.Lex(source);
        var kinds = result.Tokens.Select(static token => token.Kind).ToArray();

        Assert.That(
            kinds,
            Is.EqualTo(
            [
                TokenKind.Keyword,
                TokenKind.Tag,
                TokenKind.Newline,
                TokenKind.Newline,
                TokenKind.Keyword,
                TokenKind.Identifier,
                TokenKind.Colon,
                TokenKind.Newline,
                TokenKind.Indent,
                TokenKind.StringLiteral,
                TokenKind.Newline,
                TokenKind.Dedent,
                TokenKind.Keyword,
                TokenKind.Tag,
                TokenKind.Newline,
                TokenKind.Newline,
                TokenKind.Keyword,
                TokenKind.Tag,
                TokenKind.Newline,
                TokenKind.EndOfFile,
            ]));
    }

    [Test]
    public void Lex_RecognizesAllReservedKeywords()
    {
        var source = string.Join(' ', ReservedKeywords);
        var tokens = KeLexer.Lex(source).Tokens;

        Assert.Multiple(() =>
        {
            Assert.That(tokens.Take(ReservedKeywords.Length).Select(static token => token.Kind),
                Is.EqualTo(Enumerable.Repeat(TokenKind.Keyword, ReservedKeywords.Length)));
            Assert.That(tokens.Take(ReservedKeywords.Length).Select(static token => token.Lexeme),
                Is.EqualTo(ReservedKeywords));
            Assert.That(tokens[^2].Kind, Is.EqualTo(TokenKind.Newline));
            Assert.That(tokens[^1].Kind, Is.EqualTo(TokenKind.EndOfFile));
        });
    }

    [Test]
    public void Lex_TokenizesOperatorsAndPunctuation()
    {
        const string source = "( ) [ ] , . : ; @ + - * / = == ! != < <= > >= && ||\n";

        AssertTokenSequence(
            source,
            [
                new(TokenKind.OpenParen, "("),
                new(TokenKind.CloseParen, ")"),
                new(TokenKind.OpenBracket, "["),
                new(TokenKind.CloseBracket, "]"),
                new(TokenKind.Comma, ","),
                new(TokenKind.Dot, "."),
                new(TokenKind.Colon, ":"),
                new(TokenKind.Semicolon, ";"),
                new(TokenKind.At, "@"),
                new(TokenKind.Plus, "+"),
                new(TokenKind.Minus, "-"),
                new(TokenKind.Star, "*"),
                new(TokenKind.Slash, "/"),
                new(TokenKind.Equals, "="),
                new(TokenKind.DoubleEquals, "=="),
                new(TokenKind.Bang, "!"),
                new(TokenKind.NotEquals, "!="),
                new(TokenKind.Less, "<"),
                new(TokenKind.LessOrEqual, "<="),
                new(TokenKind.Greater, ">"),
                new(TokenKind.GreaterOrEqual, ">="),
                new(TokenKind.AndAnd, "&&"),
                new(TokenKind.OrOr, "||"),
                new(TokenKind.Newline, "\n"),
                new(TokenKind.EndOfFile, string.Empty),
            ]);
    }

    [TestCaseSource(nameof(GetStatementCases))]
    public void Lex_TokenizesImplementedStatementForms(string source, ExpectedToken[] expectedTokens)
    {
        AssertTokenSequence(source, expectedTokens);
    }

    [Test]
    public void Lex_TreatsDoubleQuoteAsLiteralTextInsideSayBlock()
    {
        const string source = """
say Riku:
    "hello
""";

        var result = KeLexer.Lex(source);
        var textToken = result.Tokens.Single(static token => token.Kind == TokenKind.StringLiteral);

        Assert.Multiple(() =>
        {
            Assert.That(textToken.Lexeme, Is.EqualTo("\"hello"));
            Assert.That(textToken.Line, Is.EqualTo(2));
            Assert.That(textToken.Column, Is.EqualTo(5));
        });
    }

    [Test]
    public void Lex_ReportsUnterminatedStringOutsideSayBlock()
    {
        const string source = """
var message = "hello
""";

        var exception = Assert.Throws<LexerException>(() => KeLexer.Lex(source));
        Assert.Multiple(() =>
        {
            Assert.That(exception!.Diagnostic.Code, Is.EqualTo("KES1001"));
            Assert.That(exception.Diagnostic.Line, Is.EqualTo(1));
            Assert.That(exception.Diagnostic.Column, Is.EqualTo(15));
        });
    }

    [Test]
    public void Lex_ReportsUnterminatedBlockComment()
    {
        const string source = """
/* comment
label #start
""";

        var exception = Assert.Throws<LexerException>(() => KeLexer.Lex(source));
        Assert.Multiple(() =>
        {
            Assert.That(exception!.Diagnostic.Code, Is.EqualTo("KES1002"));
            Assert.That(exception.Diagnostic.Line, Is.EqualTo(1));
            Assert.That(exception.Diagnostic.Column, Is.EqualTo(1));
        });
    }

    [Test]
    public void Lex_PreservesIndentationAcrossInlineBlockComment()
    {
        const string source = """
if true:
    /* comment */ hp = 1
""";

        AssertTokenSequence(
            source,
            [
                new(TokenKind.Keyword, "if"),
                new(TokenKind.Keyword, "true"),
                new(TokenKind.Colon, ":"),
                new(TokenKind.Newline, "\n"),
                new(TokenKind.Indent, string.Empty),
                new(TokenKind.Identifier, "hp"),
                new(TokenKind.Equals, "="),
                new(TokenKind.NumberLiteral, "1"),
                new(TokenKind.Newline, "\n"),
                new(TokenKind.Dedent, string.Empty),
                new(TokenKind.EndOfFile, string.Empty),
            ]);
    }

    [Test]
    public void Lex_PreservesLocationAfterMultilineBlockComment()
    {
        const string source = """
/* comment
still comment */
label #start
""";

        var result = KeLexer.Lex(source);
        var keywordToken = result.Tokens.First(static token => token.Kind == TokenKind.Keyword);
        var tagToken = result.Tokens.First(static token => token.Kind == TokenKind.Tag);

        Assert.Multiple(() =>
        {
            Assert.That(keywordToken.Lexeme, Is.EqualTo("label"));
            Assert.That(keywordToken.Line, Is.EqualTo(3));
            Assert.That(keywordToken.Column, Is.EqualTo(1));
            Assert.That(tagToken.Line, Is.EqualTo(3));
            Assert.That(tagToken.Column, Is.EqualTo(7));
        });
    }

    [Test]
    public void Lex_ReportsTabIndentation()
    {
        var source = "say Riku:\n\t\"hello\"\n";

        var exception = Assert.Throws<LexerException>(() => KeLexer.Lex(source));
        Assert.Multiple(() =>
        {
            Assert.That(exception!.Diagnostic.Code, Is.EqualTo("KES1003"));
            Assert.That(exception.Diagnostic.Line, Is.EqualTo(2));
            Assert.That(exception.Diagnostic.Column, Is.EqualTo(1));
        });
    }

    [Test]
    public void Lex_ReportsUnexpectedCharacter()
    {
        const string source = "var hp = 1 ?\n";

        var exception = Assert.Throws<LexerException>(() => KeLexer.Lex(source));
        Assert.Multiple(() =>
        {
            Assert.That(exception!.Diagnostic.Code, Is.EqualTo("KES1004"));
            Assert.That(exception.Diagnostic.Line, Is.EqualTo(1));
            Assert.That(exception.Diagnostic.Column, Is.EqualTo(12));
        });
    }

    [Test]
    public void Lex_ReportsIndentationMismatch()
    {
        const string source = """
if true:
    hp = 1
  hp = 2
""";

        var exception = Assert.Throws<LexerException>(() => KeLexer.Lex(source));
        Assert.Multiple(() =>
        {
            Assert.That(exception!.Diagnostic.Code, Is.EqualTo("KES1005"));
            Assert.That(exception.Diagnostic.Line, Is.EqualTo(3));
            Assert.That(exception.Diagnostic.Column, Is.EqualTo(1));
        });
    }

    private static IEnumerable<TestCaseData> GetStatementCases()
    {
        yield return new TestCaseData(
            "import Common\n",
            new[]
            {
                new ExpectedToken(TokenKind.Keyword, "import"),
                new ExpectedToken(TokenKind.Identifier, "Common"),
                new ExpectedToken(TokenKind.Newline, "\n"),
                new ExpectedToken(TokenKind.EndOfFile, string.Empty),
            })
            .SetName("Lex_TokenizesImportStatement");

        yield return new TestCaseData(
            "var hp = 10\n",
            new[]
            {
                new ExpectedToken(TokenKind.Keyword, "var"),
                new ExpectedToken(TokenKind.Identifier, "hp"),
                new ExpectedToken(TokenKind.Equals, "="),
                new ExpectedToken(TokenKind.NumberLiteral, "10"),
                new ExpectedToken(TokenKind.Newline, "\n"),
                new ExpectedToken(TokenKind.EndOfFile, string.Empty),
            })
            .SetName("Lex_TokenizesVariableDeclaration");

        yield return new TestCaseData(
            """
fn Setup():
    return new Scene()
""",
            new[]
            {
                new ExpectedToken(TokenKind.Keyword, "fn"),
                new ExpectedToken(TokenKind.Identifier, "Setup"),
                new ExpectedToken(TokenKind.OpenParen, "("),
                new ExpectedToken(TokenKind.CloseParen, ")"),
                new ExpectedToken(TokenKind.Colon, ":"),
                new ExpectedToken(TokenKind.Newline, "\n"),
                new ExpectedToken(TokenKind.Indent, string.Empty),
                new ExpectedToken(TokenKind.Keyword, "return"),
                new ExpectedToken(TokenKind.Keyword, "new"),
                new ExpectedToken(TokenKind.Identifier, "Scene"),
                new ExpectedToken(TokenKind.OpenParen, "("),
                new ExpectedToken(TokenKind.CloseParen, ")"),
                new ExpectedToken(TokenKind.Newline, "\n"),
                new ExpectedToken(TokenKind.Dedent, string.Empty),
                new ExpectedToken(TokenKind.EndOfFile, string.Empty),
            })
            .SetName("Lex_TokenizesFunctionAndReturnNewStatement");

        yield return new TestCaseData(
            """
class Scene:
    public
    private
enum Route:
    Start
actor Riku
""",
            new[]
            {
                new ExpectedToken(TokenKind.Keyword, "class"),
                new ExpectedToken(TokenKind.Identifier, "Scene"),
                new ExpectedToken(TokenKind.Colon, ":"),
                new ExpectedToken(TokenKind.Newline, "\n"),
                new ExpectedToken(TokenKind.Indent, string.Empty),
                new ExpectedToken(TokenKind.Keyword, "public"),
                new ExpectedToken(TokenKind.Newline, "\n"),
                new ExpectedToken(TokenKind.Keyword, "private"),
                new ExpectedToken(TokenKind.Newline, "\n"),
                new ExpectedToken(TokenKind.Dedent, string.Empty),
                new ExpectedToken(TokenKind.Keyword, "enum"),
                new ExpectedToken(TokenKind.Identifier, "Route"),
                new ExpectedToken(TokenKind.Colon, ":"),
                new ExpectedToken(TokenKind.Newline, "\n"),
                new ExpectedToken(TokenKind.Indent, string.Empty),
                new ExpectedToken(TokenKind.Identifier, "Start"),
                new ExpectedToken(TokenKind.Newline, "\n"),
                new ExpectedToken(TokenKind.Dedent, string.Empty),
                new ExpectedToken(TokenKind.Keyword, "actor"),
                new ExpectedToken(TokenKind.Identifier, "Riku"),
                new ExpectedToken(TokenKind.Newline, "\n"),
                new ExpectedToken(TokenKind.EndOfFile, string.Empty),
            })
            .SetName("Lex_TokenizesTypeAndActorStatements");

        yield return new TestCaseData(
            """
if true:
    continue
else:
    break
""",
            new[]
            {
                new ExpectedToken(TokenKind.Keyword, "if"),
                new ExpectedToken(TokenKind.Keyword, "true"),
                new ExpectedToken(TokenKind.Colon, ":"),
                new ExpectedToken(TokenKind.Newline, "\n"),
                new ExpectedToken(TokenKind.Indent, string.Empty),
                new ExpectedToken(TokenKind.Keyword, "continue"),
                new ExpectedToken(TokenKind.Newline, "\n"),
                new ExpectedToken(TokenKind.Dedent, string.Empty),
                new ExpectedToken(TokenKind.Keyword, "else"),
                new ExpectedToken(TokenKind.Colon, ":"),
                new ExpectedToken(TokenKind.Newline, "\n"),
                new ExpectedToken(TokenKind.Indent, string.Empty),
                new ExpectedToken(TokenKind.Keyword, "break"),
                new ExpectedToken(TokenKind.Newline, "\n"),
                new ExpectedToken(TokenKind.Dedent, string.Empty),
                new ExpectedToken(TokenKind.EndOfFile, string.Empty),
            })
            .SetName("Lex_TokenizesIfElseControlFlowStatements");

        yield return new TestCaseData(
            """
while hp >= 1:
    hp = hp - 1
for item in items:
    break
""",
            new[]
            {
                new ExpectedToken(TokenKind.Keyword, "while"),
                new ExpectedToken(TokenKind.Identifier, "hp"),
                new ExpectedToken(TokenKind.GreaterOrEqual, ">="),
                new ExpectedToken(TokenKind.NumberLiteral, "1"),
                new ExpectedToken(TokenKind.Colon, ":"),
                new ExpectedToken(TokenKind.Newline, "\n"),
                new ExpectedToken(TokenKind.Indent, string.Empty),
                new ExpectedToken(TokenKind.Identifier, "hp"),
                new ExpectedToken(TokenKind.Equals, "="),
                new ExpectedToken(TokenKind.Identifier, "hp"),
                new ExpectedToken(TokenKind.Minus, "-"),
                new ExpectedToken(TokenKind.NumberLiteral, "1"),
                new ExpectedToken(TokenKind.Newline, "\n"),
                new ExpectedToken(TokenKind.Dedent, string.Empty),
                new ExpectedToken(TokenKind.Keyword, "for"),
                new ExpectedToken(TokenKind.Identifier, "item"),
                new ExpectedToken(TokenKind.Keyword, "in"),
                new ExpectedToken(TokenKind.Identifier, "items"),
                new ExpectedToken(TokenKind.Colon, ":"),
                new ExpectedToken(TokenKind.Newline, "\n"),
                new ExpectedToken(TokenKind.Indent, string.Empty),
                new ExpectedToken(TokenKind.Keyword, "break"),
                new ExpectedToken(TokenKind.Newline, "\n"),
                new ExpectedToken(TokenKind.Dedent, string.Empty),
                new ExpectedToken(TokenKind.EndOfFile, string.Empty),
            })
            .SetName("Lex_TokenizesLoopStatements");

        yield return new TestCaseData(
            """
using SceneLoader as loader:
    jump #exit
label #entry
""",
            new[]
            {
                new ExpectedToken(TokenKind.Keyword, "using"),
                new ExpectedToken(TokenKind.Identifier, "SceneLoader"),
                new ExpectedToken(TokenKind.Keyword, "as"),
                new ExpectedToken(TokenKind.Identifier, "loader"),
                new ExpectedToken(TokenKind.Colon, ":"),
                new ExpectedToken(TokenKind.Newline, "\n"),
                new ExpectedToken(TokenKind.Indent, string.Empty),
                new ExpectedToken(TokenKind.Keyword, "jump"),
                new ExpectedToken(TokenKind.Tag, "#exit"),
                new ExpectedToken(TokenKind.Newline, "\n"),
                new ExpectedToken(TokenKind.Dedent, string.Empty),
                new ExpectedToken(TokenKind.Keyword, "label"),
                new ExpectedToken(TokenKind.Tag, "#entry"),
                new ExpectedToken(TokenKind.Newline, "\n"),
                new ExpectedToken(TokenKind.EndOfFile, string.Empty),
            })
            .SetName("Lex_TokenizesUsingLabelAndJumpStatements");

        yield return new TestCaseData(
            """
say Riku:
    "hello
nar:
    plain text
""",
            new[]
            {
                new ExpectedToken(TokenKind.Keyword, "say"),
                new ExpectedToken(TokenKind.Identifier, "Riku"),
                new ExpectedToken(TokenKind.Colon, ":"),
                new ExpectedToken(TokenKind.Newline, "\n"),
                new ExpectedToken(TokenKind.Indent, string.Empty),
                new ExpectedToken(TokenKind.StringLiteral, "\"hello"),
                new ExpectedToken(TokenKind.Newline, "\n"),
                new ExpectedToken(TokenKind.Dedent, string.Empty),
                new ExpectedToken(TokenKind.Keyword, "nar"),
                new ExpectedToken(TokenKind.Colon, ":"),
                new ExpectedToken(TokenKind.Newline, "\n"),
                new ExpectedToken(TokenKind.Indent, string.Empty),
                new ExpectedToken(TokenKind.StringLiteral, "plain text"),
                new ExpectedToken(TokenKind.Newline, "\n"),
                new ExpectedToken(TokenKind.Dedent, string.Empty),
                new ExpectedToken(TokenKind.EndOfFile, string.Empty),
            })
            .SetName("Lex_TokenizesSayAndNarTextBlocks");

        yield return new TestCaseData(
            """
select:
    case "Go" #go
    case "Stay" #stay
""",
            new[]
            {
                new ExpectedToken(TokenKind.Keyword, "select"),
                new ExpectedToken(TokenKind.Colon, ":"),
                new ExpectedToken(TokenKind.Newline, "\n"),
                new ExpectedToken(TokenKind.Indent, string.Empty),
                new ExpectedToken(TokenKind.Keyword, "case"),
                new ExpectedToken(TokenKind.StringLiteral, "Go"),
                new ExpectedToken(TokenKind.Tag, "#go"),
                new ExpectedToken(TokenKind.Newline, "\n"),
                new ExpectedToken(TokenKind.Keyword, "case"),
                new ExpectedToken(TokenKind.StringLiteral, "Stay"),
                new ExpectedToken(TokenKind.Tag, "#stay"),
                new ExpectedToken(TokenKind.Newline, "\n"),
                new ExpectedToken(TokenKind.Dedent, string.Empty),
                new ExpectedToken(TokenKind.EndOfFile, string.Empty),
            })
            .SetName("Lex_TokenizesSelectAndCaseStatements");
    }

    private static void AssertTokenSequence(string source, ExpectedToken[] expectedTokens)
    {
        var actualTokens = KeLexer.Lex(source).Tokens;

        Assert.That(actualTokens.Select(static token => new ExpectedToken(token.Kind, token.Lexeme)),
            Is.EqualTo(expectedTokens));
    }

    public readonly record struct ExpectedToken(TokenKind Kind, string Lexeme);
}
