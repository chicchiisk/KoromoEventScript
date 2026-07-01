using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Commands.Run;

public sealed record RunCommandOptions(
    string? ProjectDirectory,
    DiagnosticOutputFormat OutputFormat,
    string Target = "windows",
    RunBuildMode BuildMode = RunBuildMode.IfStale,
    string? Locale = null,
    string? Start = null,
    bool Fullscreen = false,
    int? Width = null,
    int? Height = null,
    bool Debug = false,
    bool Profile = false,
    IReadOnlyList<string>? RuntimeArguments = null);
