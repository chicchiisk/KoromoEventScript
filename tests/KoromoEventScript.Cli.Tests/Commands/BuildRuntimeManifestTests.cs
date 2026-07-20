using System.Text.Json;
using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Runtime.Core.Manifests;

namespace KoromoEventScript.Cli.Tests.Commands;

public sealed class BuildRuntimeManifestTests
{
    [Test]
    public void Run_BuildsRuntimeReadableManifestForFullCommandSample()
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

        var manifestPath = Path.Combine(fixture.Root, "build", "windows", "manifest.json");
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var runtimeManifest = new RuntimeManifestReader().Read(manifestPath);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.Success));
            Assert.That(output.ToString(), Is.Empty);
            Assert.That(error.ToString(), Is.Empty);
            Assert.That(manifest.RootElement.GetProperty("schemaVersion").GetString(), Is.EqualTo("1.0"));
            Assert.That(manifest.RootElement.GetProperty("gameId").GetString(), Is.EqualTo("fullcommandsample"));
            Assert.That(manifest.RootElement.GetProperty("title").GetString(), Is.EqualTo("FullCommandSample"));
            Assert.That(manifest.RootElement.GetProperty("defaultLocale").GetString(), Is.EqualTo("ja-JP"));
            Assert.That(manifest.RootElement.GetProperty("scripts").GetArrayLength(), Is.EqualTo(7));
            Assert.That(manifest.RootElement.GetProperty("scripts")[0].GetProperty("scriptId").GetString(), Is.EqualTo("events/chapter001"));
            Assert.That(manifest.RootElement.GetProperty("scripts")[0].GetProperty("isEntry").GetBoolean(), Is.True);
            Assert.That(manifest.RootElement.GetProperty("events").GetArrayLength(), Is.EqualTo(6));
            Assert.That(manifest.RootElement.GetProperty("events")[0].GetProperty("eventId").GetString(), Is.EqualTo("chapter001_intro"));
            Assert.That(manifest.RootElement.GetProperty("events")[0].GetProperty("trigger").GetProperty("or").GetArrayLength(), Is.EqualTo(2));
            Assert.That(manifest.RootElement.GetProperty("events")[1].GetProperty("trigger").GetProperty("conditions").GetArrayLength(), Is.EqualTo(2));
            Assert.That(manifest.RootElement.GetProperty("assets").GetArrayLength(), Is.EqualTo(16));
            Assert.That(
                manifest.RootElement.GetProperty("assets").EnumerateArray().Select(static asset => asset.GetProperty("assetId").GetString()),
                Does.Contain("assets.actor.riku_normal"));
            Assert.That(
                manifest.RootElement.GetProperty("assets").EnumerateArray().Select(static asset => asset.GetProperty("assetId").GetString()),
                Does.Contain("assets.actor.riku_smile"));
            Assert.That(
                manifest.RootElement.GetProperty("assets").EnumerateArray().Select(static asset => asset.GetProperty("assetId").GetString()),
                Does.Contain("assets.audio.bgm.bgm_001_alice2"));
            Assert.That(
                manifest.RootElement.GetProperty("assets").EnumerateArray().Select(static asset => asset.GetProperty("assetId").GetString()),
                Does.Contain("assets.audio.se.se_001_door"));
            Assert.That(
                manifest.RootElement.GetProperty("assets").EnumerateArray().Select(static asset => asset.GetProperty("assetId").GetString()),
                Does.Contain("assets.voice.voice_001_sample"));
            Assert.That(manifest.RootElement.GetProperty("defaults").GetProperty("width").GetInt32(), Is.EqualTo(1280));
            Assert.That(manifest.RootElement.GetProperty("defaults").GetProperty("height").GetInt32(), Is.EqualTo(720));
            Assert.That(manifest.RootElement.GetProperty("build").GetProperty("target").GetString(), Is.EqualTo("windows"));
            Assert.That(runtimeManifest.Succeeded, Is.True);
            Assert.That(runtimeManifest.Document!.Scripts.Select(static script => script.ScriptId), Is.EqualTo(["events/chapter001", "events/chapter002", "events/chapter003", "events/chapter004", "events/chapter005", "events/actor_animation_test", "events/lib/Common"]));
            Assert.That(runtimeManifest.Document.Events.Select(static entry => entry.EventId), Is.EqualTo(["chapter001_intro", "chapter002_intro", "chapter003_intro", "chapter004_intro", "chapter005_intro", "actor_animation_test"]));
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
