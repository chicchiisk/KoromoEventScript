using System;
using System.Collections.Generic;
using KoromoEventScript.Runtime.Core.Diagnostics;
using KoromoEventScript.Runtime.Core.Effects;
using KoromoEventScript.Runtime.Core.Execution;
using KoromoEventScript.Runtime.Core.Klib;
using KoromoEventScript.Runtime.Core.Manifests;
using KoromoEventScript.Runtime.Core.Persistence;
using KoromoEventScript.Runtime.Core.Stl;
using UnityEngine;

namespace KoromoEventScript.Unity
{

[DisallowMultipleComponent]
[AddComponentMenu("KoromoEventScript/KES Manager")]
public sealed class KesManager : MonoBehaviour, IRuntimeEffectSink, IKesInputTarget
{
    private const int MaxSynchronousEventTransitions = 100;

    [SerializeField]
    private KesBuildAsset buildAsset;

    [SerializeField]
    private bool playOnStart = true;

    [SerializeField]
    private string locale = string.Empty;

    [SerializeField]
    private string startScriptId = string.Empty;

    [SerializeField]
    [Tooltip("Logs the source event file, line, column, opcode, and bytecode offset before each VM instruction executes.")]
    private bool logExecutionSource;

    [SerializeField]
    private KesPresentation presentation;

    [SerializeField]
    private KesInputController inputController;

    [SerializeField]
    private MonoBehaviour saveHostBehaviour;

    private KesVmExecutor executor;
    private KesVmSession session;
    private RuntimeGameParameterStore gameParameters;
    private RuntimeTriggerEvaluator triggerEvaluator;
    [NonSerialized]
    private KesEventAssetReference currentEvent;
    private string activeLocale = string.Empty;
    private readonly List<RuntimeDiagnostic> lastDiagnostics = new();
    private readonly Queue<RuntimeEffect> pendingHostEffects = new();
    private IKesSaveHost saveHost;
    private int executionGeneration;
    private long nextOperationId;
    private long activeOperationId;
    private bool hostOperationActive;
    private bool hostInstructionWasRestored;
    private string pendingAdvanceEffectName = string.Empty;
    private static KesManager activeManager;

    public KesBuildAsset BuildAsset => buildAsset;

    public bool PlayOnStart => playOnStart;

    public string Locale => locale;

    public string StartScriptId => startScriptId;

    public bool LogExecutionSource => logExecutionSource;

    public string ActiveLocale => activeLocale;

    public string CurrentEventId => currentEvent == null ? string.Empty : currentEvent.EventId;

    public KesVmSession Session => session;

    public KesPresentation Presentation => presentation;

    public RuntimeContinuation Continuation => session == null
        ? RuntimeContinuation.Completed
        : session.Continuation;

    public IReadOnlyList<RuntimeDiagnostic> LastDiagnostics => lastDiagnostics;

    public event Action<RuntimeEffectBatch> EffectsPublished;

    public event Action<IReadOnlyList<RuntimeDiagnostic>> DiagnosticsPublished;

    public void SetBuildAsset(KesBuildAsset value)
    {
        buildAsset = value;
    }

    public void SetPlayOnStart(bool value)
    {
        playOnStart = value;
    }

    public void SetLocale(string value)
    {
        locale = value ?? string.Empty;
    }

    public void SetStartScriptId(string value)
    {
        startScriptId = value ?? string.Empty;
    }

    public void SetLogExecutionSource(bool value)
    {
        logExecutionSource = value;
    }

    public void SetPresentation(KesPresentation value)
    {
        presentation = value;
    }

    public void SetInputController(KesInputController value)
    {
        inputController = value;
    }

    public void SetSaveHost(IKesSaveHost value)
    {
        saveHost = value;
        saveHostBehaviour = value as MonoBehaviour;
    }

