using System.Text.Json;
using System.Text.RegularExpressions;
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
    public void Run_UnityTargetEmitsKsonManifest()
    {
        using var fixture = TemporaryProject.Create();
        using var output = new StringWriter();
        using var error = new StringWriter();
        CopyProject(GetTestDataPath("projects", "minimal"), fixture.Root);

        var exitCode = new CliApplication().Run(
            ["build", fixture.Root, "--target", "unity"],
            output,
            error,
            TestContext.CurrentContext.WorkDirectory);

        var unityRoot = Path.Combine(fixture.Root, "build", "unity");
        var manifestPath = Path.Combine(unityRoot, "manifest.kson");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.Success));
            Assert.That(error.ToString(), Is.Empty);
            Assert.That(File.Exists(manifestPath), Is.True);
            Assert.That(File.Exists(Path.Combine(unityRoot, "manifest.json")), Is.False);
            Assert.That(File.ReadAllText(manifestPath), Does.Contain("\"target\": \"unity\""));
            Assert.That(File.Exists(Path.Combine(unityRoot, "events", "chapter001.klib")), Is.True);
        });
    }

    [Test]
    public void Run_BuildsFullCommandSampleProject()
    {
        using var fixture = TemporaryProject.Create();
        using var output = new StringWriter();
        using var error = new StringWriter();
        CopyProject(GetTestDataPath("projects", "full-command-sample"), fixture.Root);

        var exitCode = new CliApplication().Run(
            ["build", fixture.Root, "--txt-il"],
            output,
            error,
            TestContext.CurrentContext.WorkDirectory);

        var root = Path.Combine(fixture.Root, "build", "windows");
        var chapter001Klib = Path.Combine(root, "events", "chapter001.klib");
        var chapter001Text = Path.Combine(root, "events", "chapter001.klibtxt");
        var chapter002Klib = Path.Combine(root, "events", "chapter002.klib");
        var chapter002Text = Path.Combine(root, "events", "chapter002.klibtxt");
        var actorAnimationKlib = Path.Combine(root, "events", "actor_animation_test.klib");
        var actorAnimationText = Path.Combine(root, "events", "actor_animation_test.klibtxt");
        var commonKlib = Path.Combine(root, "events", "lib", "Common.klib");
        var commonText = Path.Combine(root, "events", "lib", "Common.klibtxt");
        var diagnosticsPath = Path.Combine(root, "diagnostics.json");
        var manifestPath = Path.Combine(root, "manifest.json");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.Success));
            Assert.That(output.ToString(), Is.Empty);
            Assert.That(error.ToString(), Is.Empty);
            Assert.That(File.Exists(chapter001Klib), Is.True);
            Assert.That(File.Exists(chapter001Text), Is.True);
            Assert.That(File.Exists(chapter002Klib), Is.True);
            Assert.That(File.Exists(chapter002Text), Is.True);
            Assert.That(File.Exists(actorAnimationKlib), Is.True);
            Assert.That(File.Exists(actorAnimationText), Is.True);
            Assert.That(File.Exists(commonKlib), Is.True);
            Assert.That(File.Exists(commonText), Is.True);
            Assert.That(File.Exists(diagnosticsPath), Is.True);
            Assert.That(File.Exists(manifestPath), Is.True);
            Assert.That(File.ReadAllText(chapter001Text), Does.Contain("SELECT"));
            Assert.That(File.ReadAllText(chapter002Text), Does.Contain("SYSCALLVOID"));
            Assert.That(File.ReadAllText(actorAnimationText), Does.Contain("string \"action_jump\""));
            Assert.That(File.ReadAllText(commonText), Does.Contain("CALL"));
        });
    }

    [Test]
    public void FullCommandSampleSourcesCoverWalkthroughStlCommandsAndFlowSyntax()
    {
        var projectRoot = GetTestDataPath("projects", "full-command-sample");
        var source = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(Path.Combine(projectRoot, "events"), "*.kc", SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));
        var expectedCommands = new[]
        {
            "print", "array_len", "str_len", "range", "number_to_string", "bool_to_string", "assert",
            "rt_back", "rt_front", "bg", "trans", "camera_autofocus",
            "standby", "show", "hide", "face", "move", "action_jump",
            "vo", "vf", "p", "r", "l", "cm", "wait_click",
            "bgm", "bgm_stop", "se", "se_stop", "se_stop_all", "voice_stop",
            "save", "autosave", "mark_read", "is_read",
            "wait", "set_auto", "set_skip",
            "set_config_string", "set_config_number", "set_config_bool", "get_config",
            "set_param_string", "set_param_number", "set_param_bool", "get_param",
            "say", "nar", "label", "jump", "select", "case",
        };

        foreach (var command in expectedCommands)
        {
            Assert.That(
                Regex.IsMatch(source, $@"(?<![A-Za-z0-9_]){Regex.Escape(command)}(?![A-Za-z0-9_])"),
                Is.True,
                $"The full-command sample does not cover '{command}'.");
        }

        Assert.That(source, Does.Contain("vo \"assets.voice.voice_001_sample\""));
        Assert.That(source, Does.Match(@"(?m)^\s*vo\s*$"));
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
    var faceName: string = "normal"

standby:
    hero : Hero

say hero:
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
    var faceName: string = "normal"

standby:
    hero : Hero

say hero #sy_main_0001:
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
    var faceName: string = "normal"

standby:
    hero : Hero

say hero #sy_main_0001:
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
    var faceName: string = "normal"

standby:
    hero : Hero

say hero #sy_main_0001:
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
            Assert.That(manifest.RootElement.GetProperty("scripts").GetArrayLength(), Is.EqualTo(1));
            Assert.That(manifest.RootElement.GetProperty("scripts")[0].GetProperty("locale").GetString(), Is.EqualTo("en"));
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
    var faceName: string = "normal"

standby:
    hero : Hero

say hero #sy_main_0001:
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
