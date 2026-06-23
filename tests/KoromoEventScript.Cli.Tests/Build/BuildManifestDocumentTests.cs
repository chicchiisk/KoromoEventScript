using KoromoEventScript.Cli.Build;

namespace KoromoEventScript.Cli.Tests.Build;

public class BuildManifestDocumentTests
{
    [Test]
    public void Document_CapturesPrimaryAndLocalizedArtifacts()
    {
        var document = new BuildManifestDocument(
            CliVersion: "0.1.0",
            Target: "windows",
            EntryEventListPath: "events/main.kel",
            Inputs:
            [
                new BuildManifestInputFile("events/main.kel", "kel"),
                new BuildManifestInputFile("events/chapter001.kc", "kc"),
            ],
            Scripts:
            [
                new BuildManifestScriptArtifact(
                    SourcePath: "events/chapter001.kc",
                    KlibPath: "events/chapter001.klib",
                    KlibTextPath: "events/chapter001.klibtxt"),
            ],
            Localizations:
            [
                new BuildManifestLocalizationArtifact(
                    Locale: "en",
                    Scripts:
                    [
                        new BuildManifestScriptArtifact(
                            SourcePath: "events/chapter001.kc",
                            KlibPath: "events/loc/en/chapter001.klib",
                            KlibTextPath: "events/loc/en/chapter001.klibtxt"),
                    ]),
            ]);

        Assert.Multiple(() =>
        {
            Assert.That(document.CliVersion, Is.EqualTo("0.1.0"));
            Assert.That(document.Target, Is.EqualTo("windows"));
            Assert.That(document.EntryEventListPath, Is.EqualTo("events/main.kel"));
            Assert.That(document.Inputs.Select(static input => input.Path), Is.EqualTo(["events/main.kel", "events/chapter001.kc"]));
            Assert.That(document.Scripts.Single().KlibPath, Is.EqualTo("events/chapter001.klib"));
            Assert.That(document.Scripts.Single().ScriptId, Is.EqualTo("events/chapter001"));
            Assert.That(document.Defaults.Width, Is.EqualTo(1280));
            Assert.That(document.Defaults.Height, Is.EqualTo(720));
            Assert.That(document.Localizations.Single().Locale, Is.EqualTo("en"));
            Assert.That(document.Localizations.Single().Scripts.Single().KlibPath, Is.EqualTo("events/loc/en/chapter001.klib"));
        });
    }
}
