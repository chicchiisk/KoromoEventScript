using System.Globalization;
using KoromoEventScript.Runtime.Core.Diagnostics;
using KoromoEventScript.Runtime.Core.Effects;
using KoromoEventScript.Runtime.Core.Execution;

namespace KoromoEventScript.Runtime.Core.Stl;

public interface IRuntimeSyscallDispatcher
{
    RuntimeSyscallResult Invoke(RuntimeSyscallInvocation invocation, KesVmSession session);
}

public sealed record RuntimeSyscallInvocation(
    string Id,
    IReadOnlyList<RuntimeValue> Arguments,
    bool ExpectsReturnValue,
    RuntimeSourceLocation Location);

public sealed record RuntimeSyscallResult(
    bool Succeeded,
    RuntimeValue? ReturnValue,
    IReadOnlyList<RuntimeDiagnostic> Diagnostics,
    RuntimeFailureKind FailureKind)
{
    public static RuntimeSyscallResult Success(RuntimeValue? returnValue = null)
    {
        return new RuntimeSyscallResult(true, returnValue, [], RuntimeFailureKind.None);
    }

    public static RuntimeSyscallResult Failure(RuntimeFailureKind failureKind, params RuntimeDiagnostic[] diagnostics)
    {
        return new RuntimeSyscallResult(false, null, diagnostics, failureKind);
    }
}

public sealed class StlSyscallDispatcher : IRuntimeSyscallDispatcher
{
    private readonly IRuntimeEffectSink? effectSink;

    public StlSyscallDispatcher(IRuntimeEffectSink? effectSink = null)
    {
        this.effectSink = effectSink;
    }

    public RuntimeSyscallResult Invoke(RuntimeSyscallInvocation invocation, KesVmSession session)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(session);

