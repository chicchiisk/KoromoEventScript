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

    [Test]
    public void Collect_ReturnsScopedMajorDefinitionsAndCompatibilitySymbols()
    {
        const string source = """
actor Riku:
    var faceName: string = "normal"

fn calc(base: number): number:
    var bonus = 1
    score bonus

class Counter:
    private var value: number = 0
    public fn add(amount: number): number:
        var next = value
        score next

enum Mood:
    normal
    smile
""";

        var document = new ScriptDocument("events/main.ke", "Main", KeParser.Parse(source));

        var result = new DefinitionCollector().Collect(document);
        var definitions = result.DefinitionTable.Definitions;

        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.DefinitionTable.Scopes.Select(static scope => scope.Kind),
                Does.Contain(ScopeKind.Module).And.Contain(ScopeKind.Class).And.Contain(ScopeKind.Function).And.Contain(ScopeKind.Method).And.Contain(ScopeKind.Block));
            Assert.That(definitions.Select(static definition => (definition.Name, definition.Kind)),
                Does.Contain(("Riku", DefinitionKind.Actor))
                    .And.Contain(("faceName", DefinitionKind.Variable))
                    .And.Contain(("calc", DefinitionKind.Function))
                    .And.Contain(("base", DefinitionKind.Parameter))
                    .And.Contain(("bonus", DefinitionKind.Variable))
                    .And.Contain(("Counter", DefinitionKind.Class))
                    .And.Contain(("value", DefinitionKind.ClassField))
                    .And.Contain(("add", DefinitionKind.ClassMethod))
                    .And.Contain(("amount", DefinitionKind.Parameter))
                    .And.Contain(("Mood", DefinitionKind.Enum))
                    .And.Contain(("normal", DefinitionKind.EnumMember)));
            Assert.That(result.Symbols.Select(static symbol => symbol.Name), Is.SupersetOf(["Riku", "calc", "Counter", "Mood"]));
        });
    }

    [Test]
    public void Collect_ReportsDuplicateMajorDefinitionsInModuleScope()
    {
        const string source = """
actor Riku:
    load Riku
fn Riku():
    load Riku
""";

        var document = new ScriptDocument("events/main.ke", "Main", KeParser.Parse(source));

        var result = new DefinitionCollector().Collect(document);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(result.Diagnostics[0].Code, Is.EqualTo("KES2009"));
            Assert.That(result.Diagnostics[0].File, Is.EqualTo("events/main.ke"));
            Assert.That(result.Diagnostics[0].Line, Is.EqualTo(3));
            Assert.That(result.Diagnostics[0].Column, Is.EqualTo(4));
            Assert.That(result.Diagnostics[0].Message, Does.Contain("Riku"));
        });
    }

    [Test]
    public void Collect_ReportsShadowingFromOuterScope()
    {
        const string source = """
var score = 0
fn calc(score: number):
    var local = score
""";

        var document = new ScriptDocument("events/main.ke", "Main", KeParser.Parse(source));

        var result = new DefinitionCollector().Collect(document);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(result.Diagnostics[0].Code, Is.EqualTo("KES2014"));
            Assert.That(result.Diagnostics[0].Line, Is.EqualTo(2));
            Assert.That(result.Diagnostics[0].Column, Is.EqualTo(9));
            Assert.That(result.Diagnostics[0].Message, Does.Contain("score"));
        });
    }

    [Test]
    public void Collect_AllowsSameMemberNameInDifferentClassScopes()
    {
        const string source = """
class First:
    var value: number = 0
class Second:
    var value: number = 0
""";

        var document = new ScriptDocument("events/main.ke", "Main", KeParser.Parse(source));

        var result = new DefinitionCollector().Collect(document);

        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.DefinitionTable.Definitions.Count(static definition => definition.Name == "value"), Is.EqualTo(2));
            Assert.That(result.DefinitionTable.Definitions.Where(static definition => definition.Name == "value").Select(static definition => definition.ScopeId).Distinct().ToArray(), Has.Length.EqualTo(2));
        });
    }
}
