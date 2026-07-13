using System;

namespace KoromoEventScript.Unity
{

[Serializable]
public sealed class KesManifestData
{
    public string schemaVersion = string.Empty;
    public string gameId = string.Empty;
    public string defaultLocale = string.Empty;
    public string target = string.Empty;
    public KesManifestScriptData[] scripts = Array.Empty<KesManifestScriptData>();
    public KesManifestLocalizationData[] localizations = Array.Empty<KesManifestLocalizationData>();
    public KesManifestBuildData build = new();
}

[Serializable]
public sealed class KesManifestScriptData
{
    public string scriptId = string.Empty;
    public string locale = string.Empty;
    public string klibPath = string.Empty;
}

[Serializable]
public sealed class KesManifestLocalizationData
{
    public string locale = string.Empty;
    public KesManifestScriptData[] scripts = Array.Empty<KesManifestScriptData>();
}

[Serializable]
public sealed class KesManifestBuildData
{
    public string buildId = string.Empty;
}
}
