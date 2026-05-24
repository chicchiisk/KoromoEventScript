using System.Text.Json;
using System.Diagnostics;
using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.Commands.Build;
using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Tests.Commands;

public class BuildCheckOnlyCommandTests
{
    [Test]
    public void Run_ReturnsSuccessForMinimalProjectWithoutEmittingDiagnostics()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var projectRoot = GetTestDataPath("projects", "minimal");

        var exitCode = new CliApplication().Run(["build", projectRoot, "--check-only"], output, error, TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.Success));
            Assert.That(output.ToString(), Is.Empty);
            Assert.That(error.ToString(), Is.Empty);
        });
    }

    [Test]
    public void Execute_ReturnsFileErrorWhenEntryIsMissing()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteConfig(entry: "events/missing.kel");

        var result = new BuildCheckOnlyCommand().Execute(new BuildCommandOptions(fixture.Root, DiagnosticOutputFormat.Text), TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.FileOrDirectoryError));
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("KES9004"));
        });
    }

    [Test]
    public void Run_OutputsJsonLinesDiagnosticsWhenRequested()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteConfig(entry: "events/main.kel");
        fixture.WriteFile("events/main.kel", "entry =\n");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = new CliApplication().Run(["build", fixture.Root, "--check-only", "--log-format", "json"], output, error, TestContext.CurrentContext.WorkDirectory);

        using var document = JsonDocument.Parse(error.ToString());
        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.SyntaxError));
            Assert.That(document.RootElement.GetProperty("code").GetString(), Does.StartWith("KES1"));
            Assert.That(document.RootElement.GetProperty("file").GetString(), Is.EqualTo("events/main.kel"));
        });
    }

    [Test]
    public void Execute_DoesNotModifyExistingArtifacts()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteConfig(entry: "events/main.kel");
        fixture.WriteFile("events/main.kel", """
entry = intro
intro = {
    chapter = "events/chapter001.kc"
}
""");
        fixture.WriteFile("events/chapter001.kc", """
label #start
jump #start
""");
        fixture.WriteFile("build/windows/events/chapter001.k", "existing build artifact");
        fixture.WriteFile("dist/windows/manifest.json", "{}");
        var before = fixture.SnapshotFiles();

        var result = new BuildCheckOnlyCommand().Execute(new BuildCommandOptions(fixture.Root, DiagnosticOutputFormat.Text), TestContext.CurrentContext.WorkDirectory);
        var after = fixture.SnapshotFiles();

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.Success));
            Assert.That(after, Is.EqualTo(before));
        });
    }

    [Test]
    public void ProcessInvocation_ReturnsSuccessForMinimalProject()
    {
        var projectRoot = GetTestDataPath("projects", "minimal");

        var result = RunCliProcess($"build \"{projectRoot}\" --check-only");

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo((int)CliExitCode.Success));
            Assert.That(result.StandardOutput, Is.Empty);
            Assert.That(result.StandardError, Is.Empty);
        });
    }

    [Test]
    public void ProcessInvocation_ReturnsCommandLineErrorForUnsupportedCommand()
    {
        var result = RunCliProcess("run");

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo((int)CliExitCode.CommandLineError));
            Assert.That(result.StandardError, Does.Contain("KES9001"));
        });
    }

    private static ProcessResult RunCliProcess(string arguments)
    {
        var cliAssembly = Path.Combine(TestContext.CurrentContext.TestDirectory, "KoromoEventScript.Cli.dll");
        var startInfo = new ProcessStartInfo("dotnet", $"\"{cliAssembly}\" {arguments}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = GetRepositoryRoot(),
        };

        using var process = Process.Start(startInfo)!;
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new ProcessResult(process.ExitCode, standardOutput, standardError);
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

    private static string GetTestDataPath(params string[] segments)
    {
        return Path.GetFullPath(Path.Combine(GetRepositoryRoot(), "testdata", Path.Combine(segments)));
    }

    private static string GetRepositoryRoot()
    {
        return Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", ".."));
    }
}
