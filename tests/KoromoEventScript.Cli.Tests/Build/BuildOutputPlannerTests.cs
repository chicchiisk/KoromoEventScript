using KoromoEventScript.Cli.Build;
using KoromoEventScript.Cli.Commands.Build;
using KoromoEventScript.Cli.Diagnostics;
using KoromoEventScript.Cli.ProjectSystem;

namespace KoromoEventScript.Cli.Tests.Build;

public class BuildOutputPlannerTests
{
    [Test]
    public void Resolve_UsesProjectBuildPathForPrimaryLanguageArtifacts()
    {
        var config = CreateConfig();
        var options = new BuildCommandOptions(
            ProjectDirectory: config.ProjectRoot,
            OutputFormat: DiagnosticOutputFormat.Text,
            Target: "windows");

        var paths = new BuildOutputPlanner().Resolve(config, options, "events/chapter001.kc");

        Assert.Multiple(() =>
        {
            Assert.That(paths.KlibPath, Is.EqualTo(Path.Combine(config.ProjectRoot, "build", "windows", "events", "chapter001.klib")));
            Assert.That(paths.KlibTextPath, Is.Null);
            Assert.That(paths.ManifestPath, Is.EqualTo(Path.Combine(config.ProjectRoot, "build", "windows", "manifest.json")));
            Assert.That(paths.DiagnosticsPath, Is.EqualTo(Path.Combine(config.ProjectRoot, "build", "windows", "diagnostics.json")));
        });
    }

    [Test]
    public void Resolve_UsesOutDirOverrideForPrimaryLanguageArtifacts()
    {
        var config = CreateConfig();
        var options = new BuildCommandOptions(
            ProjectDirectory: config.ProjectRoot,
            OutputFormat: DiagnosticOutputFormat.Text,
            Target: "windows",
            OutputDirectory: "custom-output",
            EmitTextIr: true);

        var paths = new BuildOutputPlanner().Resolve(config, options, "events/sub/chapter001.kc");

        Assert.Multiple(() =>
        {
            Assert.That(paths.KlibPath, Is.EqualTo(Path.Combine(config.ProjectRoot, "custom-output", "windows", "events", "sub", "chapter001.klib")));
            Assert.That(paths.KlibTextPath, Is.EqualTo(Path.Combine(config.ProjectRoot, "custom-output", "windows", "events", "sub", "chapter001.klibtxt")));
            Assert.That(paths.ManifestPath, Is.EqualTo(Path.Combine(config.ProjectRoot, "custom-output", "windows", "manifest.json")));
            Assert.That(paths.DiagnosticsPath, Is.EqualTo(Path.Combine(config.ProjectRoot, "custom-output", "windows", "diagnostics.json")));
        });
    }

    [Test]
    public void Resolve_UsesLocaleSubdirectoryForLocalizedArtifacts()
    {
        var config = CreateConfig();
        var options = new BuildCommandOptions(
            ProjectDirectory: config.ProjectRoot,
            OutputFormat: DiagnosticOutputFormat.Text,
            Target: "windows",
            Locale: "en",
            EmitTextIr: true);

        var paths = new BuildOutputPlanner().Resolve(config, options, "events/sub/chapter001.kc");

        Assert.Multiple(() =>
        {
            Assert.That(paths.KlibPath, Is.EqualTo(Path.Combine(config.ProjectRoot, "build", "windows", "events", "loc", "en", "sub", "chapter001.klib")));
            Assert.That(paths.KlibTextPath, Is.EqualTo(Path.Combine(config.ProjectRoot, "build", "windows", "events", "loc", "en", "sub", "chapter001.klibtxt")));
            Assert.That(paths.ManifestPath, Is.EqualTo(Path.Combine(config.ProjectRoot, "build", "windows", "manifest.json")));
            Assert.That(paths.DiagnosticsPath, Is.EqualTo(Path.Combine(config.ProjectRoot, "build", "windows", "diagnostics.json")));
        });
    }

    private static ProjectConfig CreateConfig()
    {
        return new ProjectConfig(
            ProjectRoot: Path.Combine("D:", "Develop", "Koromosoft", "KoromoEventScript", "testdata", "projects", "minimal"),
            EntryPath: "events/main.kel",
            EventsPath: "events",
            AssetsPath: "assets",
            LocalePath: "locale",
            BuildPath: "build",
            DistPath: "dist");
    }
}
