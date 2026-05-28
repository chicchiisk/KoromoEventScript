using KoromoEventScript.Cli.Diagnostics;
using KoromoEventScript.Cli.Parsing;
using KoromoEventScript.Cli.Semantics;

namespace KoromoEventScript.Cli.Tests.Semantics;

public class WarningAnalyzerTests
{
    [Test]
    public void Analyze_ReportsWarningForEmptyScriptDocument()
    {
        var document = new ScriptDocument("events/main.ke", "main", new ScriptSyntax([]));

        var result = new WarningAnalyzer().Analyze([document]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(result.Diagnostics[0].Level, Is.EqualTo(DiagnosticLevel.Warning));
            Assert.That(result.Diagnostics[0].Code, Is.EqualTo("KES4001"));
            Assert.That(result.Diagnostics[0].File, Is.EqualTo("events/main.ke"));
            Assert.That(result.Diagnostics[0].Line, Is.EqualTo(1));
            Assert.That(result.Diagnostics[0].Column, Is.EqualTo(1));
            Assert.That(result.Diagnostics[0].Message, Is.Not.Empty);
        });
    }

    [Test]
    public void Analyze_DoesNotReportWarningForNonEmptyScriptDocument()
    {
        var document = new ScriptDocument(
            "events/main.ke",
            "main",
            new ScriptSyntax([new VarStatementSyntax("score", [], [])]));

        var result = new WarningAnalyzer().Analyze([document]);

        Assert.That(result.Diagnostics, Is.Empty);
    }
}
