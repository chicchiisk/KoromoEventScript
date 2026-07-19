using System;
using System.Text.Json.Serialization;

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
    [NonSerialized, JsonInclude]
    public KesManifestEventData[] events = Array.Empty<KesManifestEventData>();
    public KesManifestLocalizationData[] localizations = Array.Empty<KesManifestLocalizationData>();
    public KesManifestBuildData build = new();
}

[Serializable]
public sealed class KesManifestScriptData
{
    public string scriptId = string.Empty;
    public string locale = string.Empty;
    public string klibPath = string.Empty;
    public bool isEntry;
    public string startLabel = string.Empty;
}

public sealed class KesManifestEventData
{
    public string eventId = string.Empty;
    public string type = string.Empty;
    public string chapter = string.Empty;
    public string scriptId = string.Empty;
    public bool isEntry;
    public KesManifestTriggerData trigger;
}

public sealed class KesManifestTriggerData
{
    public KesManifestTriggerConditionData[] conditions = Array.Empty<KesManifestTriggerConditionData>();
    public KesManifestTriggerData[] or = Array.Empty<KesManifestTriggerData>();
}

public sealed class KesManifestTriggerConditionData
{
    public string kind = string.Empty;
    public string from = string.Empty;
    public string param = string.Empty;
    public KesManifestTriggerValueData value;
}

public sealed class KesManifestTriggerValueData
{
    public string kind = string.Empty;
    public string text = string.Empty;
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
