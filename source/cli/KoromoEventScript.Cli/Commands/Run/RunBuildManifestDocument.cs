namespace KoromoEventScript.Cli.Commands.Run;

public sealed record RunBuildManifestDocument(
    string Target,
    IReadOnlyList<RunBuildManifestScript> Scripts);

public sealed record RunBuildManifestScript(
    string KlibPath);
