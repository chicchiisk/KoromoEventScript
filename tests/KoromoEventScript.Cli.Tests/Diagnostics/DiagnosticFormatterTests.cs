using System.Text.Json;
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
    public void FormatJsonLine_UsesJsonLinesFields()
    {
        var diagnostic = new Diagnostic(
            DiagnosticLevel.Error,
            "KES1001",
            "events/chapter001.ke",
            12,
            5,
            "未定義の識別子 'Noaa'");

        var json = DiagnosticFormatter.FormatJsonLine(diagnostic);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Multiple(() =>
        {
            Assert.That(root.GetProperty("level").GetString(), Is.EqualTo("error"));
            Assert.That(root.GetProperty("code").GetString(), Is.EqualTo("KES1001"));
            Assert.That(root.GetProperty("file").GetString(), Is.EqualTo("events/chapter001.ke"));
            Assert.That(root.GetProperty("line").GetInt32(), Is.EqualTo(12));
            Assert.That(root.GetProperty("column").GetInt32(), Is.EqualTo(5));
            Assert.That(root.GetProperty("message").GetString(), Is.EqualTo("未定義の識別子 'Noaa'"));
        });
    }

    [Test]
    public void Formatters_PreserveDiagnosticOrder()
    {
        Diagnostic[] diagnostics =
        [
            new(DiagnosticLevel.Error, "KES1001", "events/chapter001.ke", 12, 5, "first"),
            new(DiagnosticLevel.Warning, "KES4001", "events/chapter001.ke", 18, 3, "second"),
        ];

        var text = DiagnosticFormatter.FormatText(diagnostics).Split(Environment.NewLine);
        var jsonLines = DiagnosticFormatter.FormatJsonLines(diagnostics).Split(Environment.NewLine);

        Assert.Multiple(() =>
        {
            Assert.That(text, Is.EqualTo(
            [
                "events/chapter001.ke:12:5 error KES1001: first",
                "events/chapter001.ke:18:3 warning KES4001: second",
            ]));
            Assert.That(jsonLines, Has.Length.EqualTo(2));
            Assert.That(jsonLines[0], Does.Contain("\"code\":\"KES1001\""));
            Assert.That(jsonLines[1], Does.Contain("\"code\":\"KES4001\""));
        });
    }
}