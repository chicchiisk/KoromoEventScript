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
            Assert.That(manifest.RootElement.GetProperty("scripts").GetArrayLength(), Is.EqualTo(3));
            Assert.That(manifest.RootElement.GetProperty("scripts")[0].GetProperty("scriptId").GetString(), Is.EqualTo("events/chapter001"));
            Assert.That(manifest.RootElement.GetProperty("scripts")[0].GetProperty("isEntry").GetBoolean(), Is.True);
            Assert.That(manifest.RootElement.GetProperty("assets").GetArrayLength(), Is.EqualTo(0));
            Assert.That(manifest.RootElement.GetProperty("defaults").GetProperty("width").GetInt32(), Is.EqualTo(1280));
            Assert.That(manifest.RootElement.GetProperty("defaults").GetProperty("height").GetInt32(), Is.EqualTo(720));
            Assert.That(manifest.RootElement.GetProperty("build").GetProperty("target").GetString(), Is.EqualTo("windows"));
            Assert.That(runtimeManifest.Succeeded, Is.True);
            Assert.That(runtimeManifest.Document!.Scripts.Select(static script => script.ScriptId), Is.EqualTo(["events/chapter001", "events/chapter002", "events/lib/Common"]));
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
