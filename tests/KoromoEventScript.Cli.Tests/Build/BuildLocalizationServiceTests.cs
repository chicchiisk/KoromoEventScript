using KoromoEventScript.Cli.Build;
using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.Localization;
using KoromoEventScript.Cli.Parsing;
using KoromoEventScript.Cli.ProjectSystem;
using KoromoEventScript.Cli.Semantics;

namespace KoromoEventScript.Cli.Tests.Build;

public class BuildLocalizationServiceTests
{
    [Test]
    public void Resolve_ReplacesSayNarAndSelectTextsForRequestedLocale()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteFile("localization.csv", """
tag,say,original,en
sy_main_0001,Hero,hello,Hello
na_main_0002,,world,World
se_main_0003_c00,,Go,Continue
""");
        var config = CreateConfig(fixture.Root);
        var document = new ScriptDocument(
            "events/main.kc",
            "main",
            KeParser.Parse("""
say Hero #sy_main_0001:
    hello
nar #na_main_0002:
    world
select #se_main_0003:
    case "Go" #next
"""));

        var result = new BuildLocalizationService().Resolve(config, [document], "en");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.Success));
            Assert.That(result.Diagnostics, Is.Empty);
            var syntax = result.Documents.Single().Syntax;
            Assert.That(((SayStatementSyntax)syntax.Statements[0]).Lines.Select(static line => line.Text), Is.EqualTo(["Hello"]));
            Assert.That(((NarStatementSyntax)syntax.Statements[1]).Lines.Select(static line => line.Text), Is.EqualTo(["World"]));
            Assert.That(((SelectStatementSyntax)syntax.Statements[2]).Cases.Select(static @case => @case.Text), Is.EqualTo(["Continue"]));
        });
    }

    [Test]
    public void Resolve_ReturnsFileErrorWhenDictionaryDoesNotExist()
    {
        using var fixture = TemporaryProject.Create();
        var config = CreateConfig(fixture.Root);
        var document = new ScriptDocument("events/main.kc", "main", KeParser.Parse("nar #na_main_0001:\n    line"));

        var result = new BuildLocalizationService().Resolve(config, [document], "en");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.FileOrDirectoryError));
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("KES9004"));
        });
    }

    [Test]
    public void Resolve_ReturnsCompileErrorWhenLocaleColumnIsMissing()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteFile("localization.csv", """
tag,say,original,ja
na_main_0001,,line,行
""");
        var config = CreateConfig(fixture.Root);
        var document = new ScriptDocument("events/main.kc", "main", KeParser.Parse("nar #na_main_0001:\n    line"));

        var result = new BuildLocalizationService().Resolve(config, [document], "en");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.CompileError));
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("KES9006"));
        });
    }

    private static ProjectConfig CreateConfig(string root)
    {
        return new ProjectConfig(root, "events/main.kel", "events", "assets", "locale", "build", "dist");
    }
}
