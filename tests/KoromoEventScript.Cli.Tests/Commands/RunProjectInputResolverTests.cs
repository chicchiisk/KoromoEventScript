using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.Commands.Run;
using KoromoEventScript.Cli.Diagnostics;
using KoromoEventScript.Cli.ProjectSystem;

namespace KoromoEventScript.Cli.Tests.Commands;

public class RunProjectInputResolverTests
{
    [Test]
    public void Resolve_DiscoversProjectRootFromCurrentDirectory()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteConfig();
        fixture.WriteFile("events/main.kel", "event main");
        var currentDirectory = Path.Combine(fixture.Root, "events", "nested");
        Directory.CreateDirectory(currentDirectory);

        var result = CreateResolver().Resolve(null, currentDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.Success));
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.Input!.ProjectRoot, Is.EqualTo(Path.GetFullPath(fixture.Root)));
            Assert.That(result.Input.Config, Is.Not.Null);
            Assert.That(result.Input.EntryPath, Is.EqualTo("events/main.kel"));
            Assert.That(result.Input.EntryFullPath, Is.EqualTo(Path.Combine(fixture.Root, "events", "main.kel")));
            Assert.That(result.Input.ManifestPath, Is.EqualTo(Path.Combine(fixture.Root, "build", "windows", "manifest.json")));
        });
    }

    [Test]
    public void Resolve_UsesExplicitProjectDirectory()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteConfig("story/start.kel");
        fixture.WriteFile("story/start.kel", "event start");

        var result = CreateResolver().Resolve(fixture.Root, TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Input!.ProjectRoot, Is.EqualTo(Path.GetFullPath(fixture.Root)));
            Assert.That(result.Input.EntryPath, Is.EqualTo("story/start.kel"));
            Assert.That(result.Input.EntryFullPath, Is.EqualTo(Path.Combine(fixture.Root, "story", "start.kel")));
        });
    }

    [TestCase("legacy.kc", ".kc file input is no longer supported")]
    [TestCase("legacy.kel", ".kel file input is no longer supported")]
    [TestCase("notes.txt", "Specify a project directory")]
    public void Resolve_RejectsExplicitFileInputBeforeProjectRootResolution(string fileName, string expectedMessage)
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteFile(fileName, "content");
        var filePath = Path.Combine(fixture.Root, fileName);

        var result = CreateResolver().Resolve(filePath, TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Input, Is.Null);
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.CommandLineError));
            Assert.That(result.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(result.Diagnostics[0].Code, Is.EqualTo("KES9001"));
            Assert.That(result.Diagnostics[0].Level, Is.EqualTo(DiagnosticLevel.Error));
            Assert.That(result.Diagnostics[0].Message, Does.Contain(expectedMessage));
        });
    }

    [Test]
    public void Resolve_ReturnsDiagnosticWhenKesXmlCannotBeFound()
    {
        using var fixture = TemporaryProject.Create();

        var result = CreateResolver().Resolve(fixture.Root, TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.FileOrDirectoryError));
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("KES9002"));
        });
    }

    [Test]
    public void Resolve_ReturnsDiagnosticWhenKesXmlIsInvalid()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteFile("kes.xml", "<KoromoEventScript><Project /></KoromoEventScript>");

        var result = CreateResolver().Resolve(fixture.Root, TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.FileOrDirectoryError));
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("KES9003"));
        });
    }

    [Test]
    public void Resolve_ReturnsDiagnosticWhenProjectEntryIsMissing()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteFile("kes.xml", """
<?xml version="1.0" encoding="utf-8"?>
<KoromoEventScript>
    <Project Name="Temp" Version="0.1.0" />
    <Paths Events="events" Assets="assets" Locale="locale" Build="build" Dist="dist" />
</KoromoEventScript>
""");

        var result = CreateResolver().Resolve(fixture.Root, TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.FileOrDirectoryError));
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("KES9003"));
            Assert.That(result.Diagnostics.Single().Message, Does.Contain("Entry"));
        });
    }

    [Test]
    public void Resolve_ReturnsDiagnosticWhenEntryFileDoesNotExist()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteConfig("events/missing.kel");

        var result = CreateResolver().Resolve(fixture.Root, TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.FileOrDirectoryError));
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("KES9002"));
            Assert.That(result.Diagnostics.Single().Message, Does.Contain("Project.Entry"));
            Assert.That(result.Diagnostics.Single().Message, Does.Contain("events/missing.kel"));
        });
    }

    private static RunProjectInputResolver CreateResolver()
    {
        return new RunProjectInputResolver(new ProjectRootResolver(), new ProjectConfigLoader());
    }
}
