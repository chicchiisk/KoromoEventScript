namespace KoromoEventScript.Runtime.Core.Manifests;

public sealed record RuntimeManifestDocument(
    string SchemaVersion,
    string GameId,
    string Title,
    string DefaultLocale,
    IReadOnlyList<RuntimeScriptEntry> Scripts,
    IReadOnlyList<RuntimeAssetEntry> Assets,
    RuntimeSettings Defaults,
    RuntimeBuildInfo Build,
    string ManifestPath,
    string ManifestDirectory);

public sealed record RuntimeScriptEntry(
    string ScriptId,
    string Locale,
    string KlibPath,
    string ResolvedKlibPath,
    bool IsEntry,
    string? StartLabel);

public sealed record RuntimeAssetEntry(
    string AssetId,
    string Kind,
    string Path,
    string ResolvedPath,
    string? Locale);

public sealed record RuntimeSettings(
    int? Width,
    int? Height,
    bool? Fullscreen);

public sealed record RuntimeBuildInfo(
    string? BuildId,
    string? CliVersion);
