using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.ProjectSystem;

public sealed record ProjectScaffoldWriteResult(
    bool Succeeded,
    IReadOnlyList<Diagnostic> Diagnostics);