    public bool Play()
    {
        lastDiagnostics.Clear();
        executionGeneration++;
        pendingHostEffects.Clear();
        hostOperationActive = false;
        hostInstructionWasRestored = false;
        pendingAdvanceEffectName = string.Empty;

        if (buildAsset == null)
        {
            activeLocale = string.Empty;
            Report(
                RuntimeDiagnostic.Error(
                    "KESU2001",
                    "KES Manager cannot start because no KES Build Asset is assigned.",
                    RuntimeFailureKind.Startup));
            return false;
        }

        if (activeManager != null && activeManager != this && activeManager.IsRunning)
        {
            Report(RuntimeDiagnostic.Error(
                "KESU2010",
                "Another KES Manager is already running.",
                RuntimeFailureKind.Startup));
            return false;
        }

        if (session != null && session.Continuation.Kind != RuntimeContinuationKind.Completed)
        {
            Report(
                RuntimeDiagnostic.Error(
                    "KESU2002",
                    "KES Manager is already running.",
                    RuntimeFailureKind.Startup));
            return false;
        }

        activeLocale = string.Empty;
        currentEvent = ResolveStartEvent();
        gameParameters = new RuntimeGameParameterStore();
        triggerEvaluator = new RuntimeTriggerEvaluator(gameParameters);
        executor = new KesVmExecutor(
            syscallDispatcher: new StlSyscallDispatcher(
                this,
                gameParameters,
                waitForHostEffects: true,
                supportsRuntimeLocalization: false),
            effectSink: this,
            gameParameters: gameParameters,
            waitForHostEffects: true,
            instructionExecuting: TraceInstruction);
        activeManager = this;
        var requestedLocale = EffectiveLocale;
        var requestedScriptId = currentEvent == null
            ? startScriptId
            : currentEvent.ScriptId;
        var selected = FindScript(requestedLocale, requestedScriptId, out var usedLocaleFallback);
        if (selected == null)
        {
            Report(
                RuntimeDiagnostic.Error(
                    "KESU2003",
                    $"No script was found for locale '{EffectiveLocale}' and script id '{requestedScriptId}'.",
                    RuntimeFailureKind.Startup));
            return false;
        }

        if (usedLocaleFallback)
        {
            AppendDiagnostic(
                RuntimeDiagnostic.Warning(
                    "KESU2005",
                    $"Locale '{requestedLocale}' was not found. Falling back to default locale '{buildAsset.DefaultLocale}'."));
        }

        return LoadScript(selected) && RunUntilWait();
    }

    public bool ContinueAdvance()
    {
        if (executor == null || session == null)
        {
            return ReportNotStarted();
        }

        if (presentation != null && presentation.CompleteTyping())
        {
            return true;
        }

        if (StringComparer.Ordinal.Equals(pendingAdvanceEffectName, "text.p"))
        {
            presentation?.ClearDialoguePage();
        }

        pendingAdvanceEffectName = string.Empty;
        return HandleResult(executor.ContinueAdvance(session)) && RunUntilWait();
    }

    public bool ChooseSelection(int choiceIndex)
    {
        if (executor == null || session == null)
        {
            return ReportNotStarted();
        }

        return HandleResult(executor.ChooseSelection(session, choiceIndex)) && RunUntilWait();
    }

    public void Publish(RuntimeEffectBatch batch)
    {
        if (batch == null)
        {
            return;
        }

        for (var i = 0; i < batch.Effects.Count; i++)
        {
            var effect = batch.Effects[i];
            if (RequiresHostOperation(effect))
            {
                pendingHostEffects.Enqueue(effect);
                continue;
            }

            ApplySynchronousEffect(effect);
        }

        EffectsPublished?.Invoke(batch);
    }

    public void Stop()
    {
        executionGeneration++;
        pendingHostEffects.Clear();
        hostOperationActive = false;
        hostInstructionWasRestored = false;
        activeOperationId = 0;
        pendingAdvanceEffectName = string.Empty;
        StopAllCoroutines();
        presentation?.CancelOperations();
        presentation?.ResetPresentation();
        session?.Stop();
        if (activeManager == this)
        {
            activeManager = null;
        }
    }

