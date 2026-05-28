namespace KoromoEventScript.Cli.ProjectSystem;

public sealed record ProjectConfig(
    string ProjectRoot,
    string EntryPath,
    string EventsPath,
    string AssetsPath,
    string LocalePath,
    string BuildPath,
    string DistPath,
    bool WarningsAsErrors = false);
