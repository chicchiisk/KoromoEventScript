using KoromoEventScript.Runtime.Core.Diagnostics;

namespace KoromoEventScript.Runtime.Windows.Bootstrap;

public sealed record WindowsRuntimeOptions(
    string ManifestPath,
    string? Locale = null,
    string? Start = null,
    bool Fullscreen = false,
    int? Width = null,
    int? Height = null,
    bool Debug = false,
    bool Profile = false);

public sealed record WindowsRuntimeBootstrapResult(
    WindowsRuntimeOptions? Options,
    IReadOnlyList<RuntimeDiagnostic> Diagnostics,
    RuntimeFailureKind FailureKind)
{
    public bool Succeeded => Options is not null && FailureKind == RuntimeFailureKind.None;

    public static WindowsRuntimeBootstrapResult Success(WindowsRuntimeOptions options)
    {
        return new WindowsRuntimeBootstrapResult(options, [], RuntimeFailureKind.None);
    }

    public static WindowsRuntimeBootstrapResult Failure(RuntimeFailureKind failureKind, params RuntimeDiagnostic[] diagnostics)
    {
        return new WindowsRuntimeBootstrapResult(null, diagnostics, failureKind);
    }
}
