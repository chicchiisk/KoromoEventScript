using KoromoEventScript.Cli.Commands;

namespace KoromoEventScript.Cli.Tests.Commands;

public class LocCommandTests
{
    [Test]
    public void Run_ExportsLocalizationDictionaryAndRewritesMissingTags()
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
    hello,{vo}
nar:
    line1
    line2
select:
    case "Go Town" #town
    case "Go Forest" #forest
label #town
label #forest
""");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = new CliApplication().Run(
            ["loc", fixture.Root, "--locale", "ja,en"],
            output,
            error,
            TestContext.CurrentContext.WorkDirectory);

        var csvPath = Path.Combine(fixture.Root, "localization.csv");
        var csv = File.ReadAllText(csvPath);
        var script = File.ReadAllText(Path.Combine(fixture.Root, "events/main.kc")).Replace("\r\n", "\n");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.Success));
            Assert.That(error.ToString(), Is.Empty);
            Assert.That(output.ToString(), Does.Contain("localization.csv"));
            Assert.That(script, Does.Contain("say hero #sy_main_0001:"));
            Assert.That(script, Does.Contain("nar #na_main_0002:"));
            Assert.That(script, Does.Contain("select #se_main_0003:"));
            Assert.That(csv, Does.Contain("tag,say,original,ja,en"));
            Assert.That(csv, Does.Contain("sy_main_0001,hero,\"hello,{vo}\",,"));
            Assert.That(csv, Does.Contain("na_main_0002,,\"line1"));
            Assert.That(csv, Does.Contain("se_main_0003_c00,,Go Town,,"));
            Assert.That(csv, Does.Contain("se_main_0003_c01,,Go Forest,,"));
        });
    }

    [Test]
    public void Run_MergesExistingTranslationsAndWritesToRequestedOutputPath()
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

say hero #sy_main_0001:
    hello
nar #na_main_0002:
    world
select #se_main_0003:
    case "Go" #go
label #go
""");
        fixture.WriteFile("translations/messages.csv", """
tag,say,original,en
sy_main_0001,Hero,hello,Hello!
manual_tag,,Manual entry,Custom
""");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = new CliApplication().Run(
            ["loc", fixture.Root, "--locale", "fr", "--out", "translations/messages.csv"],
            output,
            error,
            TestContext.CurrentContext.WorkDirectory);

        var csv = File.ReadAllText(Path.Combine(fixture.Root, "translations/messages.csv"));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.Success));
            Assert.That(error.ToString(), Is.Empty);
            Assert.That(csv, Does.Contain("tag,say,original,en,fr"));
            Assert.That(csv, Does.Contain("sy_main_0001,hero,hello,Hello!,"));
            Assert.That(csv, Does.Contain("manual_tag,,Manual entry,Custom,"));
            Assert.That(csv, Does.Contain("na_main_0002,,world,,"));
            Assert.That(csv, Does.Contain("se_main_0003_c00,,Go,,"));
        });
    }

    [Test]
    public void Run_FailsWhenExistingDictionaryIsInvalid()
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

say hero #sy_main_0001:
    hello
""");
        fixture.WriteFile("localization.csv", """
tag,original,en
sy_main_0001,hello,Hello
""");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = new CliApplication().Run(
            ["loc", fixture.Root],
            output,
            error,
            TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.FileOrDirectoryError));
            Assert.That(output.ToString(), Is.Empty);
            Assert.That(error.ToString(), Does.Contain("KES9006"));
        });
    }
}
