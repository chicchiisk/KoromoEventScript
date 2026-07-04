using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Tests.Diagnostics;

public class DiagnosticFormatterTests
{
    [Test]
    public void FormatText_UsesCliDiagnosticLayout()
    {
        var diagnostic = new Diagnostic(
            DiagnosticLevel.Error,
            "KES1001",
            "events/chapter001.ke",
            12,
            5,
            "未定義の識別子 'Noaa'");

        var text = DiagnosticFormatter.FormatText(diagnostic);

        Assert.That(text, Is.EqualTo("events/chapter001.ke:12:5 error KES1001: 未定義の識別子 'Noaa'"));
    }

    [Test]
    public void FormatText_PreservesDiagnosticOrder()
    {
        Diagnostic[] diagnostics =
        [
            new(DiagnosticLevel.Error, "KES1001", "events/chapter001.ke", 12, 5, "first"),
            new(DiagnosticLevel.Warning, "KES4001", "events/chapter001.ke", 18, 3, "second"),
        ];

        var text = DiagnosticFormatter.FormatText(diagnostics).Split(Environment.NewLine);

        Assert.That(text, Is.EqualTo(
        [
            "events/chapter001.ke:12:5 error KES1001: first",
            "events/chapter001.ke:18:3 warning KES4001: second",
        ]));
    }

    [Test]
    public void FormatText_IncludesRelatedLocationWhenPresent()
    {
        var diagnostic = new Diagnostic(
            DiagnosticLevel.Error,
            "KES2009",
            "events/main.ke",
            3,
            5,
            "Duplicate definition 'score'.",
            [
                new DiagnosticRelatedLocation(
                    "events/main.ke",
                    1,
                    5,
                    "Original definition is here.")
            ]);

        var text = DiagnosticFormatter.FormatText(diagnostic);

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.StartWith("events/main.ke:3:5 error KES2009: Duplicate definition 'score'."));
            Assert.That(text, Does.Contain("events/main.ke:1:5"));
            Assert.That(text, Does.Contain("Original definition is here."));
        });
    }

}
