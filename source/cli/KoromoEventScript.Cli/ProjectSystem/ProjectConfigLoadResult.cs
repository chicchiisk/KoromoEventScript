using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.ProjectSystem;

public sealed record ProjectConfigLoadResult(
    ProjectConfig? Config,
    Diagnostic? Diagnostic,
    bool Succeeded);
