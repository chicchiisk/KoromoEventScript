using System.Text;
using KoromoEventScript.Cli.Localization;

namespace KoromoEventScript.Cli.Tests.Localization;

public class LocalizationDictionaryCsvRepositoryTests
{
    [Test]
    public void Save_WritesUtf8BomAndRoundTripsQuotedFields()
    {
        using var fixture = TemporaryProject.Create();
        var path = Path.Combine(fixture.Root, "localization.csv");
        var repository = new LocalizationDictionaryCsvRepository();
        var document = new LocalizationDictionaryDocument(
            ["ja", "en"],
            [
                new LocalizationDictionaryEntry(
                    "sy_main_0001",
                    "Hero",
                    "hello,\n\"world\"",
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["ja"] = "hello,\n\"world\"",
                        ["en"] = "Hello,\n\"World\""
                    })
            ]);

        var save = repository.Save(path, document);
        var bytes = File.ReadAllBytes(path);
        var load = repository.Load(path);

        Assert.Multiple(() =>
        {
            Assert.That(save.Succeeded, Is.True);
            Assert.That(bytes.Take(3).ToArray(), Is.EqualTo(Encoding.UTF8.GetPreamble()));
            Assert.That(load.Succeeded, Is.True);
            Assert.That(load.Document!.LocaleColumns, Is.EqualTo(document.LocaleColumns));
            Assert.That(load.Document.Entries.Count, Is.EqualTo(1));
            Assert.That(load.Document.Entries[0].Tag, Is.EqualTo(document.Entries[0].Tag));
            Assert.That(load.Document.Entries[0].Speaker, Is.EqualTo(document.Entries[0].Speaker));
            Assert.That(load.Document.Entries[0].Original, Is.EqualTo(document.Entries[0].Original));
            Assert.That(load.Document.Entries[0].Translations["ja"], Is.EqualTo(document.Entries[0].Translations["ja"]));
            Assert.That(load.Document.Entries[0].Translations["en"], Is.EqualTo(document.Entries[0].Translations["en"]));
        });
    }

    [Test]
    public void Load_RejectsMissingRequiredColumns()
    {
        using var fixture = TemporaryProject.Create();
        var path = Path.Combine(fixture.Root, "localization.csv");
        fixture.WriteFile("localization.csv", """
tag,original,en
sy_main_0001,hello,Hello
""");

        var result = new LocalizationDictionaryCsvRepository().Load(path);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("KES9006"));
        });
    }

    [Test]
    public void Load_RejectsDuplicateTags()
    {
        using var fixture = TemporaryProject.Create();
        var path = Path.Combine(fixture.Root, "localization.csv");
        fixture.WriteFile("localization.csv", """
tag,say,original,en
sy_main_0001,Hero,hello,Hello
sy_main_0001,Hero,world,World
""");

        var result = new LocalizationDictionaryCsvRepository().Load(path);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("KES9006"));
        });
    }
}
