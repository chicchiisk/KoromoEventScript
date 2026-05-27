using KoromoEventScript.Cli.Build;
using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.ProjectSystem;
using KoromoEventScript.Cli.Semantics;

namespace KoromoEventScript.Cli.Tests.Semantics;

public class SemanticAnalyzerTests
{
    [Test]
    public void Analyze_ReturnsSuccessForResolvableImportsAndNames()
    {
        var project = LoadFixtureProject("success");
        var roots = ParseRoots(project, "events/main.kc");

        var result = new SemanticAnalyzer().Analyze(project, roots);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.Success));
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.DefinitionCollections, Is.Not.Empty);
            Assert.That(result.DefinitionCollections.SelectMany(static collection => collection.DefinitionTable.Definitions).Select(static definition => definition.Name),
                Does.Contain("commonValue"));
            Assert.That(result.ImportGraph!.OrderedDocuments.Select(static document => document.ModuleName),
                Is.EqualTo(["main", "Common", "Shared", "LegacyCommon"]));
        });
    }

    [Test]
    public void Analyze_ReturnsImportDiagnosticsBeforeNameResolution()
    {
        var project = LoadFixtureProject("missing-import");
        var roots = ParseRoots(project, "events/main.ke");

        var result = new SemanticAnalyzer().Analyze(project, roots);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.FileOrDirectoryError));
            Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Code), Is.EqualTo(["KES9005"]));
            Assert.That(result.NameResolution.Succeeded, Is.True);
        });
    }

    [Test]
    public void Analyze_PreservesImportedSyntaxFailureClassification()
    {
        var project = LoadFixtureProject("syntax-error");
        var roots = ParseRoots(project, "events/main.ke");

        var result = new SemanticAnalyzer().Analyze(project, roots);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.SyntaxError));
            Assert.That(result.Diagnostics.Single().Code, Does.StartWith("KES1"));
            Assert.That(result.Diagnostics.Single().File, Is.EqualTo("events/broken/Broken.ke"));
        });
    }

    [Test]
    public void Analyze_ReturnsCompileDiagnosticsForNameResolutionFailures()
    {
        var project = LoadFixtureProject("name-resolution-failure");
        var roots = ParseRoots(project, "events/main.ke");

        var result = new SemanticAnalyzer().Analyze(project, roots);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.CompileError));
            Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Code), Is.EqualTo(["KES2010", "KES2012"]));
            Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.File), Is.All.EqualTo("events/main.ke"));
        });
    }

    [Test]
    public void Analyze_ReturnsCompileDiagnosticsForDuplicateDefinitionsBeforeNameResolution()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteConfig();
        fixture.WriteFile("events/main.kel", """
entry = intro
intro = {
    chapter = "events/main.ke"
}
""");
        fixture.WriteFile("events/main.ke", """
var sharedValue = 1
var sharedValue = 2
""");
        var project = LoadProject(fixture.Root);
        var roots = ParseRoots(project, "events/main.ke");

        var result = new SemanticAnalyzer().Analyze(project, roots);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.CompileError));
            Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Code), Is.EqualTo(["KES2009"]));
            Assert.That(result.Diagnostics[0].Line, Is.EqualTo(2));
            Assert.That(result.NameResolution.Succeeded, Is.False);
            Assert.That(result.DefinitionCollections.Single().Succeeded, Is.False);
        });
    }

    [Test]
    public void Analyze_ReportsDuplicateModuleDefinitionsAcrossRootFilesWithSameModuleName()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteConfig();
        fixture.WriteFile("events/main.kel", """
entry = intro
intro = {
    chapter = "events/first/Main.ke"
    chapter = "events/second/Main.ke"
}
""");
        fixture.WriteFile("events/first/Main.ke", "var score = 1\n");
        fixture.WriteFile("events/second/Main.ke", "var score = 2\n");
        var project = LoadProject(fixture.Root);
        var roots = ParseRoots(project, "events/first/Main.ke", "events/second/Main.ke");

        var result = new SemanticAnalyzer().Analyze(project, roots);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.CompileError));
            Assert.That(result.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(result.Diagnostics[0].Code, Is.EqualTo("KES2009"));
            Assert.That(result.Diagnostics[0].File, Is.EqualTo("events/second/Main.ke"));
            Assert.That(result.Diagnostics[0].Line, Is.EqualTo(1));
            Assert.That(result.Diagnostics[0].Column, Is.EqualTo(5));
            Assert.That(result.Diagnostics[0].RelatedLocations.Single().File, Is.EqualTo("events/first/Main.ke"));
            Assert.That(result.Diagnostics[0].RelatedLocations.Single().Line, Is.EqualTo(1));
            Assert.That(result.Diagnostics[0].RelatedLocations.Single().Column, Is.EqualTo(5));
        });
    }

    [Test]
    public void Analyze_ReturnsCompileDiagnosticsForShadowingBeforeNameResolution()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteConfig();
        fixture.WriteFile("events/main.kel", """
entry = intro
intro = {
    chapter = "events/main.ke"
}
""");
        fixture.WriteFile("events/main.ke", """
var score = 0
fn calc(score: number):
    use missingName
""");
        var project = LoadProject(fixture.Root);
        var roots = ParseRoots(project, "events/main.ke");

        var result = new SemanticAnalyzer().Analyze(project, roots);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.CompileError));
            Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Code), Is.EqualTo(["KES2014"]));
            Assert.That(result.NameResolution.Succeeded, Is.False);
            Assert.That(result.DefinitionCollections.Single().Succeeded, Is.False);
        });
    }

    private static ProjectConfig LoadFixtureProject(string scenarioName)
    {
        return LoadProject(Path.Combine(GetRepositoryRoot(), "testdata", "projects", "import-resolution", scenarioName));
    }

    private static ProjectConfig LoadProject(string projectRoot)
    {
        var loadResult = new ProjectConfigLoader().Load(projectRoot);
        Assert.That(loadResult.Succeeded, Is.True, loadResult.Diagnostic?.Message);
        return loadResult.Config!;
    }

    private static IReadOnlyList<ScriptDocument> ParseRoots(ProjectConfig project, params string[] relativePaths)
    {
        var parser = new SourceFileParser();
        return relativePaths
            .Select(relativePath =>
            {
                var result = parser.ParseKe(Path.Combine(project.ProjectRoot, relativePath), relativePath);
                Assert.That(result.Status, Is.EqualTo(SourceParseStatus.Success), relativePath);
                return new ScriptDocument(relativePath, Path.GetFileNameWithoutExtension(relativePath), result.Syntax!);
            })
            .ToArray();
    }

    private static string GetRepositoryRoot()
    {
        return Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", ".."));
    }
}
