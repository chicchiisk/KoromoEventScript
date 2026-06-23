using KoromoEventScript.Runtime.Core.Diagnostics;
using KoromoEventScript.Runtime.Core.Execution;
using KoromoEventScript.Runtime.Windows.Audio;
using KoromoEventScript.Runtime.Windows.Input;

namespace KoromoEventScript.Runtime.Windows.Diagnostics;

public enum RuntimeDiagnosticsMode
{
    Normal,
    Debug,
}

public sealed record RuntimeResourceDiagnostics(
    int LoadedAssetCount,
    IReadOnlyList<string> UnresolvedAssetIds);

public sealed record RuntimeDiagnosticsSnapshot(
    double Fps,
    RuntimeExecutionPosition VmPosition,
    RuntimeResourceDiagnostics Resources,
    AudioServiceState Audio,
    RuntimeInputEvent? LastInput,
    IReadOnlyList<RuntimeDiagnostic> Diagnostics);
