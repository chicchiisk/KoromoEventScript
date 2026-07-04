using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.Commands.Init;
using KoromoEventScript.Cli.Commands.Correct;
using KoromoEventScript.Cli.Commands.Loc;
using KoromoEventScript.Cli.Commands.Run;
using KoromoEventScript.Cli.Diagnostics;
using System.Reflection;

namespace KoromoEventScript.Cli.Tests.Commands;

public class CliApplicationTests
{
    [TestCase("--version")]
    [TestCase("-v")]
    public void Run_PrintsVersion(string versionOption)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = new CliApplication().Run([versionOption], output, error, TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.Success));
            Assert.That(output.ToString(), Is.EqualTo($"kes 0.1.0{Environment.NewLine}"));
            Assert.That(error.ToString(), Is.Empty);
        });
    }

    [TestCase("--help")]
    [TestCase("-h")]
    public void Run_PrintsTopLevelHelp(string helpOption)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = new CliApplication().Run([helpOption], output, error, TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.Success));
            Assert.That(output.ToString(), Does.Contain("Usage:"));
            Assert.That(output.ToString(), Does.Contain("kes <COMMAND> [-h|--help]"));
            Assert.That(output.ToString(), Does.Contain("Commands:"));
            Assert.That(error.ToString(), Is.Empty);
        });
    }

    [TestCase("build", "kes build [PROJECT_DIR] [options]")]
    [TestCase("clean", "kes clean [PROJECT_DIR] [options]")]
    [TestCase("correct", "kes correct [PROJECT_DIR] [options]")]
    [TestCase("init", "kes init [PROJECT_DIR] [options]")]
    [TestCase("loc", "kes loc [PROJECT_DIR] [options]")]
    [TestCase("publish", "kes publish [PROJECT_DIR] [options]")]
    [TestCase("run", "kes run [PROJECT_DIR] [options]")]
    public void Run_PrintsCommandHelpBeforeExecutingCommand(string command, string expectedUsage)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = new CliApplication().Run([command, "--help", "--unsupported"], output, error, TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.Success));
            Assert.That(output.ToString(), Does.Contain(expectedUsage));
            Assert.That(output.ToString(), Does.Contain("Options:"));
            Assert.That(output.ToString(), Does.Contain("Examples:"));
            Assert.That(error.ToString(), Is.Empty);
        });
    }

    [Test]
    public void Run_DoesNotTreatHelpAfterRunSeparatorAsCliHelp()
    {
        var result = ParseCli(["run", "--", "--help"]);

        var options = GetRunOptions(result);

        Assert.Multiple(() =>
        {
            Assert.That(GetDiagnostics(result), Is.Empty);
            Assert.That(options, Is.Not.Null);
            Assert.That(options!.RuntimeArguments, Is.EqualTo(new[] { "--help" }));
        });
    }

    [Test]
    public void Run_RejectsUnsupportedCommandBeforeFileAccess()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = new CliApplication().Run(["unknown"], output, error, TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.CommandLineError));
            Assert.That(output.ToString(), Is.Empty);
            Assert.That(error.ToString(), Does.Contain("KES9001"));
        });
    }

    [Test]
    public void Run_RejectsLogFormatOption()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = new CliApplication().Run(["build", "--check-only", "--log-format", "json"], output, error, TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.CommandLineError));
            Assert.That(output.ToString(), Is.Empty);
            Assert.That(error.ToString(), Does.Contain("KES9001"));
            Assert.That(error.ToString(), Does.Contain("Unsupported option '--log-format'"));
        });
    }

    [Test]
    public void Run_RejectsTxtIlWhenCheckOnlyIsSpecified()
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
            ["build", fixture.Root, "--check-only", "--txt-il"],
            output,
            error,
            TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.CommandLineError));
            Assert.That(error.ToString(), Does.Contain("--txt-il"));
            Assert.That(error.ToString(), Does.Contain("--check-only"));
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
    public void Run_AcceptsVerboseAsCommonCommandOption()
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
            ["build", fixture.Root, "--check-only", "--verbose"],
            output,
            error,
            TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.Success));
            Assert.That(output.ToString(), Is.Empty);
            Assert.That(error.ToString(), Does.Contain("warning KES4001"));
            Assert.That(error.ToString(), Does.Not.Contain("KES9001"));
        });
    }

    [Test]
    public void Run_AcceptsBuildOutDirOption()
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
            ["build", fixture.Root, "--check-only", "--out-dir", "custom-build"],
            output,
            error,
            TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.Success));
            Assert.That(output.ToString(), Is.Empty);
            Assert.That(error.ToString(), Does.Contain("warning KES4001"));
            Assert.That(error.ToString(), Does.Not.Contain("KES9001"));
        });
    }

    [Test]
    public void Run_RejectsBuildNoIncrementalOption()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = new CliApplication().Run(
            ["build", "--no-incremental"],
            output,
            error,
            TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.CommandLineError));
            Assert.That(output.ToString(), Is.Empty);
            Assert.That(error.ToString(), Does.Contain("KES9001"));
            Assert.That(error.ToString(), Does.Contain("Unsupported option '--no-incremental'"));
        });
    }

    [Test]
    public void Run_AcceptsBuildLocaleOptionDuringCheckOnlyValidation()
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
            ["build", fixture.Root, "--check-only", "--loc", "en"],
            output,
            error,
            TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.Success));
            Assert.That(output.ToString(), Is.Empty);
            Assert.That(error.ToString(), Does.Contain("warning KES4001"));
            Assert.That(error.ToString(), Does.Not.Contain("KES9001"));
        });
    }

    [Test]
    public void Run_RejectsBuildOutDirOptionWithoutValue()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = new CliApplication().Run(
            ["build", "--out-dir"],
            output,
            error,
            TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.CommandLineError));
            Assert.That(error.ToString(), Does.Contain("KES9001"));
            Assert.That(error.ToString(), Does.Contain("--out-dir"));
        });
    }

    [Test]
    public void Run_RejectsBuildLocOptionWithoutValue()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = new CliApplication().Run(
            ["build", "--loc"],
            output,
            error,
            TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.CommandLineError));
            Assert.That(error.ToString(), Does.Contain("KES9001"));
            Assert.That(error.ToString(), Does.Contain("--loc"));
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
    public void Run_RejectsLogFormatOptionBeforeInitFileAccess()
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
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.CommandLineError));
            Assert.That(output.ToString(), Is.Empty);
            Assert.That(error.ToString(), Does.Contain("Unsupported option '--log-format'"));
            Assert.That(error.ToString(), Does.Not.Contain("KES9002"));
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

    [TestCase(new[] { "run" }, "windows")]
    [TestCase(new[] { "run", "--target", "windows" }, "windows")]
    [TestCase(new[] { "run", "--target", "Windows" }, "windows")]
    public void ParseRun_AcceptsWindowsTargetAndDefaultTarget(string[] args, string expectedTarget)
    {
        var result = ParseCli(args);

        var options = GetRunOptions(result);

        Assert.Multiple(() =>
        {
            Assert.That(GetDiagnostics(result), Is.Empty);
            Assert.That(options, Is.Not.Null);
            Assert.That(options!.Target, Is.EqualTo(expectedTarget));
            Assert.That(options.BuildMode, Is.EqualTo(RunBuildMode.IfStale));
        });
    }

    [Test]
    public void ParseRun_AcceptsBuildAndNoBuildModes()
    {
        var buildResult = ParseCli(["run", "--build"]);
        var noBuildResult = ParseCli(["run", "--no-build"]);

        Assert.Multiple(() =>
        {
            Assert.That(GetRunOptions(buildResult)!.BuildMode, Is.EqualTo(RunBuildMode.Always));
            Assert.That(GetRunOptions(noBuildResult)!.BuildMode, Is.EqualTo(RunBuildMode.Never));
        });
    }

    [Test]
    public void ParseRun_RejectsUnsupportedTarget()
    {
        var result = ParseCli(["run", "--target", "unity"]);

        var diagnostics = GetDiagnostics(result);

        Assert.Multiple(() =>
        {
            Assert.That(GetRunOptions(result), Is.Null);
            Assert.That(diagnostics, Has.Count.EqualTo(1));
            Assert.That(diagnostics[0].Code, Is.EqualTo("KES9001"));
            Assert.That(diagnostics[0].Message, Does.Contain("Unsupported --target value 'unity'"));
        });
    }

    [Test]
    public void ParseRun_RejectsBuildAndNoBuildTogether()
    {
        var result = ParseCli(["run", "--build", "--no-build"]);

        var diagnostics = GetDiagnostics(result);

        Assert.Multiple(() =>
        {
            Assert.That(GetRunOptions(result), Is.Null);
            Assert.That(diagnostics, Has.Count.EqualTo(1));
            Assert.That(diagnostics[0].Code, Is.EqualTo("KES9001"));
            Assert.That(diagnostics[0].Message, Does.Contain("--build"));
            Assert.That(diagnostics[0].Message, Does.Contain("--no-build"));
        });
    }

    [Test]
    public void ParseRun_RejectsManifestOption()
    {
        var result = ParseCli(["run", "--manifest", "build/windows/manifest.json"]);

        var diagnostics = GetDiagnostics(result);

        Assert.Multiple(() =>
        {
            Assert.That(GetRunOptions(result), Is.Null);
            Assert.That(diagnostics, Has.Count.EqualTo(1));
            Assert.That(diagnostics[0].Code, Is.EqualTo("KES9001"));
            Assert.That(diagnostics[0].Message, Does.Contain("--manifest"));
        });
    }

    [Test]
    public void ParseRun_PreservesRuntimeArgumentsAfterSeparator()
    {
        var result = ParseCli(["run", "ProjectA", "--debug", "--", "--target", "unity", "--build", "value"]);

        var options = GetRunOptions(result);

        Assert.Multiple(() =>
        {
            Assert.That(GetDiagnostics(result), Is.Empty);
            Assert.That(options, Is.Not.Null);
            Assert.That(options!.ProjectDirectory, Is.EqualTo("ProjectA"));
            Assert.That(options.Debug, Is.True);
            Assert.That(options.RuntimeArguments, Is.EqualTo(new[] { "--target", "unity", "--build", "value" }));
        });
    }

    private static object ParseCli(string[] args)
    {
        var parse = typeof(CliApplication).GetMethod("Parse", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(parse, Is.Not.Null);
        return parse!.Invoke(null, [args])!;
    }

    private static RunCommandOptions? GetRunOptions(object parseResult)
    {
        return (RunCommandOptions?)parseResult.GetType().GetProperty("RunOptions")!.GetValue(parseResult);
    }

    private static IReadOnlyList<Diagnostic> GetDiagnostics(object parseResult)
    {
        return (IReadOnlyList<Diagnostic>)parseResult.GetType().GetProperty("Diagnostics")!.GetValue(parseResult)!;
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
