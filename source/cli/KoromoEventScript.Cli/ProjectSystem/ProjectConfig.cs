namespace KoromoEventScript.Cli.ProjectSystem;

public sealed record ProjectConfig(
    string ProjectRoot,
    string ProjectName,
    string ProjectVersion,
    string EntryPath,
    string EventsPath,
    string AssetsPath,
    string LocalePath,
    string BuildPath,
    string DistPath,
    bool WarningsAsErrors = false,
    int RuntimeWindowWidth = 1280,
    int RuntimeWindowHeight = 720)
{
    public ProjectConfig(
        string ProjectRoot,
        string EntryPath,
        string EventsPath,
        string AssetsPath,
        string LocalePath,
        string BuildPath,
        string DistPath,
        bool WarningsAsErrors = false)
        : this(
            ProjectRoot,
            string.IsNullOrWhiteSpace(ProjectRoot) ? "KoromoEventScriptProject" : Path.GetFileName(ProjectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            "0.1.0",
            EntryPath,
            EventsPath,
            AssetsPath,
            LocalePath,
            BuildPath,
            DistPath,
            WarningsAsErrors)
    {
    }
}
