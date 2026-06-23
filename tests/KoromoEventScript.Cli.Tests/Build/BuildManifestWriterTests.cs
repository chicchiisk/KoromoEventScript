using System.Text.Json;
using KoromoEventScript.Cli.Build;

namespace KoromoEventScript.Cli.Tests.Build;

public class BuildManifestWriterTests
{
    [Test]
    public void Write_PersistsManifestJsonWithScriptsAndLocalizations()
    {
        using var fixture = TemporaryProject.Create();
        var path = Path.Combine(fixture.Root, "build", "windows", "manifest.json");
        var document = new BuildManifestDocument(
            CliVersion: "0.1.0",
            Target: "windows",
            EntryEventListPath: "events/main.kel",
            Inputs:
            [
                new BuildManifestInputFile("events/main.kel", "kel"),
                new BuildManifestInputFile("events/main.kc", "kc"),
            ],
            Scripts:
            [
                new BuildManifestScriptArtifact("events/main.kc", "events/main.klib", "events/main.klibtxt"),
            ],
            Localizations:
            [
                new BuildManifestLocalizationArtifact(
                    "en",
                    [
                        new BuildManifestScriptArtifact("events/main.kc", "events/loc/en/main.klib", "events/loc/en/main.klibtxt"),
                    ]),
            ]);

        var result = new BuildManifestWriter().Write(path, document);

        using var json = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(json.RootElement.GetProperty("schemaVersion").GetString(), Is.EqualTo("1.0"));
            Assert.That(json.RootElement.GetProperty("gameId").GetString(), Is.EqualTo("KoromoEventScriptProject"));
            Assert.That(json.RootElement.GetProperty("defaultLocale").GetString(), Is.EqualTo("ja-JP"));
            Assert.That(json.RootElement.GetProperty("cliVersion").GetString(), Is.EqualTo("0.1.0"));
            Assert.That(json.RootElement.GetProperty("target").GetString(), Is.EqualTo("windows"));
            Assert.That(json.RootElement.GetProperty("entryEventListPath").GetString(), Is.EqualTo("events/main.kel"));
            Assert.That(json.RootElement.GetProperty("scripts")[0].GetProperty("scriptId").GetString(), Is.EqualTo("events/main"));
            Assert.That(json.RootElement.GetProperty("scripts")[0].GetProperty("locale").GetString(), Is.EqualTo("ja-JP"));
            Assert.That(json.RootElement.GetProperty("scripts")[0].GetProperty("klibPath").GetString(), Is.EqualTo("events/main.klib"));
            Assert.That(json.RootElement.GetProperty("assets").GetArrayLength(), Is.EqualTo(0));
            Assert.That(json.RootElement.GetProperty("defaults").GetProperty("width").GetInt32(), Is.EqualTo(1280));
            Assert.That(json.RootElement.GetProperty("build").GetProperty("cliVersion").GetString(), Is.EqualTo("0.1.0"));
            Assert.That(json.RootElement.GetProperty("localizations")[0].GetProperty("locale").GetString(), Is.EqualTo("en"));
            Assert.That(json.RootElement.GetProperty("localizations")[0].GetProperty("scripts")[0].GetProperty("klibPath").GetString(), Is.EqualTo("events/loc/en/main.klib"));
        });
    }
}
