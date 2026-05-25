using KoromoEventScript.Cli.Diagnostics;
using KoromoEventScript.Cli.Parsing;
using KoromoEventScript.Cli.Semantics;

namespace KoromoEventScript.Cli.Tests.Semantics;

public class DefinitionCollectorTests
{
    [Test]
    public void Collect_ReturnsCurrentTopLevelDefinitionsWithParserLocations()
    {
        const string source = """
show Noa 0

var sharedValue: number = 1
label #start
say Riku #sayTag:
    hello
nar #narTag:
    world
jump #start
""";

        var document = new ScriptDocument(
            "events/main.ke",
            "Main",
            KeParser.Parse(source));

        var result = new DefinitionCollector().Collect(document);

        var locations = result.Symbols
            .Select(static symbol => (symbol.Name, symbol.Line, symbol.Column))
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(result.Document, Is.SameAs(document));
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(
                result.Symbols.Select(static symbol => symbol.Name),
                Is.EqualTo(["sharedValue", "#start", "#sayTag", "#narTag"]));
            Assert.That(result.Symbols.Select(static symbol => symbol.ModuleName), Is.All.EqualTo("Main"));
            Assert.That(result.Symbols.Select(static symbol => symbol.File), Is.All.EqualTo("events/main.ke"));
            Assert.That(
                locations,
                Is.EqualTo(new[]
                {
                    ("sharedValue", 3, 5),
                    ("#start", 4, 7),
                    ("#sayTag", 5, 10),
                    ("#narTag", 7, 5),
                }));
        });
    }

    [Test]
    public void Collect_IgnoresSayAndNarStatementsWithoutTags()
    {
        var document = new ScriptDocument(
            "events/main.ke",
            "Main",
            new ScriptSyntax(
            [
                new SayStatementSyntax("actor", null, [new TextLineSyntax("hello", false)]),
                new NarStatementSyntax(null, [new TextLineSyntax("world", false)]),
            ]));

        var result = new DefinitionCollector().Collect(document);

        Assert.Multiple(() =>
        {
            Assert.That(result.Symbols, Is.Empty);
            Assert.That(result.Diagnostics, Is.Empty);
        });
    }

    [Test]
    public void Collect_ReportsDuplicateDefinitionsWithinModuleAsCompileDiagnostics()
    {
        const string source = """
var sharedValue = 1
var otherValue = 2
var sharedValue = 3
""";

        var document = new ScriptDocument(
            "events/main.ke",
            "Main",
            KeParser.Parse(source));

        var result = new DefinitionCollector().Collect(document);

        Assert.Multiple(() =>
        {
            Assert.That(result.Symbols.Select(static symbol => symbol.Name), Is.EqualTo(["sharedValue", "otherValue", "sharedValue"]));
            Assert.That(result.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(result.Diagnostics[0].Level, Is.EqualTo(DiagnosticLevel.Error));
            Assert.That(result.Diagnostics[0].Code, Is.EqualTo("KES2009"));
            Assert.That(result.Diagnostics[0].File, Is.EqualTo("events/main.ke"));
            Assert.That(result.Diagnostics[0].Line, Is.EqualTo(3));
            Assert.That(result.Diagnostics[0].Column, Is.EqualTo(5));
            Assert.That(result.Diagnostics[0].Message, Does.Contain("sharedValue"));
            Assert.That(result.Diagnostics[0].Message, Does.Contain("Main"));
        });
    }

    [Test]
    public void Collect_TreatsDefinitionNamesAsCaseSensitive()
    {
        var document = new ScriptDocument(
            "events/main.ke",
            "Main",
            new ScriptSyntax(
            [
                new VarStatementSyntax("sharedValue", [], []),
                new VarStatementSyntax("SharedValue", [], []),
            ]));

        var result = new DefinitionCollector().Collect(document);

        Assert.Multiple(() =>
        {
            Assert.That(result.Symbols.Select(static symbol => symbol.Name), Is.EqualTo(["sharedValue", "SharedValue"]));
            Assert.That(result.Diagnostics, Is.Empty);
        });
    }
}
