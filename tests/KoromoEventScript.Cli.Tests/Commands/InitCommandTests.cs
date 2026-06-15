using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.Commands.Build;
using KoromoEventScript.Cli.Commands.Init;

namespace KoromoEventScript.Cli.Tests.Commands;

public class InitCommandTests
{
    [Test]
    public void Execute_CreatesBasicProjectAndReturnsSuccessMessage()
    {
        using var fixture = TemporaryProject.Create();
        var command = new InitCommand();
        var options = new InitCommandOptions(
            ProjectDirectory: "SampleGame",
            ProjectName: "SampleGame",
            Template: InitTemplate.Basic,
            Force: false,
            NoSample: false,
            OutputFormat: KoromoEventScript.Cli.Diagnostics.DiagnosticOutputFormat.Text);

        var result = command.Execute(options, fixture.Root);

        var projectRoot = Path.Combine(fixture.Root, "SampleGame");
        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.Success));
            Assert.That(result.SuccessMessage, Does.Contain("SampleGame"));
            Assert.That(result.ProjectRoot, Is.EqualTo(projectRoot));
            Assert.That(File.Exists(Path.Combine(projectRoot, "kes.xml")), Is.True);
            Assert.That(File.Exists(Path.Combine(projectRoot, "events", "main.kel")), Is.True);
            Assert.That(File.Exists(Path.Combine(projectRoot, "events", "chapter001.kc")), Is.True);
        });
    }

    [Test]
    public void Execute_ReturnsFailureWithoutSuccessMessageWhenWriterFails()
    {
        using var fixture = TemporaryProject.Create();
        File.WriteAllText(Path.Combine(fixture.Root, "SampleGame"), "not a directory");
        var command = new InitCommand();
        var options = new InitCommandOptions(
            ProjectDirectory: "SampleGame",
            ProjectName: "SampleGame",
            Template: InitTemplate.Basic,
            Force: false,
            NoSample: false,
            OutputFormat: KoromoEventScript.Cli.Diagnostics.DiagnosticOutputFormat.Text);

        var result = command.Execute(options, fixture.Root);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.FileOrDirectoryError));
            Assert.That(result.SuccessMessage, Is.Null);
            Assert.That(result.Diagnostics, Is.Not.Empty);
        });
    }

    [Test]
    public void Execute_GeneratedBasicProjectPassesBuildCheckOnly()
    {
        using var fixture = TemporaryProject.Create();
        var command = new InitCommand();
        var options = new InitCommandOptions(
            ProjectDirectory: "PlayableGame",
            ProjectName: "PlayableGame",
            Template: InitTemplate.Basic,
            Force: false,
            NoSample: false,
            OutputFormat: KoromoEventScript.Cli.Diagnostics.DiagnosticOutputFormat.Text);

        var initResult = command.Execute(options, fixture.Root);
        var buildResult = new BuildCheckOnlyCommand().Execute(
            new BuildCommandOptions(initResult.ProjectRoot, KoromoEventScript.Cli.Diagnostics.DiagnosticOutputFormat.Text),
            fixture.Root);

        Assert.Multiple(() =>
        {
            Assert.That(initResult.ExitCode, Is.EqualTo(CliExitCode.Success));
            Assert.That(buildResult.ExitCode, Is.EqualTo(CliExitCode.Success));
            Assert.That(buildResult.Diagnostics, Is.Empty);
        });
    }
}
