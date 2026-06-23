using KoromoEventScript.Runtime.Core.Diagnostics;
using KoromoEventScript.Runtime.Core.Manifests;

namespace KoromoEventScript.Runtime.Core.Tests.Manifests;

public sealed class RuntimeManifestReaderTests
{
    [Test]
    public void Read_WithValidManifest_ResolvesRelativePathsFromManifestDirectory()
    {
        using var workspace = TestWorkspace.Create();
        var manifestPath = workspace.WriteFile(
            "data/manifest.json",
            """
            {
              "schemaVersion": "1.0",
              "gameId": "sample-game",
              "title": "Sample Game",
              "defaultLocale": "ja-JP",
              "scripts": [
                {
                  "scriptId": "chapter001",
                  "locale": "ja-JP",
                  "klibPath": "events/chapter001.klib",
                  "isEntry": true,
                  "startLabel": "#start"
                }
              ],
              "assets": [
                {
                  "assetId": "bg.school",
                  "kind": "background",
                  "path": "assets/bg/school.png",
                  "locale": null
                }
              ],
              "defaults": {
                "width": 1280,
                "height": 720,
                "fullscreen": false
              },
              "build": {
                "buildId": "build-001",
                "cliVersion": "0.1.0"
              }
            }
            """);

        var result = new RuntimeManifestReader().Read(manifestPath);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Document!.ManifestDirectory, Is.EqualTo(Path.GetDirectoryName(manifestPath)));
            Assert.That(result.Document.Scripts.Single().ResolvedKlibPath, Is.EqualTo(Path.Combine(Path.GetDirectoryName(manifestPath)!, "events", "chapter001.klib")));
            Assert.That(result.Document.Assets.Single().ResolvedPath, Is.EqualTo(Path.Combine(Path.GetDirectoryName(manifestPath)!, "assets", "bg", "school.png")));
        });
    }

    [Test]
    public void Read_WithMissingManifest_ReturnsStartupError()
    {
        using var workspace = TestWorkspace.Create();
        var missingPath = Path.Combine(workspace.Root, "data", "manifest.json");

        var result = new RuntimeManifestReader().Read(missingPath);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureKind, Is.EqualTo(RuntimeFailureKind.Startup));
            Assert.That(RuntimeExitCodeMapper.Map(result.FailureKind), Is.EqualTo(RuntimeExitCode.RuntimeStartupError));
            Assert.That(result.Diagnostics.Single().Severity, Is.EqualTo(RuntimeDiagnosticSeverity.Error));
        });
    }

    [Test]
    public void Read_WithMissingRequiredFields_ReturnsStartupError()
    {
        using var workspace = TestWorkspace.Create();
        var manifestPath = workspace.WriteFile(
            "data/manifest.json",
            """
            {
              "schemaVersion": "1.0",
              "gameId": "",
              "title": "Sample Game",
              "defaultLocale": "ja-JP",
              "scripts": []
            }
            """);

        var result = new RuntimeManifestReader().Read(manifestPath);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureKind, Is.EqualTo(RuntimeFailureKind.Startup));
            Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Code), Does.Contain("KESR1002"));
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
            var root = Path.Combine(Path.GetTempPath(), "kes-runtime-manifest-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new TestWorkspace(root);
        }

        public string WriteFile(string relativePath, string content)
        {
            var path = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            return path;
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
