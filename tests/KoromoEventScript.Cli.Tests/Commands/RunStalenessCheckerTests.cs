using KoromoEventScript.Cli.Build;
using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.Commands.Run;
using KoromoEventScript.Cli.Diagnostics;
using KoromoEventScript.Cli.ProjectSystem;

namespace KoromoEventScript.Cli.Tests.Commands;

public sealed class RunStalenessCheckerTests
{
    [Test]
    public void Check_ReturnsFreshWhenArtifactsAreNewerThanInputs()
    {
        using var fixture = TemporaryProject.Create();
        var input = ArrangeProject(fixture);
        WriteKlib(fixture, "build/windows/scripts/main.klib");
        WriteManifest(fixture, "scripts/main.klib");
        SetInputTimes(fixture, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        SetArtifactTimes(fixture, new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero));

        var result = new RunStalenessChecker().Check(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.IsStale, Is.False);
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.Success));
            Assert.That(result.Diagnostics, Is.Empty);
        });
    }

    [Test]
    public void Check_ReturnsStaleWhenManifestIsMissing()
    {
        using var fixture = TemporaryProject.Create();
        var input = ArrangeProject(fixture);

        var result = new RunStalenessChecker().Check(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.IsStale, Is.True);
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.Success));
            Assert.That(result.Diagnostics, Is.Empty);
        });
    }

    [Test]
    public void Check_ReturnsStaleWhenRequiredKlibIsMissing()
    {
        using var fixture = TemporaryProject.Create();
        var input = ArrangeProject(fixture);
        WriteManifest(fixture, "scripts/missing.klib");

        var result = new RunStalenessChecker().Check(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.IsStale, Is.True);
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.Success));
            Assert.That(result.Diagnostics, Is.Empty);
        });
    }

    [Test]
    public void Check_ReturnsFileDiagnosticWhenManifestIsInvalid()
    {
        using var fixture = TemporaryProject.Create();
        var input = ArrangeProject(fixture);
        fixture.WriteFile("build/windows/manifest.json", "{ invalid");

        var result = new RunStalenessChecker().Check(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.IsStale, Is.False);
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.FileOrDirectoryError));
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("KES9003"));
        });
    }

    [TestCase("kes.xml")]
    [TestCase("events/main.kel")]
    [TestCase("events/chapter001.kc")]
    [TestCase("assets/image.png")]
    [TestCase("locale/messages.csv")]
    public void Check_ReturnsStaleWhenInputCandidateIsNewerThanOldestArtifact(string newerRelativePath)
    {
        using var fixture = TemporaryProject.Create();
        var input = ArrangeProject(fixture);
        WriteKlib(fixture, "build/windows/scripts/main.klib");
        WriteManifest(fixture, "scripts/main.klib");
        SetInputTimes(fixture, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        SetArtifactTimes(fixture, new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero));
        File.SetLastWriteTimeUtc(
            Path.Combine(fixture.Root, newerRelativePath),
            new DateTimeOffset(2026, 1, 3, 0, 0, 0, TimeSpan.Zero).UtcDateTime);

        var result = new RunStalenessChecker().Check(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.IsStale, Is.True);
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.Success));
            Assert.That(result.Diagnostics, Is.Empty);
        });
    }

    [Test]
    public void Check_ReturnsFileDiagnosticWhenInputTimestampCannotBeRead()
    {
        using var fixture = TemporaryProject.Create();
        var input = ArrangeProject(fixture);
        WriteKlib(fixture, "build/windows/scripts/main.klib");
        WriteManifest(fixture, "scripts/main.klib");
        var unreadablePath = Path.Combine(fixture.Root, "assets", "image.png");
        var fileSystem = new UnreadableTimestampFileSystem(unreadablePath);

        var result = new RunStalenessChecker(fileSystem: fileSystem).Check(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.IsStale, Is.False);
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.FileOrDirectoryError));
            Assert.That(result.Diagnostics.Single().Level, Is.EqualTo(DiagnosticLevel.Error));
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("KES9002"));
            Assert.That(result.Diagnostics.Single().File, Is.EqualTo(unreadablePath.Replace('\\', '/')));
        });
    }

    private static RunProjectInput ArrangeProject(TemporaryProject fixture)
    {
        fixture.WriteConfig();
        fixture.WriteFile("events/main.kel", "entry");
        fixture.WriteFile("events/chapter001.kc", "say \"hello\"");
        fixture.WriteFile("assets/image.png", "asset");
        fixture.WriteFile("locale/messages.csv", "key,ja");

        var config = new ProjectConfig(
            fixture.Root,
            "events/main.kel",
            "events",
            "assets",
            "locale",
            "build",
            "dist");
        return new RunProjectInput(
            fixture.Root,
            config,
            "events/main.kel",
            Path.Combine(fixture.Root, "events", "main.kel"),
            Path.Combine(fixture.Root, "build", "windows", "manifest.json"));
    }

    private static void WriteKlib(TemporaryProject fixture, string relativePath)
    {
        fixture.WriteFile(relativePath, "klib");
    }

    private static void WriteManifest(TemporaryProject fixture, string klibPath)
    {
        var manifestPath = Path.Combine(fixture.Root, "build", "windows", "manifest.json");
        var document = new BuildManifestDocument(
            "1.2.3",
            "windows",
            "events/main.kel",
            [new BuildManifestInputFile("events/main.kel", "entry")],
            [new BuildManifestScriptArtifact("events/chapter001.kc", klibPath, null, "events/chapter001", "ja-JP", true, null)],
            []);
        var result = new BuildManifestWriter().Write(manifestPath, document);
        Assert.That(result.Succeeded, Is.True);
    }

    private static void SetInputTimes(TemporaryProject fixture, DateTimeOffset timestamp)
    {
        File.SetLastWriteTimeUtc(Path.Combine(fixture.Root, "kes.xml"), timestamp.UtcDateTime);
        File.SetLastWriteTimeUtc(Path.Combine(fixture.Root, "events", "main.kel"), timestamp.UtcDateTime);
        File.SetLastWriteTimeUtc(Path.Combine(fixture.Root, "events", "chapter001.kc"), timestamp.UtcDateTime);
        File.SetLastWriteTimeUtc(Path.Combine(fixture.Root, "assets", "image.png"), timestamp.UtcDateTime);
        File.SetLastWriteTimeUtc(Path.Combine(fixture.Root, "locale", "messages.csv"), timestamp.UtcDateTime);
    }

    private static void SetArtifactTimes(TemporaryProject fixture, DateTimeOffset timestamp)
    {
        File.SetLastWriteTimeUtc(Path.Combine(fixture.Root, "build", "windows", "manifest.json"), timestamp.UtcDateTime);
        File.SetLastWriteTimeUtc(Path.Combine(fixture.Root, "build", "windows", "scripts", "main.klib"), timestamp.UtcDateTime);
    }

    private sealed class UnreadableTimestampFileSystem : RunStalenessFileSystem
    {
        private readonly string unreadablePath;

        public UnreadableTimestampFileSystem(string unreadablePath)
        {
            this.unreadablePath = Path.GetFullPath(unreadablePath);
        }

        public override DateTimeOffset GetLastWriteTimeUtc(string path)
        {
            if (string.Equals(Path.GetFullPath(path), unreadablePath, StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("simulated unreadable input");
            }

            return base.GetLastWriteTimeUtc(path);
        }
    }
}