        return invocation.Id switch
        {
            "core.print" => Print(invocation),
            "core.array_len" => ArrayLength(invocation, session),
            "core.str_len" => StringLength(invocation),
            "core.range" => Range(invocation, session),
            "core.number_to_string" => NumberToString(invocation),
            "core.bool_to_string" => BoolToString(invocation),
            "core.assert" => Assert(invocation),
            _ => RuntimeSyscallResult.Failure(
                RuntimeFailureKind.Runtime,
                Error("KESR3400", invocation, $"Runtime syscall '{invocation.Id}' is not supported.")),
        };
    }

    private RuntimeSyscallResult Print(RuntimeSyscallInvocation invocation)
    {
        if (invocation.Arguments.Count != 1 || invocation.Arguments[0].Kind != RuntimeValueKind.String)
        {
            return RuntimeSyscallResult.Failure(
                RuntimeFailureKind.Runtime,
                Error("KESR3402", invocation, "Syscall 'core.print' requires one string argument."));
        }

        var message = invocation.Arguments[0].StringValue ?? string.Empty;
        var diagnostic = RuntimeDiagnostic.Info("KESR3401", message, invocation.Location);
        effectSink?.Publish(new RuntimeEffectBatch([RuntimeEffect.Diagnostic(diagnostic)], [diagnostic]));
        return RuntimeSyscallResult.Success();
    }

    private static RuntimeSyscallResult ArrayLength(RuntimeSyscallInvocation invocation, KesVmSession session)
    {
        if (invocation.Arguments.Count != 1 ||
            invocation.Arguments[0].Kind != RuntimeValueKind.Reference ||
            string.IsNullOrEmpty(invocation.Arguments[0].ReferenceId))
        {
            return RuntimeSyscallResult.Failure(
                RuntimeFailureKind.Runtime,
                Error("KESR3402", invocation, "Syscall 'core.array_len' requires one array reference argument."));
        }

        if (!session.ObjectStore.TryGetArrayLength(invocation.Arguments[0].ReferenceId!, out var length, out var error))
        {
            return RuntimeSyscallResult.Failure(
                RuntimeFailureKind.Runtime,
                Error("KESR3402", invocation, error ?? "Syscall 'core.array_len' failed."));
        }

        return RuntimeSyscallResult.Success(RuntimeValue.Number(length));
    }

    private static RuntimeSyscallResult StringLength(RuntimeSyscallInvocation invocation)
    {
        if (invocation.Arguments.Count != 1 || invocation.Arguments[0].Kind != RuntimeValueKind.String)
        {
            return RuntimeSyscallResult.Failure(
                RuntimeFailureKind.Runtime,
                Error("KESR3402", invocation, "Syscall 'core.str_len' requires one string argument."));
        }

        return RuntimeSyscallResult.Success(RuntimeValue.Number(invocation.Arguments[0].StringValue?.Length ?? 0));
    }

    private static RuntimeSyscallResult Range(RuntimeSyscallInvocation invocation, KesVmSession session)
    {
        if (!TryReadNumberPair(invocation, "core.range", out var start, out var end, out var failure))
        {
            return failure!;
        }

        var values = new List<RuntimeValue>();
        for (var current = start; current < end; current += 1)
        {
            values.Add(RuntimeValue.Number(current));
        }

        return RuntimeSyscallResult.Success(session.ObjectStore.CreateArray(values));
    }

    private static RuntimeSyscallResult NumberToString(RuntimeSyscallInvocation invocation)
    {
        if (invocation.Arguments.Count != 1 ||
            invocation.Arguments[0].Kind != RuntimeValueKind.Number ||
            invocation.Arguments[0].NumberValue is null)
        {
            return RuntimeSyscallResult.Failure(
                RuntimeFailureKind.Runtime,
                Error("KESR3402", invocation, "Syscall 'core.number_to_string' requires one number argument."));
        }

        var value = invocation.Arguments[0].NumberValue.GetValueOrDefault();
        return RuntimeSyscallResult.Success(RuntimeValue.String(value.ToString("G", CultureInfo.InvariantCulture)));
    }

    private static RuntimeSyscallResult BoolToString(RuntimeSyscallInvocation invocation)
    {
        if (invocation.Arguments.Count != 1 ||
            invocation.Arguments[0].Kind != RuntimeValueKind.Bool ||
            invocation.Arguments[0].BoolValue is null)
        {
            return RuntimeSyscallResult.Failure(
                RuntimeFailureKind.Runtime,
                Error("KESR3402", invocation, "Syscall 'core.bool_to_string' requires one bool argument."));
        }

        var value = invocation.Arguments[0].BoolValue.GetValueOrDefault();
        return RuntimeSyscallResult.Success(RuntimeValue.String(value ? "true" : "false"));
    }

    private static RuntimeSyscallResult Assert(RuntimeSyscallInvocation invocation)
    {
        if (invocation.Arguments.Count is < 1 or > 2 ||
            invocation.Arguments[0].Kind != RuntimeValueKind.Bool ||
            invocation.Arguments[0].BoolValue is null ||
            (invocation.Arguments.Count == 2 && invocation.Arguments[1].Kind != RuntimeValueKind.String))
        {
            return RuntimeSyscallResult.Failure(
                RuntimeFailureKind.Runtime,
                Error("KESR3402", invocation, "Syscall 'core.assert' requires a bool condition and optional string message."));
        }

        var condition = invocation.Arguments[0].BoolValue.GetValueOrDefault();
        if (condition)
        {
            return RuntimeSyscallResult.Success();
        }

        var message = invocation.Arguments.Count == 2
            ? invocation.Arguments[1].StringValue ?? string.Empty
            : "Assertion failed.";
        return RuntimeSyscallResult.Failure(
            RuntimeFailureKind.Runtime,
            Error("KESR3403", invocation, $"Assertion failed: {message}"));
    }

    private static bool TryReadNumberPair(
        RuntimeSyscallInvocation invocation,
        string syscallId,
        out double start,
        out double end,
        out RuntimeSyscallResult? failure)
    {
        start = 0;
        end = 0;
        failure = null;

        if (invocation.Arguments.Count != 2 ||
            invocation.Arguments[0].Kind != RuntimeValueKind.Number ||
            invocation.Arguments[1].Kind != RuntimeValueKind.Number ||
            invocation.Arguments[0].NumberValue is null ||
            invocation.Arguments[1].NumberValue is null)
        {
            failure = RuntimeSyscallResult.Failure(
                RuntimeFailureKind.Runtime,
                Error("KESR3402", invocation, $"Syscall '{syscallId}' requires two number arguments."));
            return false;
        }

        var firstValue = invocation.Arguments[0].NumberValue.GetValueOrDefault();
        var secondValue = invocation.Arguments[1].NumberValue.GetValueOrDefault();
        start = firstValue;
        end = secondValue;
        return true;
    }

    private static RuntimeDiagnostic Error(string code, RuntimeSyscallInvocation invocation, string message)
    {
        return RuntimeDiagnostic.Error(code, message, RuntimeFailureKind.Runtime, invocation.Location);
    }
}
