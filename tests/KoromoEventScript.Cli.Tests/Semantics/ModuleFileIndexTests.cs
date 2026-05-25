using KoromoEventScript.Cli.ProjectSystem;
using KoromoEventScript.Cli.Semantics;

namespace KoromoEventScript.Cli.Tests.Semantics;

public class ModuleFileIndexTests
{
    [Test]
    public void Build_ScansKeAndKcFilesUnderEventsPath()
    {
        using var workspace = new TestProjectWorkspace();
        workspace.WriteEventFile("main.ke", "");
        workspace.WriteEventFile("legacy/LegacyCommon.kc", "");
        workspace.WriteEventFile("notes.txt", "");

        var index = new ModuleFileIndex().Build(workspace.Config).Index;

        var main = index.FindModule("main");
        var legacy = index.FindModule("LegacyCommon");
        var ignored = index.FindModule("notes");

        Assert.Multiple(() =>
        {
            Assert.That(main.Kind, Is.EqualTo(ModuleFileMatchKind.Found));
            Assert.That(main.File?.ProjectRelativePath, Is.EqualTo("events/main.ke"));
            Assert.That(legacy.Kind, Is.EqualTo(ModuleFileMatchKind.Found));
            Assert.That(legacy.File?.ProjectRelativePath, Is.EqualTo("events/legacy/LegacyCommon.kc"));
            Assert.That(ignored.Kind, Is.EqualTo(ModuleFileMatchKind.Missing));
        });
    }

    [Test]
    public void FindModule_UsesExtensionlessFileNameAsCaseSensitiveModuleKey()
    {
        using var workspace = new TestProjectWorkspace();
        workspace.WriteEventFile("lib/Common.ke", "");

        var index = new ModuleFileIndex().Build(workspace.Config).Index;

        var exact = index.FindModule("Common");
        var differentCase = index.FindModule("common");

        Assert.Multiple(() =>
        {
            Assert.That(exact.Kind, Is.EqualTo(ModuleFileMatchKind.Found));
            Assert.That(exact.File?.ModuleName, Is.EqualTo("Common"));
            Assert.That(differentCase.Kind, Is.EqualTo(ModuleFileMatchKind.Missing));
        });
    }

    [Test]
    public void FindModule_ReturnsAmbiguousMatchWithProjectRelativePathsForDuplicateModuleKeys()
    {
        using var workspace = new TestProjectWorkspace();
        workspace.WriteEventFile("lib/Common.ke", "");
        workspace.WriteEventFile("vendor/Common.kc", "");

        var index = new ModuleFileIndex().Build(workspace.Config).Index;

        var match = index.FindModule("Common");

        Assert.Multiple(() =>
        {
            Assert.That(match.Kind, Is.EqualTo(ModuleFileMatchKind.Ambiguous));
            Assert.That(match.File, Is.Null);
            Assert.That(
                match.Candidates.Select(static candidate => candidate.ProjectRelativePath),
                Is.EqualTo(["events/lib/Common.ke", "events/vendor/Common.kc"]));
        });
    }

    [Test]
    public void Build_ResolvesNestedImportTargetsFromProjectRootRatherThanImporterDirectory()
    {
        using var workspace = new TestProjectWorkspace();
        workspace.WriteEventFile("chapters/main.ke", "");
        workspace.WriteEventFile("shared/Shared.ke", "");

        var index = new ModuleFileIndex().Build(workspace.Config).Index;

        var match = index.FindModule("Shared");

        Assert.Multiple(() =>
        {
            Assert.That(match.Kind, Is.EqualTo(ModuleFileMatchKind.Found));
            Assert.That(match.File?.ProjectRelativePath, Is.EqualTo("events/shared/Shared.ke"));
        });
    }

    private sealed class TestProjectWorkspace : IDisposable
    {
        private readonly string root;

        public TestProjectWorkspace()
        {
            root = Path.Combine(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(EventsPath);
            Config = new ProjectConfig(
                root,
                "events/main.kel",
                "events",
                "assets",
                "locale",
                "build",
                "dist");
        }

        public ProjectConfig Config { get; }

        private string EventsPath => Path.Combine(root, "events");

        public void WriteEventFile(string projectEventsRelativePath, string contents)
        {
            var path = Path.Combine(EventsPath, projectEventsRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, contents);
        }

        public void Dispose()
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
