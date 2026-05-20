using KoromoEventScript.Cli.Lexing;

namespace KoromoEventScript.Cli.Tests.Lexing;

public class KeLexerTests
{
    [Test]
    public void Lex_ProducesExpectedTokenStreamForValidScript()
    {
        const string source = """
label #start
// a comment
say Riku:
    "hello"
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
    public void Lex_ReportsUnterminatedString()
    {
        const string source = """
say Riku:
    "hello
""";

        var exception = Assert.Throws<LexerException>(() => KeLexer.Lex(source));
        Assert.Multiple(() =>
        {
            Assert.That(exception!.Diagnostic.Code, Is.EqualTo("KES1001"));
            Assert.That(exception.Diagnostic.Line, Is.EqualTo(2));
            Assert.That(exception.Diagnostic.Column, Is.EqualTo(5));
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
}
