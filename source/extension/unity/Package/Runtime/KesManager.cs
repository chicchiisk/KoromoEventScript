using System;
using System.Collections.Generic;
using KoromoEventScript.Runtime.Core.Diagnostics;
using KoromoEventScript.Runtime.Core.Effects;
using KoromoEventScript.Runtime.Core.Execution;
using KoromoEventScript.Runtime.Core.Klib;
using UnityEngine;

namespace KoromoEventScript.Unity
{

[DisallowMultipleComponent]
[AddComponentMenu("KoromoEventScript/KES Manager")]
public sealed class KesManager : MonoBehaviour, IRuntimeEffectSink
{
    [SerializeField]
    private KesBuildAsset buildAsset;

    [SerializeField]
    private bool playOnStart = true;

    [SerializeField]
    private string locale = string.Empty;

    [SerializeField]
    private string startScriptId = string.Empty;

    [SerializeField]
    private KesPresentation presentation;

    private KesVmExecutor executor;
    private KesVmSession session;
    private string activeLocale = string.Empty;
    private readonly List<RuntimeDiagnostic> lastDiagnostics = new();

    public KesBuildAsset BuildAsset => buildAsset;

    public bool PlayOnStart => playOnStart;

    public string Locale => locale;

    public string StartScriptId => startScriptId;

    public string ActiveLocale => activeLocale;

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

    public void SetPresentation(KesPresentation value)
    {
        presentation = value;
    }

    public bool Play()
    {
        lastDiagnostics.Clear();

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
        var requestedLocale = EffectiveLocale;
        var selected = FindScript(requestedLocale, out var usedLocaleFallback);
        if (selected == null)
        {
            Report(
                RuntimeDiagnostic.Error(
                    "KESU2003",
                    $"No script was found for locale '{EffectiveLocale}' and script id '{startScriptId}'.",
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

        var loadResult = selected.Klib.LoadModule(selected.Klib.name);
        if (!loadResult.Succeeded || loadResult.Document == null)
        {
            Report(loadResult.Diagnostics);
            return false;
        }

        session = new KesVmSession(loadResult.Document);
        executor = new KesVmExecutor(effectSink: this);
        activeLocale = selected.Locale;
        return RunUntilWait();
    }

    public bool ContinueAdvance()
    {
        if (executor == null || session == null)
        {
            return ReportNotStarted();
        }

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
        presentation?.Apply(batch);
        EffectsPublished?.Invoke(batch);
    }

    private void Start()
    {
        if (playOnStart)
        {
            Play();
        }
    }

    private string EffectiveLocale => string.IsNullOrEmpty(locale)
        ? buildAsset != null ? buildAsset.DefaultLocale : string.Empty
        : locale;

    private KesScriptAssetReference FindScript(string requestedLocale, out bool usedLocaleFallback)
    {
        usedLocaleFallback = false;
        if (buildAsset == null)
        {
            return null;
        }

        var selected = FindScriptForLocale(requestedLocale);
        if (selected != null)
        {
            return selected;
        }

        if (!StringComparer.Ordinal.Equals(requestedLocale, buildAsset.DefaultLocale))
        {
            selected = FindScriptForLocale(buildAsset.DefaultLocale);
            if (selected != null)
            {
                usedLocaleFallback = true;
                return selected;
            }
        }

        return null;
    }

    private KesScriptAssetReference FindScriptForLocale(string requestedLocale)
    {
        for (var i = 0; i < buildAsset.Scripts.Count; i++)
        {
            var script = buildAsset.Scripts[i];
            if (!StringComparer.Ordinal.Equals(script.Locale, requestedLocale))
            {
                continue;
            }

            if (string.IsNullOrEmpty(startScriptId) ||
                StringComparer.Ordinal.Equals(script.ScriptId, startScriptId))
            {
                return script;
            }
        }

        return null;
    }

    private bool RunUntilWait()
    {
        if (executor == null || session == null || !HandleResult(executor.Run(session)))
        {
            return false;
        }

        presentation?.ApplyContinuation(session.Continuation);
        return true;
    }

    private bool HandleResult(KesVmExecutionResult result)
    {
        if (result.Succeeded)
        {
            return true;
        }

        Report(result.Diagnostics);
        return false;
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