    private void Start()
    {
        if (playOnStart)
        {
            Play();
        }
    }

    private void OnDisable()
    {
        Stop();
    }

    private void OnDestroy()
    {
        Stop();
    }

    private string EffectiveLocale => string.IsNullOrEmpty(locale)
        ? buildAsset != null ? buildAsset.DefaultLocale : string.Empty
        : locale;

    private KesScriptAssetReference FindScript(
        string requestedLocale,
        string requestedScriptId,
        out bool usedLocaleFallback)
    {
        usedLocaleFallback = false;
        if (buildAsset == null)
        {
            return null;
        }

        var selected = FindScriptForLocale(requestedLocale, requestedScriptId);
        if (selected != null)
        {
            return selected;
        }

        if (!StringComparer.Ordinal.Equals(requestedLocale, buildAsset.DefaultLocale))
        {
            selected = FindScriptForLocale(buildAsset.DefaultLocale, requestedScriptId);
            if (selected != null)
            {
                usedLocaleFallback = true;
                return selected;
            }
        }

        return null;
    }

    private KesScriptAssetReference FindScriptForLocale(string requestedLocale, string requestedScriptId)
    {
        KesScriptAssetReference first = null;
        KesScriptAssetReference entry = null;
        for (var i = 0; i < buildAsset.Scripts.Count; i++)
        {
            var script = buildAsset.Scripts[i];
            if (!StringComparer.Ordinal.Equals(script.Locale, requestedLocale))
            {
                continue;
            }

            first ??= script;
            if (script.IsEntry)
            {
                entry ??= script;
            }

            if (!string.IsNullOrEmpty(requestedScriptId) &&
                StringComparer.Ordinal.Equals(script.ScriptId, requestedScriptId))
            {
                return script;
            }
        }

        return string.IsNullOrEmpty(requestedScriptId) ? entry ?? first : null;
    }

    private bool RunUntilWait()
    {
        var synchronousTransitions = 0;
        while (executor != null && session != null)
        {
            if (!HandleResult(executor.Run(session)))
            {
                return false;
            }

            if (session.Continuation.Kind != RuntimeContinuationKind.Completed)
            {
                presentation?.ApplyContinuation(session.Continuation);
                if (session.Continuation.Kind == RuntimeContinuationKind.WaitingForHost)
                {
                    BeginNextHostEffect();
                }

                return true;
            }

            var transition = TryStartNextEvent();
            if (transition == EventTransitionResult.Failed)
            {
                return false;
            }

            if (transition == EventTransitionResult.None)
            {
                presentation?.ApplyContinuation(session.Continuation);
                return true;
            }

            synchronousTransitions++;
            if (synchronousTransitions >= MaxSynchronousEventTransitions)
            {
                Report(RuntimeDiagnostic.Error(
                    "KESU2009",
                    "Event transitions exceeded the synchronous transition limit before reaching an input wait.",
                    RuntimeFailureKind.Runtime));
                return false;
            }
        }

        return false;
    }

    private void TraceInstruction(KlibDocument document, KlibInstruction instruction)
    {
        if (!logExecutionSource || document == null || instruction == null)
        {
            return;
        }

        var scriptId = string.IsNullOrEmpty(document.Module.ScriptId)
            ? "<unknown-script>"
            : document.Module.ScriptId;
        var sourcePath = string.IsNullOrEmpty(document.Module.SourcePath)
            ? scriptId
            : document.Module.SourcePath;

        if (instruction.Source is KlibSourceLocation source)
        {
            Debug.Log(
                "[KES TRACE] " + sourcePath + ":" + source.Line + ":" + source.Column +
                " [" + instruction.OpCode + " @bytecode:" + instruction.Offset + "]",
                this);
            return;
        }

        Debug.Log(
            "[KES TRACE] " + scriptId + "@bytecode:" + instruction.Offset +
            " [" + instruction.OpCode + "]",
            this);
    }

