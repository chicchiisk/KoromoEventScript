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
    public void Run_OutputsTextDiagnostics()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteConfig(entry: "events/main.kel");
        fixture.WriteFile("events/main.kel", "entry =\n");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = new CliApplication().Run(["build", fixture.Root, "--check-only"], output, error, TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.SyntaxError));
            Assert.That(output.ToString(), Is.Empty);
            Assert.That(error.ToString(), Does.Contain("events/main.kel"));
            Assert.That(error.ToString(), Does.Contain("error KES1"));
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
    public void Run_OutputsImportAndNameDiagnosticsAsOrderedText()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var projectRoot = GetTestDataPath("projects", "import-resolution", "name-resolution-failure");

        var exitCode = new CliApplication().Run(
            ["build", projectRoot, "--check-only"],
            output,
            error,
            TestContext.CurrentContext.WorkDirectory);

        var lines = error.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.CompileError));
            Assert.That(output.ToString(), Is.Empty);
            Assert.That(lines, Has.Length.EqualTo(2));
            Assert.That(lines[0], Does.Contain("events/main.ke:7:17 error KES2010"));
            Assert.That(lines[1], Does.Contain("events/main.ke:7:28 error KES2012"));
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
    public void Execute_ReturnsSuccessForValidMajorDefinitions()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteConfig(entry: "events/main.kel");
        fixture.WriteFile("events/main.kel", """
entry = intro
intro = {
    chapter = "events/main.ke"
}
""");
        fixture.WriteFile("events/main.ke", """
actor Riku:
    var faceName: string = "normal"
standby:
    riku : Riku
fn setup():
    var localValue = 1
class Counter:
    var value: number = 0
enum Mood:
    normal
""");

        var result = new BuildCheckOnlyCommand().Execute(
            new BuildCommandOptions(fixture.Root, DiagnosticOutputFormat.Text),
            TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.Success));
            Assert.That(result.Diagnostics, Is.Empty);
        });
    }

    [Test]
    public void Execute_IncludesWarningDiagnosticsWithoutFailingByDefault()
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

        var result = new BuildCheckOnlyCommand().Execute(
            new BuildCommandOptions(fixture.Root, DiagnosticOutputFormat.Text),
            TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.Success));
            Assert.That(result.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(result.Diagnostics[0].Level, Is.EqualTo(DiagnosticLevel.Warning));
            Assert.That(result.Diagnostics[0].Code, Is.EqualTo("KES4001"));
            Assert.That(result.Diagnostics[0].File, Is.EqualTo("events/main.ke"));
            Assert.That(result.Diagnostics[0].Line, Is.EqualTo(1));
            Assert.That(result.Diagnostics[0].Column, Is.EqualTo(1));
        });
    }

    [Test]
    public void Execute_ReturnsWarningsAsErrorsForWarningDiagnosticsWhenOptionIsEnabled()
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

        var result = new BuildCheckOnlyCommand().Execute(
            new BuildCommandOptions(fixture.Root, DiagnosticOutputFormat.Text, WarningsAsErrors: true),
            TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.WarningsAsErrors));
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("KES4001"));
        });
    }

    [Test]
    public void Execute_ReturnsWarningsAsErrorsForWarningDiagnosticsWhenConfigIsEnabled()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteConfig(entry: "events/main.kel", warningsAsErrors: true);
        fixture.WriteFile("events/main.kel", """
entry = intro
intro = {
    chapter = "events/main.ke"
}
""");
        fixture.WriteFile("events/main.ke", string.Empty);

        var result = new BuildCheckOnlyCommand().Execute(
            new BuildCommandOptions(fixture.Root, DiagnosticOutputFormat.Text),
            TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.WarningsAsErrors));
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("KES4001"));
        });
    }

    [Test]
    public void Run_OutputsWarningDiagnosticsAsText()
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
            ["build", fixture.Root, "--check-only"],
            output,
            error,
            TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.Success));
            Assert.That(output.ToString(), Is.Empty);
            Assert.That(error.ToString(), Does.Contain("events/main.ke:1:1 warning KES4001"));
        });
    }

    [Test]
    public void Run_OutputsWarningDiagnosticsToStderrAndReturnsWarningsAsErrors()
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
            Assert.That(error.ToString(), Does.Contain("events/main.ke:1:1 warning KES4001"));
        });
    }

    [Test]
    public void Execute_ReportsUndefinedJumpAndCaseTagsAsCompileDiagnostics()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteConfig(entry: "events/main.kel");
        fixture.WriteFile("events/main.kel", """
entry = intro
intro = {
    chapter = "events/main.ke"
}
""");
        fixture.WriteFile("events/main.ke", """
select:
    case "進む" #missing_case
jump #missing_jump
""");

        var result = new BuildCheckOnlyCommand().Execute(
            new BuildCommandOptions(fixture.Root, DiagnosticOutputFormat.Text),
            TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.CompileError));
            Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Code), Is.EqualTo(["KES2013", "KES2013"]));
            Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.File), Is.All.EqualTo("events/main.ke"));
            Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Line), Is.EqualTo([2, 3]));
            Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Column), Is.EqualTo([15, 6]));
        });
    }

    [Test]
    public void Execute_ReportsUndefinedActorAndFunctionReferencesAtReferenceLocations()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteConfig(entry: "events/main.kel");
        fixture.WriteFile("events/main.kel", """
entry = intro
intro = {
    chapter = "events/main.ke"
}
""");
        fixture.WriteFile("events/main.ke", """
actor Riku:
    var faceName: string = "normal"

say MissingSpeaker:
    こんにちは

show MissingActor 0
missing_command
var result = missing_call()
""");

        var result = new BuildCheckOnlyCommand().Execute(
            new BuildCommandOptions(fixture.Root, DiagnosticOutputFormat.Text),
            TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.CompileError));
            Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Code), Is.EqualTo(["KES2010", "KES2010", "KES2010", "KES2010"]));
            Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Line), Is.EqualTo([4, 7, 8, 9]));
            Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Column), Is.EqualTo([5, 6, 1, 14]));
            Assert.That(result.Diagnostics[0].Message, Does.Contain("MissingSpeaker"));
            Assert.That(result.Diagnostics[1].Message, Does.Contain("MissingActor"));
            Assert.That(result.Diagnostics[2].Message, Does.Contain("missing_command"));
            Assert.That(result.Diagnostics[3].Message, Does.Contain("missing_call"));
        });
    }

    [Test]
    public void Execute_ReportsTypeDiagnosticsAsCompileErrors()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteConfig(entry: "events/main.kel");
        fixture.WriteFile("events/main.kel", """
entry = intro
intro = {
    chapter = "events/main.ke"
}
""");
        fixture.WriteFile("events/main.ke", """
var score: number = "bad"
""");

        var result = new BuildCheckOnlyCommand().Execute(
            new BuildCommandOptions(fixture.Root, DiagnosticOutputFormat.Text),
            TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.CompileError));
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("KES2015"));
            Assert.That(result.Diagnostics.Single().File, Is.EqualTo("events/main.ke"));
            Assert.That(result.Diagnostics.Single().Line, Is.EqualTo(1));
            Assert.That(result.Diagnostics.Single().Message, Does.Contain("number"));
            Assert.That(result.Diagnostics.Single().Message, Does.Contain("string"));
        });
    }

    [TestCase("success", CliExitCode.Success, null)]
    [TestCase("failures", CliExitCode.CompileError, "KES2015")]
    public void Execute_ValidatesTypeCheckingFixtures(
        string scenarioName,
        CliExitCode expectedExitCode,
        string? expectedFirstDiagnosticCode)
    {
        var projectRoot = GetTestDataPath("projects", "type-checking", scenarioName);

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
    public void Run_OutputsTypeDiagnosticsAsText()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteConfig(entry: "events/main.kel");
        fixture.WriteFile("events/main.kel", """
entry = intro
intro = {
    chapter = "events/main.ke"
}
""");
        fixture.WriteFile("events/main.ke", """
var score: number = "bad"
""");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = new CliApplication().Run(
            ["build", fixture.Root, "--check-only"],
            output,
            error,
            TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.CompileError));
            Assert.That(output.ToString(), Is.Empty);
            Assert.That(error.ToString(), Does.Contain("events/main.ke:1:5 error KES2015"));
        });
    }

    [Test]
    public void Execute_ReturnsCompileErrorForDefinitionShadowing()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteConfig(entry: "events/main.kel");
        fixture.WriteFile("events/main.kel", """
entry = intro
intro = {
    chapter = "events/main.ke"
}
""");
        fixture.WriteFile("events/main.ke", """
var score = 0
fn calc(score: number):
    score_value score
""");

        var result = new BuildCheckOnlyCommand().Execute(
            new BuildCommandOptions(fixture.Root, DiagnosticOutputFormat.Text),
            TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.CompileError));
            Assert.That(result.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(result.Diagnostics[0].Code, Is.EqualTo("KES2014"));
            Assert.That(result.Diagnostics[0].File, Is.EqualTo("events/main.ke"));
            Assert.That(result.Diagnostics[0].Line, Is.EqualTo(2));
            Assert.That(result.Diagnostics[0].Column, Is.EqualTo(9));
        });
    }

    [Test]
    public void Execute_ReportsDuplicateTagsAsCompileDiagnostics()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteConfig(entry: "events/main.kel");
        fixture.WriteFile("events/main.kel", """
entry = intro
intro = {
    chapter = "events/main.ke"
}
""");
        fixture.WriteFile("events/main.ke", """
label #start
nar #start:
    duplicated
""");

        var result = new BuildCheckOnlyCommand().Execute(
            new BuildCommandOptions(fixture.Root, DiagnosticOutputFormat.Text),
            TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.CompileError));
            Assert.That(result.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(result.Diagnostics[0].Code, Is.EqualTo("KES2009"));
            Assert.That(result.Diagnostics[0].File, Is.EqualTo("events/main.ke"));
            Assert.That(result.Diagnostics[0].Line, Is.EqualTo(2));
            Assert.That(result.Diagnostics[0].Column, Is.EqualTo(5));
            Assert.That(result.Diagnostics[0].Message, Does.Contain("#start"));
        });
    }

    [Test]
    public void Run_OutputsDefinitionDiagnosticsAsText()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteConfig(entry: "events/main.kel");
        fixture.WriteFile("events/main.kel", """
entry = intro
intro = {
    chapter = "events/main.ke"
}
""");
        fixture.WriteFile("events/main.ke", """
class Counter:
    var value: number = 0
    fn value():
        use value
""");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = new CliApplication().Run(
            ["build", fixture.Root, "--check-only"],
            output,
            error,
            TestContext.CurrentContext.WorkDirectory);

        var lines = error.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.CompileError));
            Assert.That(output.ToString(), Is.Empty);
            Assert.That(lines.Single(), Does.Contain("events/main.ke:3:8 error KES2009"));
            Assert.That(lines.Single(), Does.Contain("events/main.ke:2:9"));
        });
    }

    [Test]
    public void Run_OutputsDuplicateDefinitionOriginalLocationInText()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteConfig(entry: "events/main.kel");
        fixture.WriteFile("events/main.kel", """
entry = intro
intro = {
    chapter = "events/main.ke"
}
""");
        fixture.WriteFile("events/main.ke", """
var score = 0
var score = 1
""");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = new CliApplication().Run(
            ["build", fixture.Root, "--check-only"],
            output,
            error,
            TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.CompileError));
            Assert.That(output.ToString(), Is.Empty);
            Assert.That(error.ToString(), Does.Contain("events/main.ke:2:5 error KES2009"));
            Assert.That(error.ToString(), Does.Contain("events/main.ke:1:5"));
            Assert.That(error.ToString(), Does.Contain("Original definition is here."));
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
    public void ProcessInvocation_PrintsVersion()
    {
        var result = RunCliProcess("--version");

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo((int)CliExitCode.Success));
            Assert.That(result.StandardOutput, Is.EqualTo($"kes 0.1.0{Environment.NewLine}"));
            Assert.That(result.StandardError, Is.Empty);
        });
    }

    [Test]
    public void ProcessInvocation_PrintsHelp()
    {
        var result = RunCliProcess("--help");

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo((int)CliExitCode.Success));
            Assert.That(result.StandardOutput, Does.Contain("Usage:"));
            Assert.That(result.StandardOutput, Does.Contain("Commands:"));
            Assert.That(result.StandardError, Is.Empty);
        });
    }

    [Test]
    public void ProcessInvocation_ReturnsWarningsAsErrorsForWarningOnlyProject()
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

        var result = RunCliProcess($"build \"{fixture.Root}\" --check-only --warnings-as-errors");

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo((int)CliExitCode.WarningsAsErrors));
            Assert.That(result.StandardOutput, Is.Empty);
            Assert.That(result.StandardError, Does.Contain("warning KES4001"));
        });
    }

    [Test]
    public void ProcessInvocation_ReturnsCommandLineErrorForUnsupportedCommand()
    {
        var result = RunCliProcess("unknown");

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo((int)CliExitCode.CommandLineError));
            Assert.That(result.StandardError, Does.Contain("KES9001"));
        });
    }

    private static ProcessResult RunCliProcess(string arguments)
    {
        var cliAssemblyName = typeof(CliApplication).Assembly.GetName().Name + ".dll";
        var cliAssembly = Path.Combine(TestContext.CurrentContext.TestDirectory, cliAssemblyName);
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
