#nullable enable

using System.Collections.Generic;
using KoromoEventScript.Runtime.Core.Diagnostics;

namespace KoromoEventScript.Runtime.Core.Effects
{

public enum RuntimeEffectKind
{
    Scene = 1,
    Audio = 2,
    Wait = 3,
    Ui = 4,
    Save = 5,
    Settings = 6,
    Diagnostic = 7,
}

public enum RuntimeWaitKind
{
    Click = 1,
    Choice = 2,
    Timed = 3,
    Audio = 4,
}

public sealed record RuntimeEffect(
    RuntimeEffectKind Kind,
    string Name,
    IReadOnlyDictionary<string, string?> Payload)
{
    public static RuntimeEffect Wait(RuntimeWaitKind waitKind)
    {
        return new RuntimeEffect(
            RuntimeEffectKind.Wait,
            waitKind.ToString(),
            new Dictionary<string, string?>
            {
                ["kind"] = waitKind.ToString(),
            });
    }

    public static RuntimeEffect Diagnostic(RuntimeDiagnostic diagnostic)
    {
        return new RuntimeEffect(
            RuntimeEffectKind.Diagnostic,
            diagnostic.Code,
            new Dictionary<string, string?>
            {
                ["severity"] = diagnostic.Severity.ToString(),
                ["message"] = diagnostic.Message,
                ["failureKind"] = diagnostic.FailureKind.ToString(),
            });
    }
}

public sealed record RuntimeEffectBatch(
    IReadOnlyList<RuntimeEffect> Effects,
    IReadOnlyList<RuntimeDiagnostic> Diagnostics);

public interface IRuntimeEffectSink
{
    void Publish(RuntimeEffectBatch batch);
}
}
