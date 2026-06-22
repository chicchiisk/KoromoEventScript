using System.Text;
using KoromoEventScript.Runtime.Core.Diagnostics;
using KoromoEventScript.Runtime.Core.Klib;
using KoromoEventScript.Runtime.Core.Manifests;
using KoromoEventScript.Runtime.Core.Packages;

namespace KoromoEventScript.Runtime.Core.Tests.Packages;

public sealed class RuntimePackageResolverTests
{
    [Test]
    public void Resolve_WithMatchingKlibScriptId_ReturnsPackageModules()
    {
        using var workspace = TestWorkspace.Create();
        var klibPath = workspace.WriteMinimalKlib("data/events/chapter001.klib", "chapter001");
        var manifest = CreateManifest(workspace, new RuntimeScriptEntry(
            "chapter001",
            "ja-JP",
            "events/chapter001.klib",
            klibPath,
            IsEntry: true,
            StartLabel: null));

        var result = new RuntimePackageResolver(new KlibModuleLoader()).Resolve(manifest);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Package!.Scripts, Has.Count.EqualTo(1));
            Assert.That(result.Package.Scripts.Single().Entry.ScriptId, Is.EqualTo("chapter001"));
            Assert.That(result.Package.Scripts.Single().Document.Module.ScriptId, Is.EqualTo("chapter001"));
        });
    }

    [Test]
    public void Resolve_WithMissingKlib_ReturnsIoError()
    {
        using var workspace = TestWorkspace.Create();
        var missingPath = Path.Combine(workspace.Root, "data", "events", "missing.klib");
        var manifest = CreateManifest(workspace, new RuntimeScriptEntry(
            "missing",
            "ja-JP",
            "events/missing.klib",
            missingPath,
            IsEntry: true,
            StartLabel: null));

        var result = new RuntimePackageResolver(new KlibModuleLoader()).Resolve(manifest);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureKind, Is.EqualTo(RuntimeFailureKind.Io));
            Assert.That(RuntimeExitCodeMapper.Map(result.FailureKind), Is.EqualTo(RuntimeExitCode.FileOrDirectoryError));
            Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Code), Does.Contain("KESR2001"));
        });
    }

    [Test]
    public void Resolve_WithScriptIdMismatch_ReturnsStartupError()
    {
        using var workspace = TestWorkspace.Create();
        var klibPath = workspace.WriteMinimalKlib("data/events/chapter001.klib", "other-script");
        var manifest = CreateManifest(workspace, new RuntimeScriptEntry(
            "chapter001",
            "ja-JP",
            "events/chapter001.klib",
            klibPath,
            IsEntry: true,
            StartLabel: null));

        var result = new RuntimePackageResolver(new KlibModuleLoader()).Resolve(manifest);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureKind, Is.EqualTo(RuntimeFailureKind.Startup));
            Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Code), Does.Contain("KESR2002"));
        });
    }

    [TestCase(".kc")]
    [TestCase(".kel")]
    [TestCase(".csv")]
    [TestCase(".klibtxt")]
    public void Resolve_WithNonKlibRuntimeInput_ReturnsStartupError(string extension)
    {
        using var workspace = TestWorkspace.Create();
        var inputPath = workspace.WriteFile($"data/events/chapter001{extension}", "not a runtime klib");
        var manifest = CreateManifest(workspace, new RuntimeScriptEntry(
            "chapter001",
            "ja-JP",
            $"events/chapter001{extension}",
            inputPath,
            IsEntry: true,
            StartLabel: null));

        var result = new RuntimePackageResolver(new KlibModuleLoader()).Resolve(manifest);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureKind, Is.EqualTo(RuntimeFailureKind.Startup));
            Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Code), Does.Contain("KESR2003"));
        });
    }

    private static RuntimeManifestDocument CreateManifest(TestWorkspace workspace, params RuntimeScriptEntry[] scripts)
    {
        var manifestPath = Path.Combine(workspace.Root, "data", "manifest.json");
        return new RuntimeManifestDocument(
            "1.0",
            "sample-game",
            "Sample Game",
            "ja-JP",
            scripts,
            [],
            new RuntimeSettings(1280, 720, Fullscreen: false),
            new RuntimeBuildInfo("build-001", "0.1.0"),
            manifestPath,
            Path.GetDirectoryName(manifestPath)!);
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
            var root = Path.Combine(Path.GetTempPath(), "kes-runtime-package-tests", Guid.NewGuid().ToString("N"));
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

        public string WriteMinimalKlib(string relativePath, string scriptId)
        {
            var path = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            using var moduleStream = new MemoryStream();
            using (var moduleWriter = new BinaryWriter(moduleStream, Encoding.UTF8, leaveOpen: true))
            {
                WriteString(moduleWriter, scriptId);
                WriteString(moduleWriter, $"{scriptId}.module");
                WriteString(moduleWriter, $"events/{scriptId}.kc");
                moduleWriter.Write(0);
            }

            var moduleBytes = moduleStream.ToArray();
            const int headerSize = 4 + (5 * sizeof(int));
            const int sectionTableSize = 3 * sizeof(int);
            var moduleOffset = headerSize + sectionTableSize;

            using var fileStream = File.OpenWrite(path);
            using var writer = new BinaryWriter(fileStream, Encoding.UTF8, leaveOpen: false);
            writer.Write(Encoding.ASCII.GetBytes("KLIB"));
            writer.Write(1);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Write(1);
            writer.Write(0x0001);
            writer.Write(moduleOffset);
            writer.Write(moduleBytes.Length);
            writer.Write(moduleBytes);

            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }

        private static void WriteString(BinaryWriter writer, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }
    }
}
