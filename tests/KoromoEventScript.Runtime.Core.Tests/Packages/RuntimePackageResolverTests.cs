using System.Text;
using KoromoEventScript.Runtime.Core.Diagnostics;
using KoromoEventScript.Runtime.Core.Klib;
using KoromoEventScript.Runtime.Core.Manifests;
using KoromoEventScript.Runtime.Core.Packages;

namespace KoromoEventScript.Runtime.Core.Tests.Packages;

public sealed class RuntimePackageResolverTests
{
    [Test]
    public void Resolve_WithFullCommandSampleBuildOutput_LoadsRuntimePackage()
    {
        var manifestPath = Path.Combine(
            GetRepositoryRoot(),
            "testdata",
            "projects",
            "full-command-sample",
            "build",
            "windows",
            "manifest.json");

        var manifestResult = new RuntimeManifestReader().Read(manifestPath);
        var packageResult = manifestResult.Succeeded
            ? new RuntimePackageResolver(new KlibModuleLoader()).Resolve(manifestResult.Document!)
            : null;

        Assert.Multiple(() =>
        {
            Assert.That(manifestResult.Succeeded, Is.True);
            Assert.That(packageResult!.Succeeded, Is.True);
            Assert.That(packageResult.Package!.SelectedLocale, Is.EqualTo("ja-JP"));
            Assert.That(
                packageResult.Package.Scripts.Select(static script => script.Entry.ScriptId),
                Is.EqualTo(["events/chapter001", "events/chapter002", "events/lib/Common"]));
            Assert.That(
                packageResult.Package.Scripts.Select(static script => script.Entry.KlibPath),
                Is.EqualTo(["events/chapter001.klib", "events/chapter002.klib", "events/lib/Common.klib"]));
            Assert.That(
                packageResult.Package.Scripts.Select(static script => script.Document.Module.ScriptId),
                Is.EqualTo(["events/chapter001", "events/chapter002", "events/lib/Common"]));
        });
    }

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

    [Test]
    public void Resolve_WithSelectedLocale_UsesLocalizedKlibAndAssets()
    {
        using var workspace = TestWorkspace.Create();
        var jaKlibPath = workspace.WriteMinimalKlib("data/events/ja-JP/chapter001.klib", "chapter001");
        var enKlibPath = workspace.WriteMinimalKlib("data/events/en-US/chapter001.klib", "chapter001");
        var sharedBackgroundPath = workspace.WriteFile("data/assets/bg/school.png", "shared background");
        var jaVoicePath = workspace.WriteFile("data/assets/voice/ja/chapter001_001.ogg", "ja voice");
        var enVoicePath = workspace.WriteFile("data/assets/voice/en/chapter001_001.ogg", "en voice");
        var manifest = CreateManifest(
            workspace,
            [
                new RuntimeScriptEntry("chapter001", "ja-JP", "events/ja-JP/chapter001.klib", jaKlibPath, IsEntry: true, StartLabel: null),
                new RuntimeScriptEntry("chapter001", "en-US", "events/en-US/chapter001.klib", enKlibPath, IsEntry: true, StartLabel: null),
            ],
            [
                new RuntimeAssetEntry("bg.school", "background", "assets/bg/school.png", sharedBackgroundPath, Locale: null),
                new RuntimeAssetEntry("voice.chapter001.001", "voice", "assets/voice/ja/chapter001_001.ogg", jaVoicePath, "ja-JP"),
                new RuntimeAssetEntry("voice.chapter001.001", "voice", "assets/voice/en/chapter001_001.ogg", enVoicePath, "en-US"),
            ]);

        var result = new RuntimePackageResolver(new KlibModuleLoader())
            .Resolve(manifest, new RuntimePackageResolveOptions("en-US"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Package!.SelectedLocale, Is.EqualTo("en-US"));
            Assert.That(result.Package.Scripts.Single().Entry.ResolvedKlibPath, Is.EqualTo(enKlibPath));
            Assert.That(result.Package.Resources.Assets, Has.Count.EqualTo(2));
            Assert.That(result.Package.Resources.ResolveAsset("bg.school")!.ResolvedPath, Is.EqualTo(sharedBackgroundPath));
            Assert.That(result.Package.Resources.ResolveAsset("voice.chapter001.001")!.ResolvedPath, Is.EqualTo(enVoicePath));
        });
    }

    [Test]
    public void Resolve_WithUnknownLocale_FallsBackToDefaultLocale()
    {
        using var workspace = TestWorkspace.Create();
        var jaKlibPath = workspace.WriteMinimalKlib("data/events/ja-JP/chapter001.klib", "chapter001");
        var enKlibPath = workspace.WriteMinimalKlib("data/events/en-US/chapter001.klib", "chapter001");
        var manifest = CreateManifest(
            workspace,
            [
                new RuntimeScriptEntry("chapter001", "ja-JP", "events/ja-JP/chapter001.klib", jaKlibPath, IsEntry: true, StartLabel: null),
                new RuntimeScriptEntry("chapter001", "en-US", "events/en-US/chapter001.klib", enKlibPath, IsEntry: true, StartLabel: null),
            ],
            []);

        var result = new RuntimePackageResolver(new KlibModuleLoader())
            .Resolve(manifest, new RuntimePackageResolveOptions("fr-FR"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Package!.SelectedLocale, Is.EqualTo("ja-JP"));
            Assert.That(result.Package.Scripts.Single().Entry.ResolvedKlibPath, Is.EqualTo(jaKlibPath));
        });
    }

    [Test]
    public void Resolve_WithMissingAsset_ReturnsIoError()
    {
        using var workspace = TestWorkspace.Create();
        var klibPath = workspace.WriteMinimalKlib("data/events/chapter001.klib", "chapter001");
        var missingAssetPath = Path.Combine(workspace.Root, "data", "assets", "bg", "missing.png");
        var manifest = CreateManifest(
            workspace,
            [new RuntimeScriptEntry("chapter001", "ja-JP", "events/chapter001.klib", klibPath, IsEntry: true, StartLabel: null)],
            [new RuntimeAssetEntry("bg.missing", "background", "assets/bg/missing.png", missingAssetPath, Locale: null)]);

        var result = new RuntimePackageResolver(new KlibModuleLoader()).Resolve(manifest);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureKind, Is.EqualTo(RuntimeFailureKind.Io));
            Assert.That(RuntimeExitCodeMapper.Map(result.FailureKind), Is.EqualTo(RuntimeExitCode.FileOrDirectoryError));
            Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Code), Does.Contain("KESR2004"));
        });
    }

    private static RuntimeManifestDocument CreateManifest(TestWorkspace workspace, params RuntimeScriptEntry[] scripts)
    {
        return CreateManifest(workspace, scripts, []);
    }

    private static RuntimeManifestDocument CreateManifest(
        TestWorkspace workspace,
        IReadOnlyList<RuntimeScriptEntry> scripts,
        IReadOnlyList<RuntimeAssetEntry> assets)
    {
        var manifestPath = Path.Combine(workspace.Root, "data", "manifest.json");
        return new RuntimeManifestDocument(
            "1.0",
            "sample-game",
            "Sample Game",
            "ja-JP",
            scripts,
            assets,
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

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "KoromoEventScript.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
