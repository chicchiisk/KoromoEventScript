using KoromoEventScript.Cli.ProjectSystem;

namespace KoromoEventScript.Cli.Tests.ProjectSystem;

public class ProjectConfigLoaderTests
{
    [Test]
    public void Load_ReadsMinimalProjectConfig()
    {
        var projectRoot = GetTestDataPath("projects", "minimal");

        var result = new ProjectConfigLoader().Load(projectRoot);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Config!.ProjectRoot, Is.EqualTo(projectRoot));
            Assert.That(result.Config.ProjectName, Is.EqualTo("MinimalProject"));
            Assert.That(result.Config.ProjectVersion, Is.EqualTo("0.1.0"));
            Assert.That(result.Config.EntryPath, Is.EqualTo("events/main.kel"));
            Assert.That(result.Config.EventsPath, Is.EqualTo("events"));
            Assert.That(result.Config.BuildPath, Is.EqualTo("build"));
            Assert.That(result.Config.DistPath, Is.EqualTo("dist"));
            Assert.That(result.Config.WarningsAsErrors, Is.False);
            Assert.That(result.Config.RuntimeWindowWidth, Is.EqualTo(1280));
            Assert.That(result.Config.RuntimeWindowHeight, Is.EqualTo(720));
        });
    }

    [TestCase("true", true)]
    [TestCase("false", false)]
    public void Load_ReadsWarningsAsErrorsBuildConfig(string value, bool expected)
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteFile("kes.xml", $$"""
<?xml version="1.0" encoding="utf-8"?>
<KoromoEventScript>
    <Project Name="Temp" Version="0.1.0" Entry="events/main.kel" />
    <Paths Events="events" Assets="assets" Locale="locale" Build="build" Dist="dist" />
    <Build WarningsAsErrors="{{value}}" />
</KoromoEventScript>
""");

        var result = new ProjectConfigLoader().Load(fixture.Root);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Config!.WarningsAsErrors, Is.EqualTo(expected));
        });
    }

    [Test]
    public void Load_ReturnsDiagnosticForInvalidWarningsAsErrorsBuildConfig()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteFile("kes.xml", """
<?xml version="1.0" encoding="utf-8"?>
<KoromoEventScript>
    <Project Name="Temp" Version="0.1.0" Entry="events/main.kel" />
    <Paths Events="events" Assets="assets" Locale="locale" Build="build" Dist="dist" />
    <Build WarningsAsErrors="maybe" />
</KoromoEventScript>
""");

        var result = new ProjectConfigLoader().Load(fixture.Root);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Diagnostic!.Code, Is.EqualTo("KES9003"));
            Assert.That(result.Diagnostic.File, Is.EqualTo("kes.xml"));
        });
    }

    [Test]
    public void Load_ReturnsDiagnosticForInvalidConfig()
    {
        using var fixture = TemporaryProject.Create();
        File.WriteAllText(Path.Combine(fixture.Root, "kes.xml"), "<KoromoEventScript><Project /></KoromoEventScript>");

        var result = new ProjectConfigLoader().Load(fixture.Root);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Diagnostic!.Code, Is.EqualTo("KES9003"));
            Assert.That(result.Diagnostic.File, Is.EqualTo("kes.xml"));
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
