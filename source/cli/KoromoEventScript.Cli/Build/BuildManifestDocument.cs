namespace KoromoEventScript.Cli.Build;

public sealed record BuildManifestDocument(
    string CliVersion,
    string Target,
    string EntryEventListPath,
    IReadOnlyList<BuildManifestInputFile> Inputs,
    IReadOnlyList<BuildManifestScriptArtifact> Scripts,
    IReadOnlyList<BuildManifestLocalizationArtifact> Localizations);

public sealed record BuildManifestInputFile(
    string Path,
    string Kind);

public sealed record BuildManifestScriptArtifact(
    string SourcePath,
    string KlibPath,
    string? KlibTextPath);

public sealed record BuildManifestLocalizationArtifact(
    string Locale,
    IReadOnlyList<BuildManifestScriptArtifact> Scripts);
