using System;
using System.Collections.Generic;
using KoromoEventScript.Runtime.Core.Diagnostics;
using KoromoEventScript.Runtime.Core.Persistence;
using KoromoEventScript.Unity;
using UnityEngine;

[AddComponentMenu("KoromoEventScript/Sample/In-Memory Save Host")]
public sealed class KesSampleSaveHost : MonoBehaviour, IKesSaveHost
{
    private readonly Dictionary<int, RuntimeSaveSnapshot> snapshots = new();

    public void Save(KesSaveRequest request, Action<KesHostOperationResult> completed)
    {
        if (request == null)
        {
            completed?.Invoke(KesHostOperationResult.Failed(RuntimeDiagnostic.Error(
                "KESS3001",
                "The sample save host received an empty save request.",
                RuntimeFailureKind.Runtime)));
            return;
        }

        snapshots[request.Slot] = request.Snapshot;
        Debug.Log(
            $"KES sample saved slot {request.Slot}: {request.Title} " +
            $"({request.Snapshot.Position.ScriptId}@{request.Snapshot.Position.InstructionIndex})");
        completed?.Invoke(KesHostOperationResult.Succeeded());
    }

    public void Load(int slot, Action<RuntimeSaveSnapshot, KesHostOperationResult> completed)
    {
        if (!snapshots.TryGetValue(slot, out var snapshot))
        {
            completed?.Invoke(
                null,
                KesHostOperationResult.Failed(RuntimeDiagnostic.Error(
                    "KESS3002",
                    $"The sample save slot {slot} does not exist.",
                    RuntimeFailureKind.Runtime)));
            return;
        }

        Debug.Log(
            $"KES sample loaded slot {slot}: " +
            $"{snapshot.Position.ScriptId}@{snapshot.Position.InstructionIndex}");
        completed?.Invoke(snapshot, KesHostOperationResult.Succeeded());
    }
}
