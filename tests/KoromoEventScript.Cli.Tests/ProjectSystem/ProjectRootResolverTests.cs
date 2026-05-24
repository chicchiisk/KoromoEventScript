using KoromoEventScript.Cli.ProjectSystem;

namespace KoromoEventScript.Cli.Tests.ProjectSystem;

public class ProjectRootResolverTests
{
    [Test]
    public void Resolve_UsesExplicitProjectDirectory()
    {
        var projectRoot = GetTestDataPath("projects", "minimal");

        var result = new ProjectRootResolver().Resolve(projectRoot, TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.ProjectRoot, Is.EqualTo(Path.GetFullPath(projectRoot)));
            Assert.That(result.Diagnostic, Is.Null);
        });
    }

    [Test]
    public void Resolve_DiscoversProjectRootFromCurrentDirectory()
    {
        var currentDirectory = GetTestDataPath("projects", "minimal", "events");

        var result = new ProjectRootResolver().Resolve(null, currentDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.ProjectRoot, Is.EqualTo(GetTestDataPath("projects", "minimal")));
        });
    }

    [Test]
    public void Resolve_ReturnsDiagnosticWhenKesXmlCannotBeFound()
    {
        var currentDirectory = TestContext.CurrentContext.WorkDirectory;

        var result = new ProjectRootResolver().Resolve(null, currentDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Diagnostic!.Code, Is.EqualTo("KES9002"));
        });
    }

    private static string GetTestDataPath(params string[] segments)
    {
        return Path.GetFullPath(Path.Combine(GetRepositoryRoot(), "testdata", Path.Combine(segments)));
    }

    private static string GetRepositoryRoot()
    {
        return Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", ".."));
    }
}
