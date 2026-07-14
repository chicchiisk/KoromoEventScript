#nullable enable

namespace KoromoEventScript.Runtime.Core.Diagnostics
{

public enum RuntimeDiagnosticSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2,
}

public enum RuntimeFailureKind
{
    None = 0,
    General = 1,
    Argument = 2,
    Runtime = 3,
    Io = 4,
    Startup = 5,
}

public enum RuntimeExitCode
{
    Success = 0,
    GeneralError = 1,
    CommandLineError = 2,
    RuntimeError = 5,
    FileOrDirectoryError = 6,
    RuntimeStartupError = 7,
}

public readonly struct RuntimeSourceLocation
{
    public RuntimeSourceLocation(string? scriptId, int? instructionIndex, string? file, int? line, int? column)
    {
        ScriptId = scriptId;
        InstructionIndex = instructionIndex;
        File = file;
        Line = line;
        Column = column;
    }

    public string? ScriptId { get; }

    public int? InstructionIndex { get; }

    public string? File { get; }

    public int? Line { get; }

    public int? Column { get; }
}

public sealed record RuntimeDiagnostic(
    RuntimeDiagnosticSeverity Severity,
    string Code,
    string Message,
    RuntimeSourceLocation? Location = null,
    RuntimeFailureKind FailureKind = RuntimeFailureKind.None)
{
    public static RuntimeDiagnostic Info(string code, string message, RuntimeSourceLocation? location = null)
    {
        return new RuntimeDiagnostic(RuntimeDiagnosticSeverity.Info, code, message, location);
    }

    public static RuntimeDiagnostic Warning(string code, string message, RuntimeSourceLocation? location = null)
    {
        return new RuntimeDiagnostic(RuntimeDiagnosticSeverity.Warning, code, message, location);
    }

    public static RuntimeDiagnostic Error(
        string code,
        string message,
        RuntimeFailureKind failureKind,
        RuntimeSourceLocation? location = null)
    {
        return new RuntimeDiagnostic(RuntimeDiagnosticSeverity.Error, code, message, location, failureKind);
    }
}

public static class RuntimeExitCodeMapper
{
    public static RuntimeExitCode Map(RuntimeFailureKind failureKind)
    {
        return failureKind switch
        {
            RuntimeFailureKind.None => RuntimeExitCode.Success,
            RuntimeFailureKind.Argument => RuntimeExitCode.CommandLineError,
            RuntimeFailureKind.Runtime => RuntimeExitCode.RuntimeError,
            RuntimeFailureKind.Io => RuntimeExitCode.FileOrDirectoryError,
            RuntimeFailureKind.Startup => RuntimeExitCode.RuntimeStartupError,
            RuntimeFailureKind.General => RuntimeExitCode.GeneralError,
            _ => RuntimeExitCode.GeneralError,
        };
    }
}
}
