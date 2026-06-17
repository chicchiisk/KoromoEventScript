using KoromoEventScript.Cli.Localization;
using KoromoEventScript.Cli.Parsing;
using KoromoEventScript.Cli.ProjectSystem;
using KoromoEventScript.Cli.Semantics;

namespace KoromoEventScript.Cli.Tests.Localization;

public class TagAssignmentPlannerTests
{
    [Test]
    public void BuildPlan_UsesSharedSequenceAcrossSayNarAndCase()
    {
        var document = new ScriptDocument(
            "events/Chapter 01!.kc",
            "Chapter01",
            new ScriptSyntax(
            [
                new SayStatementSyntax("Hero", null, [new TextLineSyntax("line", false)], SpeakerLocation: new SourceLocation(1, 5)),
                new NarStatementSyntax(null, [new TextLineSyntax("nar", false)], KeywordLocation: new SourceLocation(3, 1)),
                new SelectStatementSyntax(
                    null,
                [
                    new CaseClauseSyntax("Go", "#go", new SourceLocation(6, 15)),
                ],
                    KeywordLocation: new SourceLocation(5, 1)),
            ]));

        var plan = new TagAssignmentPlanner().BuildPlan(Config(), [document]);

        Assert.That(plan.Candidates.Select(static candidate => candidate.Tag), Is.EqualTo(
            new[]
            {
                "#sy_chapter01_0001",
                "#na_chapter01_0002",
                "#se_chapter01_0003",
            }));
    }

    [Test]
    public void BuildPlan_SkipsReservedNumbersFromExistingAutoTagsOnly()
    {
        var document = new ScriptDocument(
            "events/sample.kc",
            "sample",
            new ScriptSyntax(
            [
                new SayStatementSyntax("Hero", "#sy_sample_0001", [new TextLineSyntax("line", false)], new SourceLocation(1, 10), new SourceLocation(1, 5)),
                new NarStatementSyntax("#manual", [new TextLineSyntax("nar", false)], new SourceLocation(3, 5), new SourceLocation(3, 1)),
                new SelectStatementSyntax("#manual_select", [new CaseClauseSyntax("Go", "#go", new SourceLocation(5, 15))], new SourceLocation(5, 8), new SourceLocation(5, 1)),
                new SayStatementSyntax("Hero", null, [new TextLineSyntax("line2", false)], SpeakerLocation: new SourceLocation(7, 5)),
            ]));

        var plan = new TagAssignmentPlanner().BuildPlan(Config(), [document]);

        Assert.That(plan.Candidates.Single().Tag, Is.EqualTo("#sy_sample_0002"));
    }

    private static ProjectConfig Config()
    {
        return new ProjectConfig("root", "events/main.kel", "events", "assets", "locale", "build", "dist");
    }
}
