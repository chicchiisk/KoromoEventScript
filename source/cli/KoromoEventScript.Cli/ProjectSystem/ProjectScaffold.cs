namespace KoromoEventScript.Cli.ProjectSystem;

public sealed record ProjectScaffold(
    string ProjectRoot,
    string ResolvedProjectName,
    IReadOnlyList<string> Directories,
    IReadOnlyList<ProjectScaffoldFile> Files);

public sealed record ProjectScaffoldFile(
    string RelativePath,
    string Contents);
