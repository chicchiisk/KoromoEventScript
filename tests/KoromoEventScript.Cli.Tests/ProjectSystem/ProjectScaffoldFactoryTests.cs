using KoromoEventScript.Cli.Commands.Init;
using KoromoEventScript.Cli.ProjectSystem;

namespace KoromoEventScript.Cli.Tests.ProjectSystem;

public class ProjectScaffoldFactoryTests
{
    [Test]
    public void Create_BasicTemplateIncludesStandardStructureAndSampleFiles()
    {
        var options = new InitCommandOptions(
            ProjectDirectory: "MyGame",
            ProjectName: "CustomName",
            Template: InitTemplate.Basic,
            Force: false,
            NoSample: false,
            OutputFormat: KoromoEventScript.Cli.Diagnostics.DiagnosticOutputFormat.Text);

        var scaffold = new ProjectScaffoldFactory().Create(options, "D:\\Projects\\MyGame");

        Assert.Multiple(() =>
        {
            Assert.That(scaffold.ResolvedProjectName, Is.EqualTo("CustomName"));
            Assert.That(scaffold.Directories, Does.Contain("events"));
            Assert.That(scaffold.Directories, Does.Contain("assets/bg"));
            Assert.That(scaffold.Directories, Does.Contain("assets/actor"));
            Assert.That(scaffold.Directories, Does.Contain("assets/voice"));
            Assert.That(scaffold.Directories, Does.Contain("assets/se"));
            Assert.That(scaffold.Directories, Does.Contain("assets/bgm"));
            Assert.That(scaffold.Directories, Does.Contain("locale"));
            Assert.That(scaffold.Directories, Does.Contain("build"));
            Assert.That(scaffold.Directories, Does.Contain("dist"));
            Assert.That(scaffold.Files.Select(static file => file.RelativePath), Does.Contain("kes.xml"));
            Assert.That(scaffold.Files.Select(static file => file.RelativePath), Does.Contain("events/main.kel"));
            Assert.That(scaffold.Files.Select(static file => file.RelativePath), Does.Contain("events/chapter001.kc"));
        });

        var config = scaffold.Files.Single(static file => file.RelativePath == "kes.xml").Contents;
        var mainKel = scaffold.Files.Single(static file => file.RelativePath == "events/main.kel").Contents;

        Assert.Multiple(() =>
        {
            Assert.That(config, Does.Contain("Name=\"CustomName\""));
            Assert.That(config, Does.Contain("Entry=\"events/main.kel\""));
            Assert.That(mainKel, Does.Contain("events/chapter001.kc"));
        });
    }

    [Test]
    public void Create_UsesDirectoryNameWhenProjectNameIsOmitted()
    {
        var options = new InitCommandOptions(
            ProjectDirectory: null,
            ProjectName: null,
            Template: InitTemplate.Basic,
            Force: false,
            NoSample: false,
            OutputFormat: KoromoEventScript.Cli.Diagnostics.DiagnosticOutputFormat.Text);

        var scaffold = new ProjectScaffoldFactory().Create(options, "D:\\Projects\\CurrentGame");

        Assert.That(scaffold.ResolvedProjectName, Is.EqualTo("CurrentGame"));
        Assert.That(scaffold.Files.Single(static file => file.RelativePath == "kes.xml").Contents, Does.Contain("Name=\"CurrentGame\""));
    }

    [Test]
    public void Create_EmptyTemplateAndNoSampleDoNotIncludeEventSamples()
    {
        var emptyOptions = new InitCommandOptions(
            ProjectDirectory: "EmptyGame",
            ProjectName: null,
            Template: InitTemplate.Empty,
            Force: false,
            NoSample: false,
            OutputFormat: KoromoEventScript.Cli.Diagnostics.DiagnosticOutputFormat.Text);
        var noSampleOptions = emptyOptions with { Template = InitTemplate.Basic, NoSample = true };

        var emptyScaffold = new ProjectScaffoldFactory().Create(emptyOptions, "D:\\Projects\\EmptyGame");
        var noSampleScaffold = new ProjectScaffoldFactory().Create(noSampleOptions, "D:\\Projects\\NoSampleGame");

        Assert.Multiple(() =>
        {
            Assert.That(emptyScaffold.Files.Select(static file => file.RelativePath), Does.Not.Contain("events/main.kel"));
            Assert.That(emptyScaffold.Files.Select(static file => file.RelativePath), Does.Not.Contain("events/chapter001.kc"));
            Assert.That(noSampleScaffold.Files.Select(static file => file.RelativePath), Does.Not.Contain("events/main.kel"));
            Assert.That(noSampleScaffold.Files.Select(static file => file.RelativePath), Does.Not.Contain("events/chapter001.kc"));
            Assert.That(emptyScaffold.Directories, Does.Contain("events"));
            Assert.That(noSampleScaffold.Directories, Does.Contain("events"));
        });
    }
}
