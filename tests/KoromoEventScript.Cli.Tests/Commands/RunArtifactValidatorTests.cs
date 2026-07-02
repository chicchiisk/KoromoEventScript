using KoromoEventScript.Cli.Build;
using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.Commands.Run;

namespace KoromoEventScript.Cli.Tests.Commands;

public sealed class RunArtifactValidatorTests
{
    [Test]
    public void Validate_ReturnsSuccessWhenWindowsManifestAndKlibExist()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteFile(Path.Combine("build", "windows", "scripts", "main.klib"), "klib");
        var manifestPath = WriteManifest(
            fixture,
            "windows",
            [new BuildManifestScriptArtifact("events/main.kc", "scripts/main.klib", null, "events/main", "ja-JP", true, null)]);

        var result = new RunArtifactValidator().Validate(manifestPath);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.Success));
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.Manifest!.Target, Is.EqualTo("windows"));
            Assert.That(result.ResolvedKlibPaths.Single(), Is.EqualTo(Path.Combine(fixture.Root, "build", "windows", "scripts", "main.klib")));
        });
    }

    [Test]
    public void Validate_ReturnsFileDiagnosticWhenManifestIsMissing()
    {
        using var fixture = TemporaryProject.Create();
        var manifestPath = Path.Combine(fixture.Root, "build", "windows", "manifest.json");

        var result = new RunArtifactValidator().Validate(manifestPath);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.FileOrDirectoryError));
            Assert.That(result.Manifest, Is.Null);
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("KES9002"));
        });
    }

    [Test]
    public void Validate_ReturnsInvalidFormatDiagnosticWhenManifestTargetIsNotWindows()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteFile(Path.Combine("build", "windows", "scripts", "main.klib"), "klib");
        var manifestPath = WriteManifest(
            fixture,
            "unity",
            [new BuildManifestScriptArtifact("events/main.kc", "scripts/main.klib", null, "events/main", "ja-JP", true, null)]);

        var result = new RunArtifactValidator().Validate(manifestPath);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.FileOrDirectoryError));
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("KES9003"));
            Assert.That(result.Diagnostics.Single().Message, Does.Contain("windows"));
            Assert.That(result.ResolvedKlibPaths, Is.Empty);
        });
    }

    [Test]
    public void Validate_ReturnsFileDiagnosticWhenRequiredKlibIsMissing()
    {
        using var fixture = TemporaryProject.Create();
        var manifestPath = WriteManifest(
            fixture,
            "windows",
            [new BuildManifestScriptArtifact("events/main.kc", "scripts/missing.klib", null, "events/main", "ja-JP", true, null)]);

        var result = new RunArtifactValidator().Validate(manifestPath);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.FileOrDirectoryError));
            Assert.That(result.Manifest, Is.Not.Null);
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("KES9002"));
            Assert.That(result.Diagnostics.Single().Message, Does.Contain(".klib"));
        });
    }

    private static string WriteManifest(
        TemporaryProject fixture,
        string target,
        IReadOnlyList<BuildManifestScriptArtifact> scripts)
    {
        var manifestPath = Path.Combine(fixture.Root, "build", "windows", "manifest.json");
        var document = new BuildManifestDocument(
            "1.2.3",
            target,
            "events/main.kel",
            [new BuildManifestInputFile("events/main.kel", "entry")],
            scripts,
            []);
        var result = new BuildManifestWriter().Write(manifestPath, document);
        Assert.That(result.Succeeded, Is.True);
        return manifestPath;
    }
}
