using KoromoEventScript.Cli.Commands.Init;

namespace KoromoEventScript.Cli.ProjectSystem;

public sealed class ProjectScaffoldFactory
{
    private static readonly string[] StandardDirectories =
    [
        "events",
        "assets",
        "assets/bg",
        "assets/actor",
        "assets/voice",
        "assets/se",
        "assets/bgm",
        "locale",
        "build",
        "dist",
    ];

    public ProjectScaffold Create(InitCommandOptions options, string resolvedProjectRoot)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(resolvedProjectRoot);

        var projectName = string.IsNullOrWhiteSpace(options.ProjectName)
            ? new DirectoryInfo(resolvedProjectRoot).Name
            : options.ProjectName;

        var files = new List<ProjectScaffoldFile>
        {
            new("kes.xml", CreateConfig(projectName)),
        };

        if (options.Template == InitTemplate.Basic && !options.NoSample)
        {
            files.Add(new ProjectScaffoldFile("events/main.kel", CreateMainKel()));
            files.Add(new ProjectScaffoldFile("events/chapter001.kc", CreateChapterScript()));
        }

        return new ProjectScaffold(
            resolvedProjectRoot,
            projectName,
            StandardDirectories,
            files);
    }

    private static string CreateConfig(string projectName)
    {
        return $$"""
<?xml version="1.0" encoding="utf-8"?>
<KoromoEventScript
    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
    xsi:noNamespaceSchemaLocation="kes-config.xsd">
    <Project
        Name="{{projectName}}"
        Version="0.1.0"
        Entry="events/main.kel" />

    <Paths
        Events="events"
        Assets="assets"
        Locale="locale"
        Build="build"
        Dist="dist" />

    <Build
        Target="windows"
        WarningsAsErrors="false" />

    <Runtime
        WindowWidth="1280"
        WindowHeight="720" />
</KoromoEventScript>
""";
    }

    private static string CreateMainKel()
    {
        return """
entry = chapter001_intro

chapter001_intro = {
    type = story
    chapter = "events/chapter001.kc"
}
""";
    }

    private static string CreateChapterScript()
    {
        return """
actor Riku:
    var faceName: string = "normal"

label #start

say Riku:
    こんにちは

select:
    case "続ける" #continue
    case "終わる" #end

label #continue
nar:
    物語は続いていく。

jump #end

label #end
""";
    }
}
