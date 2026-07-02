using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.Commands.Build;
using KoromoEventScript.Cli.Commands.Correct;
using KoromoEventScript.Cli.Commands.Init;
using KoromoEventScript.Cli.Commands.Loc;
using KoromoEventScript.Cli.Commands.Publish;
using KoromoEventScript.Cli.Commands.Run;
using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Tests.Commands;

public sealed class RunCommandTests
{
    [Test]
    public void Run_WithNoBuildLaunchesWindowsRuntimeWithRequestedArguments()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteConfig(entry: "events/main.kel");
        fixture.WriteFile("build/windows/manifest.json", "{}");
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runtimePath = Path.Combine(fixture.Root, "runtime", "RuntimeStub.exe");
        var launcher = new RecordingProcessLauncher(exitCode: 7);
        var runCommand = new RunCommand(
            new KoromoEventScript.Cli.Build.BuildPipelineService(),
            new KoromoEventScript.Cli.ProjectSystem.ProjectRootResolver(),
            new KoromoEventScript.Cli.ProjectSystem.ProjectConfigLoader(),
            launcher,
            () => runtimePath);
        var app = new CliApplication(
            new BuildCheckOnlyCommand(),
            new BuildCommand(),
            new CorrectCommand(),
            new InitCommand(),
            new LocCommand(),
            new WindowsPublishCommand(),
            runCommand,
            new DiagnosticSink());

        var exitCode = app.Run(
            [
                "run",
                fixture.Root,
                "--no-build",
                "--locale",
                "ja-JP",
                "--start",
                "chapter002:start",
                "--fullscreen",
                "--width",
                "1600",
                "--height",
                "900",
                "--debug",
                "--profile",
                "--",
                "--trace-frame",
            ],
            output,
            error,
            TestContext.CurrentContext.WorkDirectory);

