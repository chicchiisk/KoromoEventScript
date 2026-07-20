using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.Parsing;
using KoromoEventScript.Cli.Semantics;

namespace KoromoEventScript.Cli.Tests.Semantics;

public class TypeCheckerTests
{
    [Test]
    public void CheckTypes_ReportsVariableExpressionArrayAndCommandMismatches()
    {
        const string source = """
actor Noa:
    var faceName: string = "normal"
standby:
    noa : Noa
var name: string = 1
var score: number = 1 + "bad"
var mixed: number[] = [1, "two"]
if score:
    print 1
show "Noa" 0
""";

        var result = Check(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.CompileError));
            Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Code), Is.All.EqualTo("KES2015"));
            Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Message),
                Has.Some.Contains("string")
                    .And.Some.Contains("Arithmetic")
                    .And.Some.Contains("Array elements")
                    .And.Some.Contains("If condition")
                    .And.Some.Contains("print")
                    .And.Some.Contains("show"));
        });
    }

    [Test]
    public void CheckTypes_AllowsValidMvpTypesAndLoopElementInference()
    {
        const string source = """
actor Noa:
    var faceName: string = "normal"
standby:
    noa : Noa
var actors: Actor[] = [noa]
var enabled = true
var score: number = 1 + 2
if enabled:
    print (number_to_string score)
for actor in actors:
    show actor 0
""";

        var result = Check(source);

        Assert.That(result.Diagnostics, Is.Empty);
    }

    [Test]
    public void CheckTypes_ValidatesArrayElementAssignment()
    {
        const string source = """
var values: number[] = [1, 2]
var index: number = 0
values[index] = 3
values[true] = 4
values[0] = "bad"
index[0] = 1
""";

        var result = Check(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Message),
                Has.Some.Contains("Array index")
                    .And.Some.Contains("element")
                    .And.Some.Contains("must be an array"));
        });
    }

    [Test]
    public void CheckTypes_ChecksUserDefinedFunctionArguments()
    {
        const string source = """
fn format(value: number): string:
    print (number_to_string value)
var text = format true
""";

        var result = Check(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Diagnostics.Single().Message, Does.Contain("format"));
            Assert.That(result.Diagnostics.Single().Message, Does.Contain("number"));
            Assert.That(result.Diagnostics.Single().Message, Does.Contain("bool"));
        });
    }

    private static TypeCheckingResult Check(string source)
    {
        var document = new ScriptDocument("events/main.ke", "Main", KeParser.Parse(source));
        var collection = new DefinitionCollector().Collect(document);
        Assert.That(collection.Diagnostics, Is.Empty);
        var graph = new ImportGraph([document], new Dictionary<string, IReadOnlyList<string>>
        {
            ["Main"] = [],
        });
        var nameResult = new NameResolver().ResolveNames(graph, [collection]);
        Assert.That(nameResult.Diagnostics, Is.Empty);
        return new TypeChecker().CheckTypes(graph, [collection]);
    }
}
