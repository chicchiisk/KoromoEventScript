using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.Commands.Build;
using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Tests.Commands;

public class WarningPolicyTests
{
    [Test]
    public void Apply_ReturnsSuccessForWarningOnlyWhenWarningsAsErrorsIsDisabled()
    {
        var diagnostics = new[]
        {
            Warning(),
        };

        var exitCode = WarningPolicy.Apply(CliExitCode.Success, diagnostics, warningsAsErrors: false);

        Assert.That(exitCode, Is.EqualTo(CliExitCode.Success));
    }

    [Test]
    public void Apply_ReturnsWarningsAsErrorsForWarningOnlyWhenWarningsAsErrorsIsEnabled()
    {
        var diagnostics = new[]
        {
            Warning(),
        };

        var exitCode = WarningPolicy.Apply(CliExitCode.Success, diagnostics, warningsAsErrors: true);

        Assert.That(exitCode, Is.EqualTo(CliExitCode.WarningsAsErrors));
    }

    [TestCase(CliExitCode.SyntaxError)]
    [TestCase(CliExitCode.CompileError)]
    [TestCase(CliExitCode.FileOrDirectoryError)]
    public void Apply_PreservesExistingErrorExitCode(CliExitCode currentExitCode)
    {
        var diagnostics = new[]
        {
            Warning(),
            new Diagnostic(DiagnosticLevel.Error, "KES2010", "events/main.ke", 2, 1, "Compile error."),
        };

        var exitCode = WarningPolicy.Apply(currentExitCode, diagnostics, warningsAsErrors: true);

        Assert.That(exitCode, Is.EqualTo(currentExitCode));
    }

    private static Diagnostic Warning()
    {
        return new Diagnostic(DiagnosticLevel.Warning, "KES4001", "events/main.ke", 1, 1, "Empty script document.");
    }
}
