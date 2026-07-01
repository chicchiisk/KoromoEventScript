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
        var launcher = new RecordingProcessLauncher(exitCode: 7);
        var runCommand = new RunCommand(
            new KoromoEventScript.Cli.Build.BuildPipelineService(),
            new KoromoEventScript.Cli.ProjectSystem.ProjectRootResolver(),
            new KoromoEventScript.Cli.ProjectSystem.ProjectConfigLoader(),
            launcher,
            () => "RuntimeStub.exe");
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
            Assert.That(launcher.LastRequest!.FileName, Is.EqualTo("RuntimeStub.exe"));
            Assert.That(launcher.LastRequest.WorkingDirectory, Is.EqualTo(Path.GetDirectoryName(expectedManifest)));
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

}