    private KesEventAssetReference ResolveStartEvent()
    {
        if (buildAsset == null || buildAsset.Events.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(startScriptId))
        {
            for (var i = 0; i < buildAsset.Events.Count; i++)
            {
                var entry = buildAsset.Events[i];
                if (StringComparer.Ordinal.Equals(entry.EventId, startScriptId) ||
                    StringComparer.Ordinal.Equals(entry.ScriptId, startScriptId))
                {
                    return entry;
                }
            }

            return null;
        }

        for (var i = 0; i < buildAsset.Events.Count; i++)
        {
            if (buildAsset.Events[i].IsEntry)
            {
                return buildAsset.Events[i];
            }
        }

        return buildAsset.Events[0];
    }

    private EventTransitionResult TryStartNextEvent()
    {
        if (buildAsset == null || currentEvent == null || triggerEvaluator == null)
        {
            return EventTransitionResult.None;
        }

        KesEventAssetReference nextEvent = null;
        for (var i = 0; i < buildAsset.Events.Count; i++)
        {
            var candidate = buildAsset.Events[i];
            if (triggerEvaluator.IsMatch(candidate.Trigger, currentEvent.EventId))
            {
                nextEvent = candidate;
                break;
            }
        }

        if (nextEvent == null)
        {
            return EventTransitionResult.None;
        }

        var requestedLocale = EffectiveLocale;
        var selected = FindScript(requestedLocale, nextEvent.ScriptId, out var usedLocaleFallback);
        if (selected == null)
        {
            Report(RuntimeDiagnostic.Error(
                "KESU2006",
                $"No script was found for event '{nextEvent.EventId}' and script id '{nextEvent.ScriptId}'.",
                RuntimeFailureKind.Startup));
            return EventTransitionResult.Failed;
        }

        if (usedLocaleFallback)
        {
            AppendDiagnostic(RuntimeDiagnostic.Warning(
                "KESU2005",
                $"Locale '{requestedLocale}' was not found. Falling back to default locale '{buildAsset.DefaultLocale}'."));
        }

        if (!LoadScript(selected))
        {
            return EventTransitionResult.Failed;
        }

        currentEvent = nextEvent;
        return EventTransitionResult.Started;
    }

    private bool LoadScript(KesScriptAssetReference selected)
    {
        var loadResult = selected.Klib.LoadModule(selected.Klib.name);
        if (!loadResult.Succeeded || loadResult.Document == null)
        {
            Report(loadResult.Diagnostics);
            return false;
        }

        if (!StringComparer.Ordinal.Equals(loadResult.Document.Module.ScriptId, selected.ScriptId))
        {
            Report(RuntimeDiagnostic.Error(
                "KESU2007",
                $"Klib script id '{loadResult.Document.Module.ScriptId}' does not match manifest script id '{selected.ScriptId}'.",
                RuntimeFailureKind.Startup));
            return false;
        }

        session = new KesVmSession(loadResult.Document);
        activeLocale = selected.Locale;
        return true;
    }

    private enum EventTransitionResult
    {
        None = 0,
        Started = 1,
        Failed = 2,
    }

    private bool HandleResult(KesVmExecutionResult result)
    {
        if (result.Succeeded)
        {
            return true;
        }

        Report(result.Diagnostics);
        session?.Fault();
        return false;
    }

    private bool IsRunning => session != null &&
        session.Continuation.Kind is not RuntimeContinuationKind.Completed and
            not RuntimeContinuationKind.Faulted and
            not RuntimeContinuationKind.Stopped;

    private void BeginNextHostEffect()
    {
        if (hostOperationActive || session == null ||
            session.Continuation.Kind != RuntimeContinuationKind.WaitingForHost)
        {
            return;
        }

        if (pendingHostEffects.Count == 0)
        {
            CompleteHostInstruction();
            return;
        }

        var effect = pendingHostEffects.Dequeue();
        var generation = executionGeneration;
        var operationId = ++nextOperationId;
        activeOperationId = operationId;
        hostOperationActive = true;
        ExecuteHostEffect(
            effect,
            result => CompleteHostEffect(generation, operationId, result));
    }

