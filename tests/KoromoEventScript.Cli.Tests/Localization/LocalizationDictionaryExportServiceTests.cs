using KoromoEventScript.Cli.Localization;
using KoromoEventScript.Cli.Parsing;
using KoromoEventScript.Cli.ProjectSystem;
using KoromoEventScript.Cli.Semantics;

namespace KoromoEventScript.Cli.Tests.Localization;

public class LocalizationDictionaryExportServiceTests
{
    [Test]
    public void Extract_PreservesMacrosAndMultilineText()
    {
        var document = new ScriptDocument(
            "events/main.kc",
            "main",
            KeParser.Parse("""
say Hero #sy_main_0001:
    hello,{vo}
    next{p}
nar #na_main_0002:
    line1
    @macro value
select #se_main_0003:
    case "Choice A" #choice_a
"""));

        var results = new LocalizationTextExtractor().Extract([document], new TagAssignmentPlan([]));

        Assert.That(results, Is.EqualTo(new[]
        {
            new LocalizationSourceEntry("sy_main_0001", "Hero", "hello,{vo}\nnext{p}"),
            new LocalizationSourceEntry("na_main_0002", string.Empty, "line1\n@macro value"),
            new LocalizationSourceEntry("se_main_0003_c00", string.Empty, "Choice A")
        }));
    }

    [Test]
    public void Export_MergesRequestedAndExistingLocalesWhilePreservingExistingTranslations()
    {
        using var fixture = TemporaryProject.Create();
        var path = Path.Combine(fixture.Root, "localization.csv");
        fixture.WriteFile("localization.csv", """
tag,say,original,en
sy_main_0001,Hero,hello,Hello
manual_tag,,manual,Custom
""");
        var config = new ProjectConfig(fixture.Root, "events/main.kel", "events", "assets", "locale", "build", "dist");
        var document = new ScriptDocument(
            "events/main.kc",
            "main",
            KeParser.Parse("""
say Hero #sy_main_0001:
    hello
nar #na_main_0002:
    world
"""));

        var result = new LocalizationDictionaryExportService().Export(new LocalizationExportRequest(
            config,
            [document],
            new TagAssignmentPlan([]),
            ["fr"],
            path));

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Document!.LocaleColumns, Is.EqualTo(new[] { "en", "fr" }));
            Assert.That(result.Document.Entries.Select(static entry => entry.Tag), Is.EqualTo(new[] { "sy_main_0001", "na_main_0002", "manual_tag" }));
            Assert.That(result.Document.Entries[0].Translations["en"], Is.EqualTo("Hello"));
            Assert.That(result.Document.Entries[0].Translations["fr"], Is.EqualTo(string.Empty));
            Assert.That(result.Document.Entries[2].Translations["en"], Is.EqualTo("Custom"));
        });
    }
}
