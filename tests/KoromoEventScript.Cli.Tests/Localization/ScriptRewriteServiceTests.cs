using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.Localization;
using KoromoEventScript.Cli.ProjectSystem;

namespace KoromoEventScript.Cli.Tests.Localization;

public class ScriptRewriteServiceTests
{
    [Test]
    public void Apply_InsertsMissingTagsWithoutChangingOtherLines()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteFile("events/main.kc", """
say Hero:
    hello
nar:
    world
select:
    case "Go" #go
""");

        var plan = new TagAssignmentPlan(
        [
            new TagAssignmentCandidate("events/main.kc", TagAssignmentKind.Say, 1, 5, 8, "#sy_main_0001"),
            new TagAssignmentCandidate("events/main.kc", TagAssignmentKind.Nar, 3, 1, 4, "#na_main_0002"),
            new TagAssignmentCandidate("events/main.kc", TagAssignmentKind.Select, 5, 1, 7, "#se_main_0003"),
        ]);

        var result = new ScriptRewriteService().Apply(Config(fixture.Root), plan);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.Success));
            Assert.That(File.ReadAllText(Path.Combine(fixture.Root, "events/main.kc")).Replace("\r\n", "\n"), Is.EqualTo("""
say Hero #sy_main_0001:
    hello
nar #na_main_0002:
    world
select #se_main_0003:
    case "Go" #go
"""));
        });
    }

    private static ProjectConfig Config(string root)
    {
        return new ProjectConfig(root, "events/main.kel", "events", "assets", "locale", "build", "dist");
    }
}
