using System;
using System.Collections.Generic;
using KoromoEventScript.Runtime.Core.Manifests;
using System.Text.Json;
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

    [SerializeField]
    private bool isEntry;

    [SerializeField]
    private string startLabel = string.Empty;

    public string ScriptId => scriptId;

    public string Locale => locale;

    public KesKlibAsset Klib => klib;

    public bool IsEntry => isEntry;

    public string StartLabel => startLabel;

    public KesScriptAssetReference(
        string scriptId,
        string locale,
        KesKlibAsset klib,
        bool isEntry = false,
        string startLabel = "")
    {
        this.scriptId = scriptId ?? throw new ArgumentNullException(nameof(scriptId));
        this.locale = locale ?? throw new ArgumentNullException(nameof(locale));
        this.klib = klib != null ? klib : throw new ArgumentNullException(nameof(klib));
        this.isEntry = isEntry;
        this.startLabel = startLabel ?? string.Empty;
    }
}

[Serializable]
public sealed class KesEventAssetReference
{
    [SerializeField]
    private string eventId = string.Empty;

    [SerializeField]
    private string type = string.Empty;

    [SerializeField]
    private string chapter = string.Empty;

    [SerializeField]
    private string scriptId = string.Empty;

    [SerializeField]
    private bool isEntry;

    [SerializeField]
    private string triggerJson = string.Empty;

    public KesEventAssetReference(
        string eventId,
        string type,
        string chapter,
        string scriptId,
        bool isEntry,
        KesTriggerAssetReference trigger)
    {
        this.eventId = eventId ?? throw new ArgumentNullException(nameof(eventId));
        this.type = type ?? string.Empty;
        this.chapter = chapter ?? string.Empty;
        this.scriptId = scriptId ?? throw new ArgumentNullException(nameof(scriptId));
        this.isEntry = isEntry;
        triggerJson = trigger == null
            ? string.Empty
            : JsonSerializer.Serialize(trigger.ToRuntimeTrigger());
    }

    public string EventId => eventId;

    public string Type => type;

    public string Chapter => chapter;

    public string ScriptId => scriptId;

    public bool IsEntry => isEntry;

    public RuntimeTrigger Trigger => string.IsNullOrEmpty(triggerJson)
        ? null
        : JsonSerializer.Deserialize<RuntimeTrigger>(triggerJson);
}

public sealed class KesTriggerAssetReference
{
    private List<KesTriggerConditionAssetReference> conditions = new();

    private List<KesTriggerAssetReference> or = new();

    public KesTriggerAssetReference(
        IEnumerable<KesTriggerConditionAssetReference> conditions,
        IEnumerable<KesTriggerAssetReference> or)
    {
        this.conditions = new List<KesTriggerConditionAssetReference>(conditions ?? Array.Empty<KesTriggerConditionAssetReference>());
        this.or = new List<KesTriggerAssetReference>(or ?? Array.Empty<KesTriggerAssetReference>());
    }

    public RuntimeTrigger ToRuntimeTrigger()
    {
        var runtimeConditions = new RuntimeTriggerCondition[conditions.Count];
        for (var i = 0; i < conditions.Count; i++)
        {
            runtimeConditions[i] = conditions[i].ToRuntimeCondition();
        }

        var runtimeOr = new RuntimeTrigger[or.Count];
        for (var i = 0; i < or.Count; i++)
        {
            runtimeOr[i] = or[i].ToRuntimeTrigger();
        }

        return new RuntimeTrigger(runtimeConditions, runtimeOr);
    }
}

public sealed class KesTriggerConditionAssetReference
{
    private string kind = string.Empty;

    private string from = string.Empty;

    private string param = string.Empty;

    private KesTriggerValueAssetReference value;

    public KesTriggerConditionAssetReference(
        string kind,
        string from,
        string param,
        KesTriggerValueAssetReference value)
    {
        this.kind = kind ?? string.Empty;
        this.from = from ?? string.Empty;
        this.param = param ?? string.Empty;
        this.value = value;
    }

    public RuntimeTriggerCondition ToRuntimeCondition()
    {
        return new RuntimeTriggerCondition(
            kind,
            string.IsNullOrEmpty(from) ? null : from,
            string.IsNullOrEmpty(param) ? null : param,
            value?.ToRuntimeValue());
    }
}

public sealed class KesTriggerValueAssetReference
{
    private string kind = string.Empty;

    private string text = string.Empty;

    public KesTriggerValueAssetReference(string kind, string text)
    {
        this.kind = kind ?? string.Empty;
        this.text = text ?? string.Empty;
    }

    public RuntimeTriggerValue ToRuntimeValue()
    {
        return new RuntimeTriggerValue(kind, text);
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

    [SerializeField]
    private List<KesEventAssetReference> events = new();

    public string ManifestJson => manifestJson;

    public string SchemaVersion => schemaVersion;

    public string GameId => gameId;

    public string BuildId => buildId;

    public string DefaultLocale => defaultLocale;

    public IReadOnlyList<KesScriptAssetReference> Scripts => scripts;

    public IReadOnlyList<KesEventAssetReference> Events => events;

    public void SetImportedData(
        string importedManifestJson,
        string importedSchemaVersion,
        string importedGameId,
        string importedBuildId,
        string importedDefaultLocale,
        IEnumerable<KesScriptAssetReference> importedScripts,
        IEnumerable<KesEventAssetReference> importedEvents = null)
    {
        manifestJson = importedManifestJson ?? throw new ArgumentNullException(nameof(importedManifestJson));
        schemaVersion = importedSchemaVersion ?? throw new ArgumentNullException(nameof(importedSchemaVersion));
        gameId = importedGameId ?? throw new ArgumentNullException(nameof(importedGameId));
        buildId = importedBuildId ?? throw new ArgumentNullException(nameof(importedBuildId));
        defaultLocale = importedDefaultLocale ?? throw new ArgumentNullException(nameof(importedDefaultLocale));
        scripts = new List<KesScriptAssetReference>(importedScripts ?? throw new ArgumentNullException(nameof(importedScripts)));
        events = new List<KesEventAssetReference>(importedEvents ?? Array.Empty<KesEventAssetReference>());
    }
}
}
