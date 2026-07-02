using KoromoEventScript.Cli.ProjectSystem;

namespace KoromoEventScript.Cli.Commands.Run;

public sealed record RunProjectInput(
    string ProjectRoot,
    ProjectConfig Config,
    string EntryPath,
    string EntryFullPath,
    string ManifestPath);
