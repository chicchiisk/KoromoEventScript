using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.Build;
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
        fixture.WriteFile("events/main.kel", "entry");
        WriteValidRunArtifacts(fixture);
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
        fixture.WriteFile("events/main.kel", "entry");
        WriteValidRunArtifacts(fixture);
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
        fixture.WriteFile("events/main.kel", "entry");
        WriteValidRunArtifacts(fixture);
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
    public void Execute_WithNoBuildStopsBeforeRuntimeWhenManifestIsInvalid()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteConfig(entry: "events/main.kel");
        fixture.WriteFile("events/main.kel", "entry");
        fixture.WriteFile("build/windows/manifest.json", "{}");
        var launcher = new RecordingProcessLauncher(exitCode: 0);
        var runCommand = new RunCommand(
            new KoromoEventScript.Cli.Build.BuildPipelineService(),
            new KoromoEventScript.Cli.ProjectSystem.ProjectRootResolver(),
            new KoromoEventScript.Cli.ProjectSystem.ProjectConfigLoader(),
            launcher,
            () => "RuntimeStub.exe");
        var options = new RunCommandOptions(
            ProjectDirectory: fixture.Root,
            OutputFormat: DiagnosticOutputFormat.Text,
            BuildMode: RunBuildMode.Never);

        var result = runCommand.Execute(options, TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo((int)CliExitCode.FileOrDirectoryError));
            Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Code), Does.Contain("KES9003"));
            Assert.That(launcher.LastRequest, Is.Null);
        });
    }

    [Test]
    public void Execute_WithFreshArtifactsSkipsBuildAndLaunchesRuntime()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteConfig(entry: "events/main.kel");
        fixture.WriteFile("events/main.kel", "entry");
        fixture.WriteFile("events/main.kc", "say \"hello\"");
        WriteValidRunArtifacts(fixture);
        SetRunInputTimes(fixture, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        SetRunArtifactTimes(fixture, new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero));
        var launcher = new RecordingProcessLauncher(exitCode: 13);
        var runCommand = new RunCommand(
            new KoromoEventScript.Cli.Build.BuildPipelineService(),
            new KoromoEventScript.Cli.ProjectSystem.ProjectRootResolver(),
            new KoromoEventScript.Cli.ProjectSystem.ProjectConfigLoader(),
            launcher,
            () => "RuntimeStub.exe");
        var options = new RunCommandOptions(
            ProjectDirectory: fixture.Root,
            OutputFormat: DiagnosticOutputFormat.Text,
            BuildMode: RunBuildMode.IfStale);

        var result = runCommand.Execute(options, TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(13));
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(launcher.LastRequest, Is.Not.Null);
            Assert.That(launcher.LastRequest!.Arguments.Take(2), Is.EqualTo(new[]
            {
                "--manifest",
                Path.Combine(fixture.Root, "build", "windows", "manifest.json"),
            }));
        });
    }

    [Test]
    public void Execute_WithBuildAlwaysBuildsThenValidatesAndLaunchesRuntime()
    {
        using var fixture = TemporaryProject.Create();
        WriteBuildableProject(fixture);
        var launcher = new RecordingProcessLauncher(exitCode: 0);
        var runCommand = new RunCommand(
            new KoromoEventScript.Cli.Build.BuildPipelineService(),
            new KoromoEventScript.Cli.ProjectSystem.ProjectRootResolver(),
            new KoromoEventScript.Cli.ProjectSystem.ProjectConfigLoader(),
            launcher,
            () => "RuntimeStub.exe");
        var options = new RunCommandOptions(
            ProjectDirectory: fixture.Root,
            OutputFormat: DiagnosticOutputFormat.Text,
            BuildMode: RunBuildMode.Always);

        var result = runCommand.Execute(options, TestContext.CurrentContext.WorkDirectory);

        var expectedManifest = Path.Combine(fixture.Root, "build", "windows", "manifest.json");
        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(File.Exists(expectedManifest), Is.True);
            Assert.That(Directory.EnumerateFiles(Path.Combine(fixture.Root, "build", "windows"), "*.klib", SearchOption.AllDirectories), Is.Not.Empty);
            Assert.That(launcher.LastRequest, Is.Not.Null);
            Assert.That(launcher.LastRequest!.Arguments.Take(2), Is.EqualTo(new[] { "--manifest", expectedManifest }));
        });
    }

    [Test]
    public void Execute_WithStaleArtifactsBuildsThenValidatesAndLaunchesRuntime()
    {
        using var fixture = TemporaryProject.Create();
        WriteBuildableProject(fixture);
        var launcher = new RecordingProcessLauncher(exitCode: 0);
        var runCommand = new RunCommand(
            new KoromoEventScript.Cli.Build.BuildPipelineService(),
            new KoromoEventScript.Cli.ProjectSystem.ProjectRootResolver(),
            new KoromoEventScript.Cli.ProjectSystem.ProjectConfigLoader(),
            launcher,
            () => "RuntimeStub.exe");
        var options = new RunCommandOptions(
            ProjectDirectory: fixture.Root,
            OutputFormat: DiagnosticOutputFormat.Text,
            BuildMode: RunBuildMode.IfStale);

        var result = runCommand.Execute(options, TestContext.CurrentContext.WorkDirectory);

        var expectedManifest = Path.Combine(fixture.Root, "build", "windows", "manifest.json");
        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(File.Exists(expectedManifest), Is.True);
            Assert.That(Directory.EnumerateFiles(Path.Combine(fixture.Root, "build", "windows"), "*.klib", SearchOption.AllDirectories), Is.Not.Empty);
            Assert.That(launcher.LastRequest, Is.Not.Null);
            Assert.That(launcher.LastRequest!.Arguments.Take(2), Is.EqualTo(new[] { "--manifest", expectedManifest }));
        });
    }

    [Test]
    public void Execute_WhenBuildFailsReturnsBuildDiagnosticsAndDoesNotLaunchRuntime()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteConfig(entry: "events/main.kel");
        fixture.WriteFile("events/main.kel", """
entry = intro
intro = {
    chapter = "events/main.kc"
}
""");
        fixture.WriteFile("events/main.kc", "say");
        var launcher = new RecordingProcessLauncher(exitCode: 0);
        var runCommand = new RunCommand(
            new KoromoEventScript.Cli.Build.BuildPipelineService(),
            new KoromoEventScript.Cli.ProjectSystem.ProjectRootResolver(),
            new KoromoEventScript.Cli.ProjectSystem.ProjectConfigLoader(),
            launcher,
            () => "RuntimeStub.exe");
        var options = new RunCommandOptions(
            ProjectDirectory: fixture.Root,
            OutputFormat: DiagnosticOutputFormat.Text,
            BuildMode: RunBuildMode.Always);

        var result = runCommand.Execute(options, TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo((int)CliExitCode.SyntaxError));
            Assert.That(result.Diagnostics, Is.Not.Empty);
            Assert.That(launcher.LastRequest, Is.Null);
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

    private static void WriteValidRunArtifacts(TemporaryProject fixture)
    {
        fixture.WriteFile("build/windows/scripts/main.klib", "klib");
        var manifestPath = Path.Combine(fixture.Root, "build", "windows", "manifest.json");
        var document = new BuildManifestDocument(
            "1.2.3",
            "windows",
            "events/main.kel",
            [new BuildManifestInputFile("events/main.kel", "entry")],
            [new BuildManifestScriptArtifact("events/main.kc", "scripts/main.klib", null, "events/main", "ja-JP", true, null)],
            []);
        var result = new BuildManifestWriter().Write(manifestPath, document);
        Assert.That(result.Succeeded, Is.True);
    }

    private static void WriteBuildableProject(TemporaryProject fixture)
    {
        fixture.WriteConfig(entry: "events/main.kel");
        fixture.WriteFile("events/main.kel", """
entry = intro
intro = {
    chapter = "events/main.kc"
}
""");
        fixture.WriteFile("events/main.kc", """
actor Hero:
    var faceName: string = "normal"

standby:
    hero : Hero

say hero #sy_main_0001:
    hello
""");
    }

    private static void SetRunInputTimes(TemporaryProject fixture, DateTimeOffset timestamp)
    {
        File.SetLastWriteTimeUtc(Path.Combine(fixture.Root, "kes.xml"), timestamp.UtcDateTime);
        File.SetLastWriteTimeUtc(Path.Combine(fixture.Root, "events", "main.kel"), timestamp.UtcDateTime);
        File.SetLastWriteTimeUtc(Path.Combine(fixture.Root, "events", "main.kc"), timestamp.UtcDateTime);
    }

    private static void SetRunArtifactTimes(TemporaryProject fixture, DateTimeOffset timestamp)
    {
        File.SetLastWriteTimeUtc(Path.Combine(fixture.Root, "build", "windows", "manifest.json"), timestamp.UtcDateTime);
        File.SetLastWriteTimeUtc(Path.Combine(fixture.Root, "build", "windows", "scripts", "main.klib"), timestamp.UtcDateTime);
    }
}
