using System;
using System.Collections.Generic;
using UnityEngine;

namespace KoromoEventScript.Unity
{

[Serializable]
public sealed class KesScriptAssetReference
{
    [SerializeField]
    private string scriptId = string.Empty;

    [SerializeField]
    private string locale = string.Empty;

    [SerializeField]
    private KesKlibAsset klib;

    public string ScriptId => scriptId;

    public string Locale => locale;

    public KesKlibAsset Klib => klib;

    public KesScriptAssetReference(string scriptId, string locale, KesKlibAsset klib)
    {
        this.scriptId = scriptId ?? throw new ArgumentNullException(nameof(scriptId));
        this.locale = locale ?? throw new ArgumentNullException(nameof(locale));
        this.klib = klib != null ? klib : throw new ArgumentNullException(nameof(klib));
    }
}

public sealed class KesBuildAsset : ScriptableObject
{
    [SerializeField]
    private string manifestJson = string.Empty;

    [SerializeField]
    private string schemaVersion = string.Empty;

    [SerializeField]
    private string gameId = string.Empty;

    [SerializeField]
    private string buildId = string.Empty;

    [SerializeField]
    private string defaultLocale = string.Empty;

    [SerializeField]
    private List<KesScriptAssetReference> scripts = new();

    public string ManifestJson => manifestJson;

    public string SchemaVersion => schemaVersion;

    public string GameId => gameId;

    public string BuildId => buildId;

    public string DefaultLocale => defaultLocale;

    public IReadOnlyList<KesScriptAssetReference> Scripts => scripts;

    public void SetImportedData(
        string importedManifestJson,
        string importedSchemaVersion,
        string importedGameId,
        string importedBuildId,
        string importedDefaultLocale,
        IEnumerable<KesScriptAssetReference> importedScripts)
    {
        manifestJson = importedManifestJson ?? throw new ArgumentNullException(nameof(importedManifestJson));
        schemaVersion = importedSchemaVersion ?? throw new ArgumentNullException(nameof(importedSchemaVersion));
        gameId = importedGameId ?? throw new ArgumentNullException(nameof(importedGameId));
        buildId = importedBuildId ?? throw new ArgumentNullException(nameof(importedBuildId));
        defaultLocale = importedDefaultLocale ?? throw new ArgumentNullException(nameof(importedDefaultLocale));
        scripts = new List<KesScriptAssetReference>(importedScripts ?? throw new ArgumentNullException(nameof(importedScripts)));
    }
}
}
