#nullable enable

using System;
using System.Collections.Generic;

namespace KoromoEventScript.Runtime.Core.Manifests
{

public sealed record RuntimeManifestDocument(
    string SchemaVersion,
    string GameId,
    string Title,
    string DefaultLocale,
    IReadOnlyList<RuntimeScriptEntry> Scripts,
    IReadOnlyList<RuntimeEventEntry> Events,
    IReadOnlyList<RuntimeAssetEntry> Assets,
    RuntimeSettings Defaults,
    RuntimeBuildInfo Build,
    string ManifestPath,
    string ManifestDirectory)
{
    public RuntimeManifestDocument(
        string SchemaVersion,
        string GameId,
        string Title,
        string DefaultLocale,
        IReadOnlyList<RuntimeScriptEntry> Scripts,
        IReadOnlyList<RuntimeAssetEntry> Assets,
        RuntimeSettings Defaults,
        RuntimeBuildInfo Build,
        string ManifestPath,
        string ManifestDirectory)
        : this(
            SchemaVersion,
            GameId,
            Title,
            DefaultLocale,
            Scripts,
            Array.Empty<RuntimeEventEntry>(),
            Assets,
            Defaults,
            Build,
            ManifestPath,
            ManifestDirectory)
    {
    }
}

public sealed record RuntimeEventEntry(
    string EventId,
    string? Type,
    string Chapter,
    string ScriptId,
    bool IsEntry,
    RuntimeTrigger? Trigger);

public sealed record RuntimeTrigger(
    IReadOnlyList<RuntimeTriggerCondition> Conditions,
    IReadOnlyList<RuntimeTrigger> Or);

public sealed record RuntimeTriggerCondition(
    string Kind,
    string? From,
    string? Param,
    RuntimeTriggerValue? Value);

public sealed record RuntimeTriggerValue(
    string Kind,
    string Text);

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
}
