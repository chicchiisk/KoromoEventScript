using KoromoEventScript.Cli.Commands;

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
}
