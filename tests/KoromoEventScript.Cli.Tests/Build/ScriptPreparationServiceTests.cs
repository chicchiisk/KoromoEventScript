using KoromoEventScript.Cli.Build;

namespace KoromoEventScript.Cli.Tests.Build;

public class ScriptPreparationServiceTests
{
    [Test]
    public void Prepare_UsesEntryOverrideWhenProvided()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteConfig(entry: "events/default.kel");
        fixture.WriteFile("events/default.kel", """
entry = intro
intro = {
    chapter = "events/default.kc"
}
""");
        fixture.WriteFile("events/default.kc", "label #default");
        fixture.WriteFile("events/alt.kel", """
entry = intro
intro = {
    chapter = "events/alt.kc"
}
""");
        fixture.WriteFile("events/alt.kc", "label #alt");

        var result = new ScriptPreparationService().Prepare(
            new ScriptPreparationRequest(fixture.Root, "events/alt.kel", WarningsAsErrors: false),
            TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.SemanticResult!.ImportGraph!.OrderedDocuments.Select(static document => document.ProjectRelativePath),
                Is.EqualTo(["events/alt.kc"]));
        });
    }
}
