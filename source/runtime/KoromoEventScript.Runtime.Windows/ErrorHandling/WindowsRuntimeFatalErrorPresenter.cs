using KoromoEventScript.Runtime.Core.Diagnostics;

namespace KoromoEventScript.Runtime.Windows.ErrorHandling;

public sealed record WindowsRuntimeProcessResult(
    RuntimeExitCode ExitCode,
    string Title,
    string PlayerMessage,
    IReadOnlyList<RuntimeDiagnostic> Diagnostics);

public sealed class WindowsRuntimeFatalErrorPresenter
{
    public WindowsRuntimeProcessResult Create(
        IReadOnlyList<RuntimeDiagnostic> diagnostics,
        RuntimeFailureKind failureKind,
        bool debug)
    {
        var primary = diagnostics.FirstOrDefault();
        var code = primary?.Code ?? "KESR0000";
        var title = Title(failureKind);
        var message = debug && primary is not null
            ? $"{PlayerMessage(failureKind)} ({code}) {primary.Message}"
            : $"{PlayerMessage(failureKind)} ({code})";

        return new WindowsRuntimeProcessResult(
            RuntimeExitCodeMapper.Map(failureKind),
            title,
            message,
            diagnostics.ToArray());
    }

    private static string Title(RuntimeFailureKind failureKind)
    {
        return failureKind switch
        {
            RuntimeFailureKind.Argument => "Runtime argument error",
            RuntimeFailureKind.Startup => "Runtime startup error",
            RuntimeFailureKind.Io => "Runtime file error",
            RuntimeFailureKind.Runtime => "Runtime error",
            _ => "Runtime error",
        };
    }

    private static string PlayerMessage(RuntimeFailureKind failureKind)
    {
        return failureKind switch
        {
            RuntimeFailureKind.Argument => "The runtime command line is invalid.",
            RuntimeFailureKind.Startup => "The game could not be started.",
            RuntimeFailureKind.Io => "A required game file could not be loaded.",
            RuntimeFailureKind.Runtime => "The game stopped because a runtime error occurred.",
            _ => "The game stopped because an unexpected error occurred.",
        };
    }
}
