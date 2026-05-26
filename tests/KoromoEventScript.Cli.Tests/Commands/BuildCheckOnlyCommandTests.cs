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

    [TestCase("success", CliExitCode.Success, null)]
    [TestCase("missing-import", CliExitCode.FileOrDirectoryError, "KES9005")]
    [TestCase("ambiguous-import", CliExitCode.CompileError, "KES2007")]
    [TestCase("cycle", CliExitCode.CompileError, "KES2008")]
    [TestCase("syntax-error", CliExitCode.SyntaxError, "KES1000")]
    [TestCase("name-resolution-failure", CliExitCode.CompileError, "KES2010")]
    public void Execute_IncludesImportAndNameSemanticValidation(
        string scenarioName,
        CliExitCode expectedExitCode,
        string? expectedFirstDiagnosticCode)
    {
        var projectRoot = GetTestDataPath("projects", "import-resolution", scenarioName);

        var result = new BuildCheckOnlyCommand().Execute(
            new BuildCommandOptions(projectRoot, DiagnosticOutputFormat.Text),
            TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(expectedExitCode));
            if (expectedFirstDiagnosticCode is null)
            {
                Assert.That(result.Diagnostics, Is.Empty);
            }
            else
            {
                Assert.That(result.Diagnostics, Is.Not.Empty);
                Assert.That(result.Diagnostics[0].Code, Is.EqualTo(expectedFirstDiagnosticCode));
            }
        });
    }

    [Test]
    public void Run_OutputsImportAndNameDiagnosticsAsOrderedJsonLines()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var projectRoot = GetTestDataPath("projects", "import-resolution", "name-resolution-failure");

        var exitCode = new CliApplication().Run(
            ["build", projectRoot, "--check-only", "--log-format", "json"],
            output,
            error,
            TestContext.CurrentContext.WorkDirectory);

        var lines = error.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        using var first = JsonDocument.Parse(lines[0]);
        using var second = JsonDocument.Parse(lines[1]);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.CompileError));
            Assert.That(output.ToString(), Is.Empty);
            Assert.That(lines, Has.Length.EqualTo(2));
            AssertJsonDiagnostic(first.RootElement, "KES2010", "events/main.ke", 4, 17);
            AssertJsonDiagnostic(second.RootElement, "KES2012", "events/main.ke", 4, 28);
        });
    }

    [Test]
    public void Execute_ReportsMissingImportAtImporterLocation()
    {
        var projectRoot = GetTestDataPath("projects", "import-resolution", "missing-import");

        var result = new BuildCheckOnlyCommand().Execute(
            new BuildCommandOptions(projectRoot, DiagnosticOutputFormat.Text),
            TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.FileOrDirectoryError));
            Assert.That(result.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(result.Diagnostics[0].Code, Is.EqualTo("KES9005"));
            Assert.That(result.Diagnostics[0].File, Is.EqualTo("events/main.ke"));
            Assert.That(result.Diagnostics[0].Line, Is.EqualTo(1));
            Assert.That(result.Diagnostics[0].Column, Is.EqualTo(1));
        });
    }

    [Test]
    public void Execute_ReportsCyclePathInImportDiagnosticMessage()
    {
        var projectRoot = GetTestDataPath("projects", "import-resolution", "cycle");

        var result = new BuildCheckOnlyCommand().Execute(
            new BuildCommandOptions(projectRoot, DiagnosticOutputFormat.Text),
            TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.CompileError));
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("KES2008"));
            Assert.That(result.Diagnostics.Single().Message, Does.Contain("A -> B -> A"));
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

    private static void AssertJsonDiagnostic(JsonElement root, string code, string file, int line, int column)
    {
        Assert.Multiple(() =>
        {
            Assert.That(root.GetProperty("level").GetString(), Is.EqualTo("error"));
            Assert.That(root.GetProperty("code").GetString(), Is.EqualTo(code));
            Assert.That(root.GetProperty("file").GetString(), Is.EqualTo(file));
            Assert.That(root.GetProperty("line").GetInt32(), Is.EqualTo(line));
            Assert.That(root.GetProperty("column").GetInt32(), Is.EqualTo(column));
            Assert.That(root.GetProperty("message").GetString(), Is.Not.Empty);
        });
    }

    private static string GetTestDataPath(params string[] segments)
    {
        return Path.GetFullPath(Path.Combine(GetRepositoryRoot(), "testdata", Path.Combine(segments)));
    }

    private static string GetRepositoryRoot()
    {
        return Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", ".."));
    }
}
