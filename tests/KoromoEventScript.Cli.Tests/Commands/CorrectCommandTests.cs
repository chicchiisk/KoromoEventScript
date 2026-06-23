using KoromoEventScript.Cli.Commands;

namespace KoromoEventScript.Cli.Tests.Commands;

public class CorrectCommandTests
{
    [Test]
    public void Run_CheckOnlyOutputsPlannedTagsWithoutModifyingFiles()
    {
        using var fixture = TemporaryProject.Create();
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

say hero:
    hello
nar:
    world
select:
    case "Go" #go
label #go
label #end
""");
        var before = fixture.SnapshotFiles();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = new CliApplication().Run(
            ["correct", fixture.Root, "--check-only"],
            output,
            error,
            TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.Success));
            Assert.That(error.ToString(), Is.Empty);
            Assert.That(output.ToString(), Does.Contain("#sy_main_0001"));
            Assert.That(output.ToString(), Does.Contain("#na_main_0002"));
            Assert.That(output.ToString(), Does.Contain("#se_main_0003"));
            Assert.That(fixture.SnapshotFiles(), Is.EqualTo(before));
        });
    }

    [Test]
    public void Run_RewritesMissingTagsInReferencedScripts()
    {
        using var fixture = TemporaryProject.Create();
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

say hero:
    hello
nar:
    world
select:
    case "Go" #go
label #go
label #end
""");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = new CliApplication().Run(
            ["correct", fixture.Root],
            output,
            error,
            TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.Success));
            Assert.That(error.ToString(), Is.Empty);
            Assert.That(File.ReadAllText(Path.Combine(fixture.Root, "events/main.kc")).Replace("\r\n", "\n"), Is.EqualTo("""
actor Hero:
    var faceName: string = "normal"

standby:
    hero : Hero

say hero #sy_main_0001:
    hello
nar #na_main_0002:
    world
select #se_main_0003:
    case "Go" #go
label #go
label #end
"""));
        });
    }
}
