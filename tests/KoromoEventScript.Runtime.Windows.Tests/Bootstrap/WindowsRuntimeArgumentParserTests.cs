using KoromoEventScript.Runtime.Core.Diagnostics;
using KoromoEventScript.Runtime.Windows.Bootstrap;

namespace KoromoEventScript.Runtime.Windows.Tests.Bootstrap;

public sealed class WindowsRuntimeArgumentParserTests
{
    [Test]
    public void Parse_WithExplicitManifest_UsesRequestedManifestAndOptions()
    {
        using var workspace = TestWorkspace.Create();
        var defaultManifest = workspace.WriteFile("data/manifest.json", "{}");
        var requestedManifest = workspace.WriteFile("custom/game.json", "{}");

        var result = WindowsRuntimeArgumentParser.Parse(
            [
                "--manifest",
                requestedManifest,
                "--locale",
                "ja-JP",
                "--start",
                "chapter002:start",
                "--fullscreen",
                "--width",
                "1600",
                "--height",
                "900",
                "--debug",
                "--profile",
            ],
            workspace.Root);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Options?.ManifestPath, Is.EqualTo(Path.GetFullPath(requestedManifest)));
            Assert.That(result.Options?.ManifestPath, Is.Not.EqualTo(defaultManifest));
            Assert.That(result.Options?.Locale, Is.EqualTo("ja-JP"));
            Assert.That(result.Options?.Start, Is.EqualTo("chapter002:start"));
            Assert.That(result.Options?.Fullscreen, Is.True);
            Assert.That(result.Options?.Width, Is.EqualTo(1600));
            Assert.That(result.Options?.Height, Is.EqualTo(900));
            Assert.That(result.Options?.Debug, Is.True);
            Assert.That(result.Options?.Profile, Is.True);
        });
    }

    [Test]
    public void Parse_WithoutManifest_FindsDataManifestBeforeRootManifest()
    {
        using var workspace = TestWorkspace.Create();
        _ = workspace.WriteFile("manifest.json", "{}");
        var dataManifest = workspace.WriteFile("data/manifest.json", "{}");

        var result = WindowsRuntimeArgumentParser.Parse([], workspace.Root);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Options?.ManifestPath, Is.EqualTo(dataManifest));
        });
    }

    [TestCase("--unknown")]
    [TestCase("--width 0")]
    [TestCase("--height -1")]
    [TestCase("--manifest")]
    public void Parse_WithInvalidArguments_ReturnsRuntimeArgumentError(string commandLine)
    {
        using var workspace = TestWorkspace.Create();
        _ = workspace.WriteFile("data/manifest.json", "{}");

        var result = WindowsRuntimeArgumentParser.Parse(commandLine, workspace.Root);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureKind, Is.EqualTo(RuntimeFailureKind.Argument));
            Assert.That(result.Diagnostics.Single().FailureKind, Is.EqualTo(RuntimeFailureKind.Argument));
            Assert.That(RuntimeExitCodeMapper.Map(result.FailureKind), Is.EqualTo(RuntimeExitCode.CommandLineError));
        });
    }

    [Test]
    public void Parse_WithoutDiscoverableManifest_ReturnsStartupError()
    {
        using var workspace = TestWorkspace.Create();

        var result = WindowsRuntimeArgumentParser.Parse([], workspace.Root);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureKind, Is.EqualTo(RuntimeFailureKind.Startup));
        });
    }

    private sealed class TestWorkspace : IDisposable
    {
        private TestWorkspace(string root)
        {
            Root = root;
        }

        public string Root { get; }

        public static TestWorkspace Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "kes-windows-runtime-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new TestWorkspace(root);
        }

        public string WriteFile(string relativePath, string contents)
        {
            var fullPath = Path.GetFullPath(Path.Combine(Root, relativePath));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, contents);
            return fullPath;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
