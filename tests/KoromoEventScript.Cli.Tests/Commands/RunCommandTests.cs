using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.Commands.Build;
using KoromoEventScript.Cli.Commands.Correct;
using KoromoEventScript.Cli.Commands.Init;
using KoromoEventScript.Cli.Commands.Loc;
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
    public void Run_WithExplicitManifestSkipsProjectBuildAndPropagatesExitCode()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteFile("custom/manifest.json", "{}");
        var manifestPath = Path.Combine(fixture.Root, "custom", "manifest.json");
        using var output = new StringWriter();
        using var error = new StringWriter();
        var launcher = new RecordingProcessLauncher(exitCode: 5);
        var command = new RunCommand(
            new KoromoEventScript.Cli.Build.BuildPipelineService(),
            new KoromoEventScript.Cli.ProjectSystem.ProjectRootResolver(),
            new KoromoEventScript.Cli.ProjectSystem.ProjectConfigLoader(),
            launcher,
            () => "RuntimeStub.exe");

        var result = command.Execute(new RunCommandOptions(
            ProjectDirectory: null,
            OutputFormat: DiagnosticOutputFormat.Text,
            ManifestPath: manifestPath,
            Debug: true),
            fixture.Root);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(5));
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(launcher.LastRequest!.Arguments, Is.EqualTo(new[] { "--manifest", manifestPath, "--debug" }));
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