    private void ExecuteHostEffect(
        RuntimeEffect effect,
        Action<KesHostOperationResult> completed)
    {
        switch (effect.Kind)
        {
            case RuntimeEffectKind.Scene:
            case RuntimeEffectKind.Audio:
                if (presentation == null)
                {
                    completed(Failed(
                        "KESU2011",
                        "KES Presentation is not configured for host effect '" + effect.Name + "'."));
                    return;
                }

                presentation.Execute(effect, completed);
                return;

            case RuntimeEffectKind.Wait:
                ExecuteWait(effect, completed);
                return;

            case RuntimeEffectKind.Save:
                ExecuteSave(effect, completed);
                return;

            default:
                completed(Failed(
                    "KESU2012",
                    "Unsupported host effect kind: " + effect.Kind));
                return;
        }
    }

    private void CompleteHostEffect(
        int generation,
        long operationId,
        KesHostOperationResult result)
    {
        if (generation != executionGeneration ||
            operationId != activeOperationId ||
            !hostOperationActive)
        {
            return;
        }

        hostOperationActive = false;
        activeOperationId = 0;
        result ??= Failed("KESU2013", "Host operation returned no result.");
        switch (result.Status)
        {
            case KesHostOperationStatus.Succeeded:
                BeginNextHostEffect();
                return;

            case KesHostOperationStatus.Cancelled:
                Stop();
                return;

            default:
                var diagnostic = result.Diagnostic ?? RuntimeDiagnostic.Error(
                    "KESU2013",
                    "Host operation failed.",
                    RuntimeFailureKind.Runtime);
                Report(diagnostic);
                pendingHostEffects.Clear();
                session?.Fault();
                return;
        }
    }

    private void CompleteHostInstruction()
    {
        if (session == null)
        {
            return;
        }

        if (hostInstructionWasRestored)
        {
            hostInstructionWasRestored = false;
            if (session.Continuation.Kind == RuntimeContinuationKind.Running)
            {
                RunUntilWait();
            }
            else
            {
                presentation?.ApplyContinuation(session.Continuation);
            }

            return;
        }

        var resume = session.ResumeHostOperation();
        if (!resume.Succeeded)
        {
            Report(resume.Diagnostics);
            session.Fault();
            return;
        }

        RunUntilWait();
    }

    private void ExecuteWait(
        RuntimeEffect effect,
        Action<KesHostOperationResult> completed)
    {
        if (!TryReadFloat(effect.Payload, "seconds", out var seconds) ||
            seconds < 0f ||
            float.IsNaN(seconds) ||
            float.IsInfinity(seconds))
        {
            completed(Failed("KESU2014", "system.wait requires a finite non-negative duration."));
            return;
        }

        if (seconds <= 0f)
        {
            completed(KesHostOperationResult.Succeeded());
            return;
        }

        StartCoroutine(WaitUnscaled(seconds, executionGeneration, completed));
    }

