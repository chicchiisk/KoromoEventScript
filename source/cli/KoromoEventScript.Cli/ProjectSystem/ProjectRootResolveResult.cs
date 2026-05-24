using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.ProjectSystem;

public sealed record ProjectRootResolveResult(
    string? ProjectRoot,
    Diagnostic? Diagnostic,
    bool Succeeded);
