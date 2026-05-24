using KoromoEventScript.Cli.Build;

namespace KoromoEventScript.Cli.Tests.Build;

public class SourceFileParserTests
{
    [Test]
    public void ParseKel_ReturnsSyntaxForValidFile()
    {
        var path = GetTestDataPath("kel", "valid", "main.kel");

        var result = new SourceFileParser().ParseKel(path, "events/main.kel");

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(SourceParseStatus.Success));
            Assert.That(result.Syntax, Is.Not.Null);
            Assert.That(result.Diagnostic, Is.Null);
        });
    }

    [Test]
    public void ParseKe_ReturnsFileDiagnosticForMissingFile()
    {
        var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "missing.kc");

        var result = new SourceFileParser().ParseKe(path, "events/missing.kc");

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(SourceParseStatus.FileError));
            Assert.That(result.Diagnostic!.Code, Is.EqualTo("KES9004"));
            Assert.That(result.Diagnostic.File, Is.EqualTo("events/missing.kc"));
        });
    }

    [Test]
    public void ParseKe_MapsParserDiagnosticsToSyntaxStageDiagnostic()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteFile("events/invalid.kc", """
say Riku
    missing colon
""");
        var path = Path.Combine(fixture.Root, "events", "invalid.kc");

        var result = new SourceFileParser().ParseKe(path, "events/invalid.kc");

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(SourceParseStatus.SyntaxError));
            Assert.That(result.Diagnostic!.Code, Does.StartWith("KES1"));
            Assert.That(result.Diagnostic.File, Is.EqualTo("events/invalid.kc"));
        });
    }

    private static string GetTestDataPath(params string[] segments)
    {
        return Path.GetFullPath(Path.Combine(GetRepositoryRoot(), "testdata", Path.Combine(segments)));
    }

    private static string GetRepositoryRoot()
    {
        return Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", ".."));
    }
}
