using KoromoEventScript.Cli.Parsing;

namespace KoromoEventScript.Cli.Tests.Parsing;

public class KelParserTests
{
    [Test]
    public void Parse_BuildsSyntaxTreeForMinimalEventList()
    {
        var source = ReadTestDataFile("kel", "valid", "main.kel");

        var syntax = KelParser.Parse(source);

        Assert.Multiple(() =>
        {
            Assert.That(syntax.EntryEventId, Is.EqualTo("chapter001"));
            Assert.That(syntax.Events, Has.Count.EqualTo(1));
            Assert.That(syntax.Events[0], Is.EqualTo(new EventDeclarationSyntax("chapter001", "events/chapter001.kc", "#start")));
        });
    }

    [Test]
    public void Parse_ReportsMissingEntryStatementAtTopOfFile()
    {
        const string source = "event \"chapter001\" \"events/chapter001.kc\" #start\n";

        var exception = Assert.Throws<ParserException>(() => KelParser.Parse(source));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Diagnostic.Code, Is.EqualTo("KES2001"));
            Assert.That(exception.Diagnostic.Line, Is.EqualTo(1));
            Assert.That(exception.Diagnostic.Column, Is.EqualTo(1));
        });
    }

    [Test]
    public void Parse_ReportsMissingEntryTagOnEventStatement()
    {
        const string source = "entry \"chapter001\"\nevent \"chapter001\" \"events/chapter001.kc\"\n";

        var exception = Assert.Throws<ParserException>(() => KelParser.Parse(source));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Diagnostic.Code, Is.EqualTo("KES2001"));
            Assert.That(exception.Diagnostic.Line, Is.EqualTo(2));
            Assert.That(exception.Diagnostic.Column, Is.EqualTo(42));
        });
    }

    private static string ReadTestDataFile(params string[] relativePathSegments)
    {
        var path = Path.Combine(GetRepositoryRoot(), "testdata", Path.Combine(relativePathSegments));
        return File.ReadAllText(path);
    }

    private static string GetRepositoryRoot()
    {
        return Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..",
            "..",
            "..",
            "..",
            ".."));
    }
}