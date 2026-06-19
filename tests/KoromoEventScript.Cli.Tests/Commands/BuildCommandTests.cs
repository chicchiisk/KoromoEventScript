using System.Text.Json;
using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.Commands.Build;

namespace KoromoEventScript.Cli.Tests.Commands;

public class BuildCommandTests
{
    [Test]
    public void Run_EmitsKlibArtifactsForNonCheckOnlyBuild()
    {
        using var fixture = TemporaryProject.Create();
        using var output = new StringWriter();
        using var error = new StringWriter();
        CopyProject(GetTestDataPath("projects", "minimal"), fixture.Root);

        var exitCode = new CliApplication().Run(
            ["build", fixture.Root, "--txt-il"],
            output,
            error,
            TestContext.CurrentContext.WorkDirectory);

        var klibPath = Path.Combine(fixture.Root, "build", "windows", "events", "chapter001.klib");
        var klibtxtPath = Path.Combine(fixture.Root, "build", "windows", "events", "chapter001.klibtxt");
        var diagnosticsPath = Path.Combine(fixture.Root, "build", "windows", "diagnostics.json");
        var manifestPath = Path.Combine(fixture.Root, "build", "windows", "manifest.json");
        var magic = File.ReadAllBytes(klibPath).Take(4).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.Success));
            Assert.That(output.ToString(), Is.Empty);
            Assert.That(error.ToString(), Is.Empty);
            Assert.That(File.Exists(klibPath), Is.True);
            Assert.That(File.Exists(klibtxtPath), Is.True);
            Assert.That(File.Exists(diagnosticsPath), Is.True);
            Assert.That(File.Exists(manifestPath), Is.True);
            Assert.That(magic, Is.EqualTo(new byte[] { 0x4B, 0x4C, 0x49, 0x42 }));
            Assert.That(File.ReadAllText(klibtxtPath), Does.Contain("SYSCALLVOID"));
            Assert.That(File.ReadAllText(klibtxtPath), Does.Contain("SELECT"));
            Assert.That(File.ReadAllText(klibtxtPath), Does.Contain("JUMP"));
        });
    }

    [Test]
    public void Run_RewritesMissingLocalizationTagsBeforeCompiling()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteConfig(entry: "events/main.kel");
        fixture.WriteFile("events/main.kel", """
entry = intro
intro = {
    chapter = "events/main.kc"
}
""");
        fixture.WriteFile("events/main.kc", """
actor Hero:
    cast Hero

say Hero:
    hello
nar:
    world
select:
    case "Go" #go
label #go
""");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = new CliApplication().Run(
            ["build", fixture.Root, "--txt-il"],
            output,
            error,
            TestContext.CurrentContext.WorkDirectory);

        var scriptPath = Path.Combine(fixture.Root, "events", "main.kc");
        var klibtxtPath = Path.Combine(fixture.Root, "build", "windows", "events", "main.klibtxt");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.Success));
            Assert.That(error.ToString(), Is.Empty);
            Assert.That(File.ReadAllText(scriptPath).Replace("\r\n", "\n"), Is.EqualTo("""
actor Hero:
    cast Hero

say Hero #sy_main_0001:
    hello
nar #na_main_0002:
    world
select #se_main_0003:
    case "Go" #go
label #go
"""));
            Assert.That(File.Exists(klibtxtPath), Is.True);
            Assert.That(File.ReadAllText(klibtxtPath), Does.Contain("SYSCALLVOID"));
        });
    }

    [Test]
    public void Run_EmitsLocalizedArtifactsWhenLocaleIsSpecified()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteConfig(entry: "events/main.kel");
        fixture.WriteFile("events/main.kel", """
entry = intro
intro = {
    chapter = "events/main.kc"
}
""");
        fixture.WriteFile("events/main.kc", """
actor Hero:
    cast Hero

say Hero #sy_main_0001:
    hello
nar #na_main_0002:
    world
select #se_main_0003:
    case "Go" #next
label #next
""");
        fixture.WriteFile("localization.csv", """
tag,say,original,en
sy_main_0001,Hero,hello,Hello
na_main_0002,,world,World
se_main_0003_c00,,Go,Continue
""");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = new CliApplication().Run(
            ["build", fixture.Root, "--loc", "en", "--txt-il"],
            output,
            error,
            TestContext.CurrentContext.WorkDirectory);

        var klibPath = Path.Combine(fixture.Root, "build", "windows", "events", "loc", "en", "main.klib");
        var klibtxtPath = Path.Combine(fixture.Root, "build", "windows", "events", "loc", "en", "main.klibtxt");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.Success));
            Assert.That(output.ToString(), Is.Empty);
            Assert.That(error.ToString(), Is.Empty);
            Assert.That(File.Exists(klibPath), Is.True);
            Assert.That(File.Exists(klibtxtPath), Is.True);
            Assert.That(File.ReadAllText(klibtxtPath), Does.Contain("Hello"));
            Assert.That(File.ReadAllText(klibtxtPath), Does.Contain("World"));
            Assert.That(File.ReadAllText(klibtxtPath), Does.Contain("Continue"));
        });
    }

    [Test]
    public void Run_UsesOutDirForArtifactsAndMetadata()
    {
        using var fixture = TemporaryProject.Create();
        using var output = new StringWriter();
        using var error = new StringWriter();
        CopyProject(GetTestDataPath("projects", "minimal"), fixture.Root);

        var exitCode = new CliApplication().Run(
            ["build", fixture.Root, "--out-dir", "custom-build", "--txt-il"],
            output,
            error,
            TestContext.CurrentContext.WorkDirectory);

        var root = Path.Combine(fixture.Root, "custom-build", "windows");
        var klibPath = Path.Combine(root, "events", "chapter001.klib");
        var klibtxtPath = Path.Combine(root, "events", "chapter001.klibtxt");
        var diagnosticsPath = Path.Combine(root, "diagnostics.json");
        var manifestPath = Path.Combine(root, "manifest.json");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.Success));
            Assert.That(error.ToString(), Is.Empty);
            Assert.That(File.Exists(klibPath), Is.True);
            Assert.That(File.Exists(klibtxtPath), Is.True);
            Assert.That(File.Exists(diagnosticsPath), Is.True);
            Assert.That(File.Exists(manifestPath), Is.True);
        });
    }

    [Test]
    public void Run_WritesManifestAndDiagnosticsForLocalizedBuild()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteConfig(entry: "events/main.kel");
        fixture.WriteFile("events/main.kel", """
entry = intro
intro = {
    chapter = "events/main.kc"
}
""");
        fixture.WriteFile("events/main.kc", """
actor Hero:
    cast Hero

say Hero #sy_main_0001:
    hello
""");
        fixture.WriteFile("localization.csv", """
tag,say,original,en
sy_main_0001,Hero,hello,Hello
""");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = new CliApplication().Run(
            ["build", fixture.Root, "--loc", "en"],
            output,
            error,
            TestContext.CurrentContext.WorkDirectory);

        var root = Path.Combine(fixture.Root, "build", "windows");
        var diagnosticsPath = Path.Combine(root, "diagnostics.json");
        var manifestPath = Path.Combine(root, "manifest.json");

        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        using var diagnostics = JsonDocument.Parse(File.ReadAllText(diagnosticsPath));
        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.Success));
            Assert.That(File.Exists(Path.Combine(root, "events", "loc", "en", "main.klib")), Is.True);
            Assert.That(manifest.RootElement.GetProperty("scripts").GetArrayLength(), Is.EqualTo(0));
            Assert.That(manifest.RootElement.GetProperty("localizations")[0].GetProperty("locale").GetString(), Is.EqualTo("en"));
            Assert.That(diagnostics.RootElement.GetArrayLength(), Is.EqualTo(0));
        });
    }

    [Test]
    public void Run_FailsWhenLocaleDictionaryIsMissing()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteConfig(entry: "events/main.kel");
        fixture.WriteFile("events/main.kel", """
entry = intro
intro = {
    chapter = "events/main.kc"
}
""");
        fixture.WriteFile("events/main.kc", """
actor Hero:
    cast Hero

say Hero #sy_main_0001:
    hello
""");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = new CliApplication().Run(
            ["build", fixture.Root, "--loc", "en"],
            output,
            error,
            TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.FileOrDirectoryError));
            Assert.That(File.Exists(Path.Combine(fixture.Root, "build", "windows", "events", "loc", "en", "main.klib")), Is.False);
            Assert.That(File.Exists(Path.Combine(fixture.Root, "build", "windows", "manifest.json")), Is.False);
            Assert.That(error.ToString(), Does.Contain("KES9004"));
        });
    }

    private static string GetTestDataPath(params string[] segments)
    {
        return Path.GetFullPath(Path.Combine(GetRepositoryRoot(), "testdata", Path.Combine(segments)));
    }

    private static void CopyProject(string sourceRoot, string destinationRoot)
    {
        foreach (var directory in Directory.EnumerateDirectories(sourceRoot, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destinationRoot, Path.GetRelativePath(sourceRoot, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(destinationRoot, Path.GetRelativePath(sourceRoot, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }

    private static string GetRepositoryRoot()
    {
        return Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", ".."));
    }
}
