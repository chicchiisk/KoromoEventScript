using KoromoEventScript.Cli.Build;
using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.Diagnostics;
using KoromoEventScript.Cli.Parsing;
using KoromoEventScript.Cli.ProjectSystem;
using KoromoEventScript.Cli.Semantics;

namespace KoromoEventScript.Cli.Tests.Semantics;

public class ImportResolverTests
{
    [Test]
    public void ResolveImports_BuildsGraphForDirectAndTransitiveImports()
    {
        using var workspace = new TestProjectWorkspace();
        workspace.WriteEventFile("main.ke", """
            import Common
            """);
        workspace.WriteEventFile("lib/Common.ke", """
            import Shared
            """);
        workspace.WriteEventFile("shared/Shared.ke", """
            var shared: text
            """);
        var root = workspace.ParseDocument("events/main.ke", "main");

        var result = Resolve(workspace, [root]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(
                result.ImportGraph!.OrderedDocuments.Select(static document => document.ModuleName),
                Is.EqualTo(["main", "Common", "Shared"]));
            Assert.That(result.ImportGraph.DirectImports["main"], Is.EqualTo(["Common"]));
            Assert.That(result.ImportGraph.DirectImports["Common"], Is.EqualTo(["Shared"]));
            Assert.That(result.ImportGraph.GetReachableImports("main"), Is.EqualTo(["Common", "Shared"]));
        });
    }

    [Test]
    public void ResolveImports_SuppressesDuplicateImportsAndKeepsStableInspectionOrder()
    {
        using var workspace = new TestProjectWorkspace();
        workspace.WriteEventFile("main.ke", """
            import Left
            import Right
            import Shared
            """);
        workspace.WriteEventFile("left/Left.ke", """
            import Shared
            """);
        workspace.WriteEventFile("right/Right.ke", """
            import Shared
            """);
        workspace.WriteEventFile("shared/Shared.ke", """
            var shared: text
            """);
        var root = workspace.ParseDocument("events/main.ke", "main");

        var result = Resolve(workspace, [root]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(
                result.ImportGraph!.OrderedDocuments.Select(static document => document.ModuleName),
                Is.EqualTo(["main", "Left", "Shared", "Right"]));
            Assert.That(result.ImportGraph.GetReachableImports("main"), Is.EqualTo(["Left", "Shared", "Right"]));
        });
    }

    [Test]
    public void ResolveImports_DoesNotReadImportTargetThatWasAlreadyParsedAsRoot()
    {
        using var workspace = new TestProjectWorkspace();
        workspace.WriteEventFile("main.ke", """
            import Common
            """);
        workspace.WriteEventFile("Common.ke", """
            var shared: text
            """);
        var main = workspace.ParseDocument("events/main.ke", "main");
        var common = workspace.ParseDocument("events/Common.ke", "Common");
        var index = new ModuleFileIndex().Build(workspace.Config).Index;
        var resolver = new ImportResolver(_ => throw new InvalidOperationException("Already parsed roots should not be read again."));

        var result = resolver.ResolveImports(index, [main, common]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(
                result.ImportGraph!.OrderedDocuments.Select(static document => document.ModuleName),
                Is.EqualTo(["main", "Common"]));
            Assert.That(result.ImportGraph.DirectImports["main"], Is.EqualTo(["Common"]));
        });
    }

    [Test]
    public void ResolveImports_ReturnsMissingImportDiagnosticAtImporterLocation()
    {
        using var workspace = new TestProjectWorkspace();
        workspace.WriteEventFile("main.ke", """
            import DoesNotExist
            """);
        var root = workspace.ParseDocument("events/main.ke", "main");

        var result = Resolve(workspace, [root]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.FileOrDirectoryError));
            Assert.That(result.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(result.Diagnostics[0].Code, Is.EqualTo("KES9005"));
            Assert.That(result.Diagnostics[0].File, Is.EqualTo("events/main.ke"));
            Assert.That(result.Diagnostics[0].Line, Is.EqualTo(1));
            Assert.That(result.Diagnostics[0].Column, Is.EqualTo(1));
            Assert.That(result.Diagnostics[0].Message, Does.Contain("DoesNotExist"));
        });
    }

    [Test]
    public void ResolveImports_ReturnsAmbiguousImportDiagnosticWithCandidatePaths()
    {
        using var workspace = new TestProjectWorkspace();
        workspace.WriteEventFile("main.ke", """
            import Common
            """);
        workspace.WriteEventFile("lib/Common.ke", "");
        workspace.WriteEventFile("vendor/Common.kc", "");
        var root = workspace.ParseDocument("events/main.ke", "main");

        var result = Resolve(workspace, [root]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.CompileError));
            Assert.That(result.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(result.Diagnostics[0].Code, Is.EqualTo("KES2007"));
            Assert.That(result.Diagnostics[0].File, Is.EqualTo("events/main.ke"));
            Assert.That(result.Diagnostics[0].Message, Does.Contain("events/lib/Common.ke"));
            Assert.That(result.Diagnostics[0].Message, Does.Contain("events/vendor/Common.kc"));
        });
    }

    [Test]
    public void ResolveImports_ReturnsCycleDiagnosticWithImportPath()
    {
        using var workspace = new TestProjectWorkspace();
        workspace.WriteEventFile("main.ke", """
            import A
            """);
        workspace.WriteEventFile("cycles/A.ke", """
            import B
            """);
        workspace.WriteEventFile("cycles/B.ke", """
            import A
            """);
        var root = workspace.ParseDocument("events/main.ke", "main");

        var result = Resolve(workspace, [root]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.CompileError));
            Assert.That(result.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(result.Diagnostics[0].Code, Is.EqualTo("KES2008"));
            Assert.That(result.Diagnostics[0].File, Is.EqualTo("events/cycles/B.ke"));
            Assert.That(result.Diagnostics[0].Message, Does.Contain("A -> B -> A"));
        });
    }

    [Test]
    public void ResolveImports_PreservesImportedSyntaxDiagnostic()
    {
        using var workspace = new TestProjectWorkspace();
        workspace.WriteEventFile("main.ke", """
            import Broken
            """);
        workspace.WriteEventFile("broken/Broken.ke", """
            nar:
            """);
        var root = workspace.ParseDocument("events/main.ke", "main");

        var result = Resolve(workspace, [root]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.SyntaxError));
            Assert.That(result.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(result.Diagnostics[0].Code, Does.StartWith("KES1"));
            Assert.That(result.Diagnostics[0].File, Is.EqualTo("events/broken/Broken.ke"));
        });
    }

    [Test]
    public void ResolveImports_ReturnsUnreadableImportDiagnostic()
    {
        using var workspace = new TestProjectWorkspace();
        workspace.WriteEventFile("main.ke", """
            import Unreadable
            """);
        workspace.WriteEventFile("Unreadable.ke", "");
        var root = workspace.ParseDocument("events/main.ke", "main");
        SourceParseResult<ScriptSyntax> ParseAsUnreadable(ModuleFileEntry entry)
        {
            return new SourceParseResult<ScriptSyntax>(
                null,
                new Diagnostic(DiagnosticLevel.Error, "KES9004", entry.ProjectRelativePath, 1, 1, "Could not read input file: denied"),
                SourceParseStatus.FileError);
        }

        var index = new ModuleFileIndex().Build(workspace.Config).Index;
        var result = new ImportResolver(ParseAsUnreadable).ResolveImports(index, [root]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.FileOrDirectoryError));
            Assert.That(result.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(result.Diagnostics[0].Code, Is.EqualTo("KES9004"));
            Assert.That(result.Diagnostics[0].File, Is.EqualTo("events/Unreadable.ke"));
        });
    }

    [Test]
    public void ResolveImports_ReturnsMultipleDiagnosticsInInspectionOrder()
    {
        using var workspace = new TestProjectWorkspace();
        workspace.WriteEventFile("main.ke", """
            import MissingOne
            import Common
            import MissingTwo
            """);
        workspace.WriteEventFile("lib/Common.ke", "");
        workspace.WriteEventFile("vendor/Common.kc", "");
        var root = workspace.ParseDocument("events/main.ke", "main");

        var result = Resolve(workspace, [root]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(
                result.Diagnostics.Select(static diagnostic => diagnostic.Code),
                Is.EqualTo(["KES9005", "KES2007", "KES9005"]));
            Assert.That(result.Diagnostics[0].Message, Does.Contain("MissingOne"));
            Assert.That(result.Diagnostics[1].Message, Does.Contain("Common"));
            Assert.That(result.Diagnostics[2].Message, Does.Contain("MissingTwo"));
        });
    }

    [Test]
    public void ResolveImports_DoesNotRequireGeneratedArtifacts()
    {
        using var workspace = new TestProjectWorkspace();
        workspace.WriteEventFile("main.ke", """
            import Common
            """);
        workspace.WriteEventFile("Common.ke", "");
        var root = workspace.ParseDocument("events/main.ke", "main");

        var result = Resolve(workspace, [root]);

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(Path.Combine(workspace.Root, "build", "game.k")), Is.False);
            Assert.That(File.Exists(Path.Combine(workspace.Root, "dist", "manifest.json")), Is.False);
            Assert.That(result.Succeeded, Is.True);
        });
    }

    private static ImportResolutionResult Resolve(TestProjectWorkspace workspace, IReadOnlyList<ScriptDocument> roots)
    {
        var index = new ModuleFileIndex().Build(workspace.Config).Index;
        return new ImportResolver().ResolveImports(index, roots);
    }

    private sealed class TestProjectWorkspace : IDisposable
    {
        public TestProjectWorkspace()
        {
            Root = Path.Combine(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(EventsPath);
            Config = new ProjectConfig(
                Root,
                "events/main.kel",
                "events",
                "assets",
                "locale",
                "build",
                "dist");
        }

        public string Root { get; }

        public ProjectConfig Config { get; }

        private string EventsPath => Path.Combine(Root, "events");

        public void WriteEventFile(string projectEventsRelativePath, string contents)
        {
            var path = Path.Combine(EventsPath, projectEventsRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, contents);
        }

        public ScriptDocument ParseDocument(string projectRelativePath, string moduleName)
        {
            var fullPath = Path.Combine(Root, projectRelativePath);
            return new ScriptDocument(
                projectRelativePath,
                moduleName,
                KeParser.Parse(File.ReadAllText(fullPath)));
        }

        public void Dispose()
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
