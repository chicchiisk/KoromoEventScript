using System;
using KoromoEventScript.Runtime.Core.Diagnostics;

namespace KoromoEventScript.Unity
{

public enum KesHostOperationStatus
{
    Succeeded = 0,
    Cancelled = 1,
    Failed = 2,
}

public sealed class KesHostOperationResult
{
    private KesHostOperationResult(
        KesHostOperationStatus status,
        RuntimeDiagnostic diagnostic)
    {
        Status = status;
        Diagnostic = diagnostic;
    }

    public KesHostOperationStatus Status { get; }

    public RuntimeDiagnostic Diagnostic { get; }

    public static KesHostOperationResult Succeeded()
    {
        return new KesHostOperationResult(KesHostOperationStatus.Succeeded, null);
    }

    public static KesHostOperationResult Cancelled()
    {
        return new KesHostOperationResult(KesHostOperationStatus.Cancelled, null);
    }

    public static KesHostOperationResult Failed(RuntimeDiagnostic diagnostic)
    {
        return new KesHostOperationResult(
            KesHostOperationStatus.Failed,
            diagnostic ?? throw new ArgumentNullException(nameof(diagnostic)));
    }
}
}
