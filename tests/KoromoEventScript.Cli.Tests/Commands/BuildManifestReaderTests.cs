using KoromoEventScript.Cli.Build;
using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.Commands.Run;
using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Tests.Commands;

public sealed class BuildManifestReaderTests
{
    [Test]
    public void Read_ReadsManifestWrittenByBuildManifestWriter()
    {
        using var fixture = TemporaryProject.Create();
        var manifestPath = Path.Combine(fixture.Root, "build", "windows", "manifest.json");
        var document = new BuildManifestDocument(
            "1.2.3",
            "windows",
            "events/main.kel",
            [new BuildManifestInputFile("events/main.kel", "entry")],
            [new BuildManifestScriptArtifact("events/main.kc", "scripts/main.klib", null, "events/main", "ja-JP", true, "start")],
            []);
        var writeResult = new BuildManifestWriter().Write(manifestPath, document);

        var result = new BuildManifestReader().Read(manifestPath);

        Assert.Multiple(() =>
        {
            Assert.That(writeResult.Succeeded, Is.True);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.Document!.Target, Is.EqualTo("windows"));
            Assert.That(result.Document.Scripts, Has.Count.EqualTo(1));
            Assert.That(result.Document.Scripts[0].KlibPath, Is.EqualTo("scripts/main.klib"));
        });
    }

    [Test]
    public void Read_ReturnsFileDiagnosticWhenManifestIsMissing()
    {
        using var fixture = TemporaryProject.Create();
        var manifestPath = Path.Combine(fixture.Root, "build", "windows", "manifest.json");

        var result = new BuildManifestReader().Read(manifestPath);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Document, Is.Null);
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("KES9002"));
            Assert.That(result.Diagnostics.Single().Level, Is.EqualTo(DiagnosticLevel.Error));
            Assert.That(result.Diagnostics.Single().Message, Does.Contain("manifest.json"));
        });
    }

    [Test]
    public void Read_ReturnsInvalidFormatDiagnosticWhenJsonIsInvalid()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteFile(Path.Combine("build", "windows", "manifest.json"), "{ invalid");
        var manifestPath = Path.Combine(fixture.Root, "build", "windows", "manifest.json");

        var result = new BuildManifestReader().Read(manifestPath);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Document, Is.Null);
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("KES9003"));
            Assert.That(result.Diagnostics.Single().Message, Does.Contain("Invalid manifest.json"));
        });
    }

    [Test]
    public void Read_ReturnsInvalidFormatDiagnosticWhenRequiredFieldsAreMissing()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteFile(Path.Combine("build", "windows", "manifest.json"), """{"target":"windows","scripts":[{}]}""");
        var manifestPath = Path.Combine(fixture.Root, "build", "windows", "manifest.json");

        var result = new BuildManifestReader().Read(manifestPath);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Document, Is.Null);
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("KES9003"));
            Assert.That(result.Diagnostics.Single().Message, Does.Contain("scripts[].klibPath"));
        });
    }
}
