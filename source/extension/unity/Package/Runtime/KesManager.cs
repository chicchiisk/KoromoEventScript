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

    private KesVmExecutor executor;
    private KesVmSession session;
    private readonly List<RuntimeDiagnostic> lastDiagnostics = new();

    public KesBuildAsset BuildAsset => buildAsset;

    public bool PlayOnStart => playOnStart;

    public string Locale => locale;

    public string StartScriptId => startScriptId;

    public KesVmSession Session => session;

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

    public bool Play()
    {
        if (buildAsset == null)
        {
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

        var selected = FindScript();
        if (selected == null)
        {
            Report(
                RuntimeDiagnostic.Error(
                    "KESU2003",
                    $"No script was found for locale '{EffectiveLocale}' and script id '{startScriptId}'.",
                    RuntimeFailureKind.Startup));
            return false;
        }

        var loadResult = selected.Klib.LoadModule(selected.Klib.name);
        if (!loadResult.Succeeded || loadResult.Document == null)
        {
            Report(loadResult.Diagnostics);
            return false;
        }

        session = new KesVmSession(loadResult.Document);
        executor = new KesVmExecutor(effectSink: this);
        lastDiagnostics.Clear();
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

    private KesScriptAssetReference FindScript()
    {
        if (buildAsset == null)
        {
            return null;
        }

        for (var i = 0; i < buildAsset.Scripts.Count; i++)
        {
            var script = buildAsset.Scripts[i];
            if (!StringComparer.Ordinal.Equals(script.Locale, EffectiveLocale))
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
        return executor != null && session != null && HandleResult(executor.Run(session));
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
            Debug.LogError(diagnostic.Code + ": " + diagnostic.Message, this);
        }

        DiagnosticsPublished?.Invoke(lastDiagnostics);
    }
}
}
