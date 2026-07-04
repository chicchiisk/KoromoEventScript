using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Tests.Diagnostics;

public class DiagnosticSinkTests
{
    [Test]
    public void Write_UsesTextFormatAndPreservesOrder()
    {
        Diagnostic[] diagnostics =
        [
            new(DiagnosticLevel.Error, "KES9001", "", 1, 1, "first"),
            new(DiagnosticLevel.Warning, "KES4001", "events/chapter001.kc", 2, 3, "second"),
        ];
        using var writer = new StringWriter();

        new DiagnosticSink().Write(diagnostics, DiagnosticOutputFormat.Text, writer);

        Assert.That(writer.ToString(), Is.EqualTo(string.Join(Environment.NewLine,
        [
            "<unknown>:1:1 error KES9001: first",
            "events/chapter001.kc:2:3 warning KES4001: second",
        ]) + Environment.NewLine));
    }

    [Test]
    public void Write_EmitsNothingForEmptyDiagnostics()
    {
        using var writer = new StringWriter();

        new DiagnosticSink().Write([], DiagnosticOutputFormat.Text, writer);

        Assert.That(writer.ToString(), Is.Empty);
    }
}
