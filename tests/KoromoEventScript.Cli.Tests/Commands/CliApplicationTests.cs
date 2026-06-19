using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.Commands.Init;
using KoromoEventScript.Cli.Commands.Correct;
using KoromoEventScript.Cli.Commands.Loc;
using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Tests.Commands;

public class CliApplicationTests
{
    [Test]
    public void Run_RejectsUnsupportedCommandBeforeFileAccess()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = new CliApplication().Run(["run"], output, error, TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.CommandLineError));
            Assert.That(output.ToString(), Is.Empty);
            Assert.That(error.ToString(), Does.Contain("KES9001"));
        });
    }

    [Test]
    public void Run_RejectsInvalidLogFormat()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = new CliApplication().Run(["build", "--check-only", "--log-format", "yaml"], output, error, TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.CommandLineError));
            Assert.That(error.ToString(), Does.Contain("KES9001"));
        });
    }

    [Test]
    public void Run_RejectsDuplicateProjectSources()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = new CliApplication().Run(["build", "ProjectA", "--project", "ProjectB", "--check-only"], output, error, TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.CommandLineError));
            Assert.That(error.ToString(), Does.Contain("project"));
        });
    }

    [Test]
    public void Run_AcceptsWarningsAsErrorsForBuildCheckOnly()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteConfig(entry: "events/main.kel");
        fixture.WriteFile("events/main.kel", """
entry = intro
intro = {
    chapter = "events/main.ke"
}
""");
        fixture.WriteFile("events/main.ke", string.Empty);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = new CliApplication().Run(
            ["build", fixture.Root, "--check-only", "--warnings-as-errors"],
            output,
            error,
            TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.WarningsAsErrors));
            Assert.That(output.ToString(), Is.Empty);
            Assert.That(error.ToString(), Does.Contain("warning KES4001"));
        });
    }

    [Test]
    public void Run_RejectsInitTemplateValueOutsidePublicSpec()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = new CliApplication().Run(
            ["init", "MyGame", "--template", "advanced"],
            output,
            error,
            TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.CommandLineError));
            Assert.That(output.ToString(), Is.Empty);
            Assert.That(error.ToString(), Does.Contain("KES9001"));
            Assert.That(error.ToString(), Does.Contain("template"));
        });
    }

    [Test]
    public void Run_RejectsInitNameOptionWithoutValue()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = new CliApplication().Run(
            ["init", "--name"],
            output,
            error,
            TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.CommandLineError));
            Assert.That(output.ToString(), Is.Empty);
            Assert.That(error.ToString(), Does.Contain("KES9001"));
            Assert.That(error.ToString(), Does.Contain("--name"));
        });
    }

    [Test]
    public void Run_UsesCurrentDirectoryWhenInitProjectDirectoryIsOmitted()
    {
        using var fixture = TemporaryProject.Create();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var initCommand = new RecordingInitCommand();
        var app = new CliApplication(
            new KoromoEventScript.Cli.Commands.Build.BuildCheckOnlyCommand(),
            new KoromoEventScript.Cli.Commands.Build.BuildCommand(),
            new CorrectCommand(),
            initCommand,
            new LocCommand(),
            new DiagnosticSink());

        var exitCode = app.Run(
            ["init"],
            output,
            error,
            fixture.Root);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.Success));
            Assert.That(initCommand.LastOptions, Is.Not.Null);
            Assert.That(initCommand.LastOptions!.ProjectDirectory, Is.Null);
            Assert.That(initCommand.LastCurrentDirectory, Is.EqualTo(fixture.Root));
        });
    }

    [Test]
    public void Run_InitializesProjectAndReportsSuccessToStandardOutput()
    {
        using var fixture = TemporaryProject.Create();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = new CliApplication().Run(
            ["init", "SampleGame"],
            output,
            error,
            fixture.Root);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.Success));
            Assert.That(output.ToString(), Does.Contain("Initialized KES project"));
            Assert.That(error.ToString(), Is.Empty);
            Assert.That(File.Exists(Path.Combine(fixture.Root, "SampleGame", "kes.xml")), Is.True);
        });
    }

    [Test]
    public void Run_OutputsJsonDiagnosticWhenInitFailsWithJsonLogFormat()
    {
        using var fixture = TemporaryProject.Create();
        File.WriteAllText(Path.Combine(fixture.Root, "TakenPath"), "not a directory");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = new CliApplication().Run(
            ["init", "TakenPath", "--log-format", "json"],
            output,
            error,
            fixture.Root);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.FileOrDirectoryError));
            Assert.That(output.ToString(), Is.Empty);
            Assert.That(error.ToString(), Does.Contain("\"code\":\"KES9002\""));
        });
    }

    [Test]
    public void Run_RejectsLocLocaleOptionWithoutValue()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = new CliApplication().Run(
            ["loc", "--locale"],
            output,
            error,
            TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.CommandLineError));
            Assert.That(output.ToString(), Is.Empty);
            Assert.That(error.ToString(), Does.Contain("KES9001"));
            Assert.That(error.ToString(), Does.Contain("--locale"));
        });
    }

    [Test]
    public void Run_RejectsLocOutOptionWithoutValue()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = new CliApplication().Run(
            ["loc", "--out"],
            output,
            error,
            TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.CommandLineError));
            Assert.That(error.ToString(), Does.Contain("KES9001"));
            Assert.That(error.ToString(), Does.Contain("--out"));
        });
    }

    private sealed class RecordingInitCommand : InitCommand
    {
        public InitCommandOptions? LastOptions { get; private set; }

        public string? LastCurrentDirectory { get; private set; }

        public override InitCommandResult Execute(InitCommandOptions options, string currentDirectory)
        {
            LastOptions = options;
            LastCurrentDirectory = currentDirectory;
            return new InitCommandResult(CliExitCode.Success, [], "initialized", currentDirectory);
        }
    }
}