        var expectedManifest = Path.Combine(fixture.Root, "build", "windows", "manifest.json");
        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(7));
            Assert.That(output.ToString(), Is.Empty);
            Assert.That(error.ToString(), Is.Empty);
            Assert.That(launcher.LastRequest, Is.Not.Null);
            Assert.That(launcher.LastRequest!.FileName, Is.EqualTo(runtimePath));
            Assert.That(launcher.LastRequest.WorkingDirectory, Is.EqualTo(Path.GetDirectoryName(runtimePath)));
            Assert.That(launcher.LastRequest.Arguments, Is.EqualTo(new[]
            {
                "--manifest",
                expectedManifest,
                "--locale",
                "ja-JP",
                "--start",
                "chapter002:start",
                "--fullscreen",
                "--width",
                "1600",
                "--height",
                "900",
                "--debug",
                "--profile",
                "--trace-frame",
            }));
        });
    }

    [Test]
    public void Run_WithCsprojRuntimePathLaunchesThroughDotnetRun()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteConfig(entry: "events/main.kel");
        fixture.WriteFile("build/windows/manifest.json", "{}");
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runtimeProjectPath = Path.Combine(fixture.Root, "source", "runtime", "KoromoEventScript.Runtime.Windows", "KoromoEventScript.Runtime.Windows.csproj");
        var launcher = new RecordingProcessLauncher(exitCode: 0);
        var runCommand = new RunCommand(
            new KoromoEventScript.Cli.Build.BuildPipelineService(),
            new KoromoEventScript.Cli.ProjectSystem.ProjectRootResolver(),
            new KoromoEventScript.Cli.ProjectSystem.ProjectConfigLoader(),
            launcher,
            () => runtimeProjectPath);
        var app = new CliApplication(
            new BuildCheckOnlyCommand(),
            new BuildCommand(),
            new CorrectCommand(),
            new InitCommand(),
            new LocCommand(),
            new WindowsPublishCommand(),
            runCommand,
            new DiagnosticSink());

        var exitCode = app.Run(
            [
                "run",
                fixture.Root,
                "--no-build",
                "--start",
                "tag with space",
                "--",
                "",
                "plain",
                "with space",
                """quote"inside""",
                @"C:\assets\",
            ],
            output,
            error,
            TestContext.CurrentContext.WorkDirectory);

        var expectedManifest = Path.Combine(fixture.Root, "build", "windows", "manifest.json");
        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(output.ToString(), Is.Empty);
            Assert.That(error.ToString(), Is.Empty);
            Assert.That(launcher.LastRequest, Is.Not.Null);
            Assert.That(launcher.LastRequest!.FileName, Is.EqualTo("dotnet"));
            Assert.That(launcher.LastRequest.WorkingDirectory, Is.EqualTo(Path.GetDirectoryName(runtimeProjectPath)));
            Assert.That(launcher.LastRequest.Arguments, Is.EqualTo(new[]
            {
                "run",
                "--project",
                runtimeProjectPath,
                "--no-launch-profile",
                "--",
                "--args",
                $@"""--manifest"" ""{expectedManifest}"" ""--start"" ""tag with space"" """" ""plain"" ""with space"" ""quote\""inside"" ""C:\assets\\""",
            }));
        });
    }

    [Test]
    public void Execute_WhenRuntimeLaunchThrowsReturnsRuntimeLaunchErrorDiagnostic()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteConfig(entry: "events/main.kel");
        var runCommand = new RunCommand(
            new KoromoEventScript.Cli.Build.BuildPipelineService(),
            new KoromoEventScript.Cli.ProjectSystem.ProjectRootResolver(),
            new KoromoEventScript.Cli.ProjectSystem.ProjectConfigLoader(),
            new ThrowingProcessLauncher(new System.ComponentModel.Win32Exception("simulated launch failure")),
            () => "MissingRuntime.exe");
        var options = new RunCommandOptions(
            ProjectDirectory: fixture.Root,
            OutputFormat: DiagnosticOutputFormat.Text,
            BuildMode: RunBuildMode.Never);

        var result = runCommand.Execute(options, TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo((int)CliExitCode.RuntimeLaunchError));
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("KES9004"));
            Assert.That(result.Diagnostics.Single().Message, Does.Contain("Could not launch Windows runtime"));
            Assert.That(result.Diagnostics.Single().Message, Does.Contain("simulated launch failure"));
        });
    }

    [Test]
    public void RunCommandOptions_UsesProjectFirstBuildModeModel()
    {
        var runtimeArguments = new[] { "--trace-frame", "--seed", "42" };
        var options = new RunCommandOptions(
            ProjectDirectory: "sample-project",
            OutputFormat: DiagnosticOutputFormat.Text,
            Target: "windows",
            BuildMode: RunBuildMode.Always,
            RuntimeArguments: runtimeArguments);

        Assert.Multiple(() =>
        {
            Assert.That(options.ProjectDirectory, Is.EqualTo("sample-project"));
            Assert.That(options.Target, Is.EqualTo("windows"));
            Assert.That(options.BuildMode, Is.EqualTo(RunBuildMode.Always));
            Assert.That(options.RuntimeArguments, Is.EqualTo(runtimeArguments));
        });
    }

    [Test]
    public void CliExitCode_AssignsRuntimeLaunchErrorWithoutChangingExistingValues()
    {
        Assert.Multiple(() =>
        {
            Assert.That((int)CliExitCode.Success, Is.EqualTo(0));
            Assert.That((int)CliExitCode.CommandLineError, Is.EqualTo(2));
            Assert.That((int)CliExitCode.FileOrDirectoryError, Is.EqualTo(6));
            Assert.That((int)CliExitCode.RuntimeLaunchError, Is.EqualTo(7));
            Assert.That((int)CliExitCode.WarningsAsErrors, Is.EqualTo(9));
        });
    }

    private sealed class RecordingProcessLauncher : IProcessLauncher
    {
        private readonly int exitCode;

        public RecordingProcessLauncher(int exitCode)
        {
            this.exitCode = exitCode;
        }

        public ProcessLaunchRequest? LastRequest { get; private set; }

        public int Launch(ProcessLaunchRequest request)
        {
            LastRequest = request;
            return exitCode;
        }
    }

    private sealed class ThrowingProcessLauncher : IProcessLauncher
    {
        private readonly Exception exception;

        public ThrowingProcessLauncher(Exception exception)
        {
            this.exception = exception;
        }

        public int Launch(ProcessLaunchRequest request)
        {
            throw exception;
        }
    }

}