    private System.Collections.IEnumerator WaitUnscaled(
        float seconds,
        int generation,
        Action<KesHostOperationResult> completed)
    {
        var elapsed = 0f;
        while (elapsed < seconds && generation == executionGeneration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (generation == executionGeneration)
        {
            completed(KesHostOperationResult.Succeeded());
        }
    }

    private void ExecuteSave(
        RuntimeEffect effect,
        Action<KesHostOperationResult> completed)
    {
        ResolveSaveHost();
        if (saveHost == null)
        {
            completed(Failed(
                "KESU2015",
                "Save/load syscall requires an IKesSaveHost implementation."));
            return;
        }

        if (session == null)
        {
            completed(Failed("KESU2015", "No VM session is available for save/load."));
            return;
        }

        switch (effect.Name)
        {
            case "state.save":
            case "state.autosave":
                var slot = effect.Name == "state.autosave"
                    ? -1
                    : ReadInt(effect.Payload, "slot", -1);
                var request = new KesSaveRequest(
                    slot,
                    Read(effect.Payload, "title", string.Empty),
                    effect.Name == "state.autosave",
                    buildAsset == null ? string.Empty : buildAsset.GameId,
                    buildAsset == null ? string.Empty : buildAsset.BuildId,
                    CurrentEventId,
                    activeLocale,
                    session.CaptureSnapshotAfterHostOperation());
                saveHost.Save(
                    request,
                    result =>
                    {
                        if (result?.Status == KesHostOperationStatus.Cancelled &&
                            StringComparer.Ordinal.Equals(effect.Name, "state.save"))
                        {
                            completed(KesHostOperationResult.Succeeded());
                            return;
                        }

                        completed(result ?? Failed("KESU2016", "Save host returned no result."));
                    });
                return;

            case "state.load":
                var loadSlot = ReadInt(effect.Payload, "slot", -1);
                saveHost.Load(
                    loadSlot,
                    (snapshot, result) =>
                    {
                        if (result?.Status == KesHostOperationStatus.Cancelled)
                        {
                            completed(KesHostOperationResult.Succeeded());
                            return;
                        }

                        if (result == null || result.Status != KesHostOperationStatus.Succeeded)
                        {
                            completed(result ?? Failed("KESU2016", "Load host returned no result."));
                            return;
                        }

                        if (snapshot == null)
                        {
                            completed(Failed("KESU2016", "Load host returned no snapshot."));
                            return;
                        }

                        var restore = RestoreVmSnapshot(snapshot);
                        completed(restore);
                    });
                return;

            default:
                completed(Failed("KESU2015", "Unsupported save effect: " + effect.Name));
                return;
        }
    }

    private KesHostOperationResult RestoreVmSnapshot(RuntimeSaveSnapshot snapshot)
    {
        if (session == null ||
            !StringComparer.Ordinal.Equals(session.Document.Module.ScriptId, snapshot.Position.ScriptId))
        {
            var script = FindScript(activeLocale, snapshot.Position.ScriptId, out _);
            if (script == null || !LoadScript(script))
            {
                return Failed(
                    "KESU2016",
                    "Could not resolve snapshot script '" + snapshot.Position.ScriptId + "'.");
            }
        }

        var restore = session.Restore(snapshot);
        if (!restore.Succeeded)
        {
            return KesHostOperationResult.Failed(restore.Diagnostics[0]);
        }

        hostInstructionWasRestored = true;
        return KesHostOperationResult.Succeeded();
    }

    private void ApplySynchronousEffect(RuntimeEffect effect)
    {
        if (effect.Kind == RuntimeEffectKind.Ui)
        {
            presentation?.Apply(new RuntimeEffectBatch(
                new[] { effect },
                Array.Empty<RuntimeDiagnostic>()));
            if (effect.Name is "text.p" or "text.l" or "text.wait_click" or
                "scenario.say" or "scenario.nar")
            {
                pendingAdvanceEffectName = effect.Name;
            }

            return;
        }

        if (effect.Kind == RuntimeEffectKind.Settings)
        {
            ApplySettings(effect);
        }
    }

    private void ApplySettings(RuntimeEffect effect)
    {
        switch (effect.Name)
        {
            case "system.set_auto":
                inputController?.SetAutoEnabled(ReadBool(effect.Payload, "enabled", false));
                break;

            case "system.set_skip":
                inputController?.SetSkipMode(Read(effect.Payload, "mode", "off"));
                break;

            case "system.set_config_string":
            case "system.set_config_number":
            case "system.set_config_bool":
                var key = Read(effect.Payload, "key", string.Empty);
                var value = Read(effect.Payload, "value", string.Empty);
                if (StringComparer.Ordinal.Equals(key, "locale"))
                {
                    locale = value;
                }
                else if (StringComparer.Ordinal.Equals(key, "autoSpeed") &&
                    float.TryParse(
                        value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var autoSpeed))
                {
                    inputController?.SetAutoInterval(autoSpeed);
                }
                else if (StringComparer.Ordinal.Equals(key, "textSpeed") &&
                    float.TryParse(
                        value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var textSpeed))
                {
                    presentation?.SetTextSpeed(textSpeed);
                }
                else if (key is "masterVolume" or "bgmVolume" or "seVolume" or "voiceVolume" &&
                    float.TryParse(
                        value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var volume))
                {
                    presentation?.SetAudioVolume(key, volume);
                }

                break;
        }
    }

    private void ResolveSaveHost()
    {
        if (saveHost == null && saveHostBehaviour != null)
        {
            saveHost = saveHostBehaviour as IKesSaveHost;
        }
    }

    private static bool RequiresHostOperation(RuntimeEffect effect)
    {
        if (effect == null)
        {
            return false;
        }

        return effect.Kind is RuntimeEffectKind.Scene or RuntimeEffectKind.Audio ||
            (effect.Kind == RuntimeEffectKind.Wait &&
                StringComparer.Ordinal.Equals(effect.Name, "system.wait")) ||
            (effect.Kind == RuntimeEffectKind.Save &&
                effect.Name is "state.save" or "state.autosave" or "state.load");
    }

    private static KesHostOperationResult Failed(string code, string message)
    {
        return KesHostOperationResult.Failed(RuntimeDiagnostic.Error(
            code,
            message,
            RuntimeFailureKind.Runtime));
    }

    private static string Read(
        IReadOnlyDictionary<string, string> payload,
        string key,
        string fallback)
    {
        return payload != null && payload.TryGetValue(key, out var value) && value != null
            ? value
            : fallback;
    }

    private static int ReadInt(
        IReadOnlyDictionary<string, string> payload,
        string key,
        int fallback)
    {
        return int.TryParse(
            Read(payload, key, string.Empty),
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,
            out var value)
            ? value
            : fallback;
    }

    private static bool ReadBool(
        IReadOnlyDictionary<string, string> payload,
        string key,
        bool fallback)
    {
        return bool.TryParse(Read(payload, key, string.Empty), out var value)
            ? value
            : fallback;
    }

    private static bool TryReadFloat(
        IReadOnlyDictionary<string, string> payload,
        string key,
        out float value)
    {
        return float.TryParse(
            Read(payload, key, string.Empty),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out value);
    }

    private bool ReportNotStarted()
    {
        Report(
            RuntimeDiagnostic.Error(
                "KESU2004",
                "KES Manager has not started a script.",
                RuntimeFailureKind.Startup));
        return false;
    }

    private void Report(RuntimeDiagnostic diagnostic)
    {
        Report(new[] { diagnostic });
    }

    private void Report(IReadOnlyList<RuntimeDiagnostic> diagnostics)
    {
        lastDiagnostics.Clear();
        for (var i = 0; i < diagnostics.Count; i++)
        {
            var diagnostic = diagnostics[i];
            lastDiagnostics.Add(diagnostic);
            LogDiagnostic(diagnostic);
        }

        DiagnosticsPublished?.Invoke(lastDiagnostics);
    }

    private void AppendDiagnostic(RuntimeDiagnostic diagnostic)
    {
        lastDiagnostics.Add(diagnostic);
        LogDiagnostic(diagnostic);
        DiagnosticsPublished?.Invoke(lastDiagnostics);
    }

    private void LogDiagnostic(RuntimeDiagnostic diagnostic)
    {
        var message = diagnostic.Code + ": " + diagnostic.Message;
        switch (diagnostic.Severity)
        {
            case RuntimeDiagnosticSeverity.Warning:
                Debug.LogWarning(message, this);
                break;

            case RuntimeDiagnosticSeverity.Info:
                Debug.Log(message, this);
                break;

            default:
                Debug.LogError(message, this);
                break;
        }
    }
}
}
