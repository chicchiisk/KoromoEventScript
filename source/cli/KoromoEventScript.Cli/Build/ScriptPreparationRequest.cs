namespace KoromoEventScript.Cli.Build;

public sealed record ScriptPreparationRequest(
    string? ProjectDirectory,
    string? EntryPath,
    bool WarningsAsErrors);
