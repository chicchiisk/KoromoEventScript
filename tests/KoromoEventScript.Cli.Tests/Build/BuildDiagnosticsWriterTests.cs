using System.Text.Json;
using KoromoEventScript.Cli.Build;
using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Tests.Build;

public class BuildDiagnosticsWriterTests
{
    [Test]
    public void Write_PersistsDiagnosticsJson()
    {
        using var fixture = TemporaryProject.Create();
        var path = Path.Combine(fixture.Root, "build", "windows", "diagnostics.json");
        Diagnostic[] diagnostics =
        [
            new Diagnostic(DiagnosticLevel.Warning, "KES2001", "events/main.kc", 12, 3, "warning message"),
        ];

        var result = new BuildDiagnosticsWriter().Write(path, diagnostics);

        using var json = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(json.RootElement.GetArrayLength(), Is.EqualTo(1));
            Assert.That(json.RootElement[0].GetProperty("level").GetString(), Is.EqualTo("warning"));
            Assert.That(json.RootElement[0].GetProperty("code").GetString(), Is.EqualTo("KES2001"));
            Assert.That(json.RootElement[0].GetProperty("file").GetString(), Is.EqualTo("events/main.kc"));
        });
    }
}
