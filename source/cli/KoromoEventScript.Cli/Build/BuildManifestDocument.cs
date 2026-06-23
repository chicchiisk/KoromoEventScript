namespace KoromoEventScript.Cli.Build;

public sealed record BuildManifestDocument(
    string CliVersion,
    string Target,
    string GameId,
    string Title,
    string DefaultLocale,
    string EntryEventListPath,
    IReadOnlyList<BuildManifestInputFile> Inputs,
    IReadOnlyList<BuildManifestScriptArtifact> Scripts,
    IReadOnlyList<BuildManifestAssetArtifact> Assets,
    BuildManifestRuntimeDefaults Defaults,
    BuildManifestBuildInfo Build,
    IReadOnlyList<BuildManifestLocalizationArtifact> Localizations)
{
    public BuildManifestDocument(
        string CliVersion,
        string Target,
        string EntryEventListPath,
        IReadOnlyList<BuildManifestInputFile> Inputs,
        IReadOnlyList<BuildManifestScriptArtifact> Scripts,
        IReadOnlyList<BuildManifestLocalizationArtifact> Localizations)
        : this(
            CliVersion,
            Target,
            "KoromoEventScriptProject",
            "KoromoEventScript Project",
            "ja-JP",
            EntryEventListPath,
            Inputs,
            Scripts,
            [],
            new BuildManifestRuntimeDefaults(1280, 720, false),
            new BuildManifestBuildInfo(BuildId: string.Empty, CliVersion),
            Localizations)
    {
    }
}

public sealed record BuildManifestInputFile(
    string Path,
    string Kind);

public sealed record BuildManifestScriptArtifact(
    string SourcePath,
    string KlibPath,
    string? KlibTextPath,
    string ScriptId,
    string Locale,
    bool IsEntry,
    string? StartLabel)
{
    public BuildManifestScriptArtifact(
        string SourcePath,
        string KlibPath,
        string? KlibTextPath)
        : this(
            SourcePath,
            KlibPath,
            KlibTextPath,
            Path.ChangeExtension(SourcePath.Replace('\\', '/'), null) ?? SourcePath.Replace('\\', '/'),
            "ja-JP",
            false,
            null)
    {
    }
}

public sealed record BuildManifestAssetArtifact(
    string AssetId,
    string Kind,
    string Path,
    string? Locale);

public sealed record BuildManifestRuntimeDefaults(
    int Width,
    int Height,
    bool Fullscreen);

public sealed record BuildManifestBuildInfo(
    string BuildId,
    string CliVersion);

public sealed record BuildManifestLocalizationArtifact(
    string Locale,
    IReadOnlyList<BuildManifestScriptArtifact> Scripts);
