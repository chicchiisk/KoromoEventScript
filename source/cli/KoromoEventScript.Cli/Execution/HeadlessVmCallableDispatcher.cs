using KoromoEventScript.Cli.Semantics;

namespace KoromoEventScript.Cli.Execution;

public enum HeadlessVmCallableOutcomeKind
{
    Continue = 0,
    WaitForAdvance = 1,
    Fault = 2,
}

public sealed record HeadlessVmCallableResult(
    HeadlessVmObservationLog Observation,
    HeadlessVmCallableOutcomeKind Outcome = HeadlessVmCallableOutcomeKind.Continue,
    HeadlessVmRuntimeValue? ReturnValue = null,
    bool HasReturnValue = false,
    string? FaultMessage = null);

public sealed class HeadlessVmCallableDispatcher
{
    private readonly BuiltInSignatureRegistry builtIns = new();

    public HeadlessVmCallableResult InvokeCall(
        string name,
        IReadOnlyList<HeadlessVmRuntimeValue> arguments,
        bool returnsValue,
        HeadlessVmObjectStore objectStore,
        HeadlessVmObservationLog observation)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(objectStore);
        ArgumentNullException.ThrowIfNull(observation);

        if (!builtIns.TryResolve(name, out var signature))
        {
            return Fault(observation, $"Unknown callable '{name}'.");
        }

        var requiredCount = signature.Parameters.Count(static parameter => !parameter.IsOptional);
        if (arguments.Count < requiredCount || arguments.Count > signature.Parameters.Count)
        {
            return Fault(observation, $"Callable '{name}' received invalid argument count '{arguments.Count}'.");
        }

        return name switch
        {
            "number_to_string" => ConvertNumberToString(arguments, observation),
            "bool_to_string" => ConvertBoolToString(arguments, observation),
            "array_len" => GetArrayLength(arguments, objectStore, observation),
            "str_len" => GetStringLength(arguments, observation),
            "range" => CreateRange(arguments, objectStore, observation),
            "assert" => InvokeAssert(arguments, observation),
            "standby" => InvokeStandby(arguments, objectStore, observation),
            "show" => InvokeShow(arguments, objectStore, observation),
            "hide" => InvokeHide(arguments, objectStore, observation),
            "face" => InvokeFace(arguments, objectStore, observation),
            "move" => InvokeMove(arguments, objectStore, observation),
            "action_jump" => Continue(observation),
            _ when !returnsValue => Continue(observation),
            _ => Fault(observation, $"Callable '{name}' is not yet supported in headless mode."),
        };
    }

    public HeadlessVmCallableResult InvokeSysCall(
        string name,
        IReadOnlyList<HeadlessVmRuntimeValue> arguments,
        bool returnsValue,
        HeadlessVmObservationLog observation)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(observation);

        return name switch
        {
            "scenario.say" => InvokeScenarioSay(arguments, observation),
            "scenario.nar" => InvokeScenarioNarration(arguments, observation),
            _ when returnsValue => new HeadlessVmCallableResult(observation, ReturnValue: HeadlessVmRuntimeValue.Null(), HasReturnValue: true),
            _ => Continue(observation),
        };
    }

    public HeadlessVmCallableResult InvokeMethod(
        string methodName,
        string receiverReferenceId,
        IReadOnlyList<HeadlessVmRuntimeValue> arguments,
        bool returnsValue,
        HeadlessVmObjectStore objectStore,
        HeadlessVmObservationLog observation)
    {
        ArgumentException.ThrowIfNullOrEmpty(methodName);
        ArgumentException.ThrowIfNullOrEmpty(receiverReferenceId);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(objectStore);
        ArgumentNullException.ThrowIfNull(observation);

        if (!string.Equals(methodName, "dispose", StringComparison.Ordinal))
        {
            return Fault(observation, $"Method '{methodName}' is not supported in headless mode.");
        }

        if (!objectStore.TryDispose(receiverReferenceId, out var error))
        {
            return Fault(observation, error ?? $"Method '{methodName}' failed.");
        }

        if (returnsValue)
        {
            return new HeadlessVmCallableResult(observation, ReturnValue: HeadlessVmRuntimeValue.Null(), HasReturnValue: true);
        }

        return Continue(observation);
    }

    public HeadlessVmCallableResult InvokeActorPropertyGet(
        string actorReferenceId,
        string propertyName,
        HeadlessVmObjectStore objectStore,
        HeadlessVmObservationLog observation)
    {
        ArgumentException.ThrowIfNullOrEmpty(actorReferenceId);
        ArgumentException.ThrowIfNullOrEmpty(propertyName);
        ArgumentNullException.ThrowIfNull(objectStore);
        ArgumentNullException.ThrowIfNull(observation);

        if (!objectStore.TryGetActorField(actorReferenceId, propertyName, out var value, out var exists, out var error))
        {
            return Fault(observation, error ?? $"Actor property '{propertyName}' lookup failed.");
        }

        if (!exists)
        {
            return Fault(observation, $"Dynamic property '{propertyName}' could not be resolved for actor instance '{actorReferenceId}'.");
        }

        return new HeadlessVmCallableResult(observation, ReturnValue: value, HasReturnValue: true);
    }

    private static HeadlessVmCallableResult Continue(HeadlessVmObservationLog observation)
    {
        return new HeadlessVmCallableResult(observation);
    }

    private static HeadlessVmCallableResult Fault(HeadlessVmObservationLog observation, string message)
    {
        return new HeadlessVmCallableResult(
            observation,
            HeadlessVmCallableOutcomeKind.Fault,
            FaultMessage: message);
    }

    private static HeadlessVmCallableResult ConvertNumberToString(IReadOnlyList<HeadlessVmRuntimeValue> arguments, HeadlessVmObservationLog observation)
    {
        var numberValue = arguments.Count == 1 ? arguments[0].NumberValue : null;
        if (arguments.Count != 1 || arguments[0].Kind != HeadlessVmRuntimeValueKind.Number || numberValue is null)
        {
            return Fault(observation, "Callable 'number_to_string' requires one number argument.");
        }

        return new HeadlessVmCallableResult(
            observation,
            ReturnValue: new HeadlessVmRuntimeValue(
                HeadlessVmRuntimeValueKind.String,
                StringValue: numberValue.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            HasReturnValue: true);
    }

    private static HeadlessVmCallableResult ConvertBoolToString(IReadOnlyList<HeadlessVmRuntimeValue> arguments, HeadlessVmObservationLog observation)
    {
        var boolValue = arguments.Count == 1 ? arguments[0].BoolValue : null;
        if (arguments.Count != 1 || arguments[0].Kind != HeadlessVmRuntimeValueKind.Bool || boolValue is null)
        {
            return Fault(observation, "Callable 'bool_to_string' requires one bool argument.");
        }

        return new HeadlessVmCallableResult(
            observation,
            ReturnValue: new HeadlessVmRuntimeValue(
                HeadlessVmRuntimeValueKind.String,
                StringValue: boolValue.Value ? "true" : "false"),
            HasReturnValue: true);
    }

    private static HeadlessVmCallableResult GetArrayLength(
        IReadOnlyList<HeadlessVmRuntimeValue> arguments,
        HeadlessVmObjectStore objectStore,
        HeadlessVmObservationLog observation)
    {
        if (arguments.Count != 1 || arguments[0].Kind != HeadlessVmRuntimeValueKind.Reference || string.IsNullOrEmpty(arguments[0].ReferenceId))
        {
            return Fault(observation, "Callable 'array_len' requires one array reference argument.");
        }

        var referenceId = arguments[0].ReferenceId!;
        if (!objectStore.TryGetArrayLength(referenceId, out var length, out var error))
        {
            return Fault(observation, error ?? "Callable 'array_len' failed.");
        }

        return new HeadlessVmCallableResult(
            observation,
            ReturnValue: new HeadlessVmRuntimeValue(HeadlessVmRuntimeValueKind.Number, NumberValue: length),
            HasReturnValue: true);
    }

    private static HeadlessVmCallableResult GetStringLength(IReadOnlyList<HeadlessVmRuntimeValue> arguments, HeadlessVmObservationLog observation)
    {
        if (arguments.Count != 1 || arguments[0].Kind != HeadlessVmRuntimeValueKind.String)
        {
            return Fault(observation, "Callable 'str_len' requires one string argument.");
        }

        return new HeadlessVmCallableResult(
            observation,
            ReturnValue: new HeadlessVmRuntimeValue(HeadlessVmRuntimeValueKind.Number, NumberValue: arguments[0].StringValue?.Length ?? 0),
            HasReturnValue: true);
    }

    private static HeadlessVmCallableResult CreateRange(
        IReadOnlyList<HeadlessVmRuntimeValue> arguments,
        HeadlessVmObjectStore objectStore,
        HeadlessVmObservationLog observation)
    {
        var startValue = arguments.Count >= 1 ? arguments[0].NumberValue : null;
        var endValue = arguments.Count >= 2 ? arguments[1].NumberValue : null;
        if (arguments.Count != 2 ||
            arguments[0].Kind != HeadlessVmRuntimeValueKind.Number ||
            startValue is null ||
            arguments[1].Kind != HeadlessVmRuntimeValueKind.Number ||
            endValue is null)
        {
            return Fault(observation, "Callable 'range' requires start and end number arguments.");
        }

        var start = (int)startValue.Value;
        var end = (int)endValue.Value;
        var values = new List<HeadlessVmRuntimeValue>();
        for (var value = start; value < end; value++)
        {
            values.Add(new HeadlessVmRuntimeValue(HeadlessVmRuntimeValueKind.Number, NumberValue: value));
        }

        return new HeadlessVmCallableResult(
            observation,
            ReturnValue: objectStore.CreateArray(values),
            HasReturnValue: true);
    }

    private static HeadlessVmCallableResult InvokeAssert(IReadOnlyList<HeadlessVmRuntimeValue> arguments, HeadlessVmObservationLog observation)
    {
        var condition = arguments.Count > 0 ? arguments[0].BoolValue : null;
        if (arguments.Count == 0 || arguments[0].Kind != HeadlessVmRuntimeValueKind.Bool || condition is null)
        {
            return Fault(observation, "Callable 'assert' requires a bool condition.");
        }

        if (condition.Value)
        {
            return Continue(observation);
        }

        var message = arguments.Count > 1 && arguments[1].Kind == HeadlessVmRuntimeValueKind.String
            ? arguments[1].StringValue ?? "Assertion failed."
            : "Assertion failed.";
        return Fault(observation, message);
    }

    private static HeadlessVmCallableResult InvokeStandby(
        IReadOnlyList<HeadlessVmRuntimeValue> arguments,
        HeadlessVmObjectStore objectStore,
        HeadlessVmObservationLog observation)
    {
        if (!TryGetActorReference(arguments, "standby", observation, out var actorReferenceId, out var fault))
        {
            return fault!;
        }

        objectStore.EnsureActorReference(actorReferenceId!);
        return Continue(observation);
    }

    private static HeadlessVmCallableResult InvokeShow(
        IReadOnlyList<HeadlessVmRuntimeValue> arguments,
        HeadlessVmObjectStore objectStore,
        HeadlessVmObservationLog observation)
    {
        if (!TryGetActorReference(arguments, "show", observation, out var actorReferenceId, out var fault))
        {
            return fault!;
        }

        objectStore.EnsureActorReference(actorReferenceId!);
        objectStore.TrySetField(actorReferenceId!, "isVisible", new HeadlessVmRuntimeValue(HeadlessVmRuntimeValueKind.Bool, BoolValue: true), out _);
        if (arguments.Count >= 2 && arguments[1].Kind == HeadlessVmRuntimeValueKind.Number)
        {
            objectStore.TrySetField(actorReferenceId!, "position", arguments[1], out _);
        }

        if (arguments.Count >= 3 && arguments[2].Kind == HeadlessVmRuntimeValueKind.String)
        {
            objectStore.TrySetField(actorReferenceId!, "face", arguments[2], out _);
        }

        return Continue(observation);
    }

    private static HeadlessVmCallableResult InvokeHide(
        IReadOnlyList<HeadlessVmRuntimeValue> arguments,
        HeadlessVmObjectStore objectStore,
        HeadlessVmObservationLog observation)
    {
        if (!TryGetActorReference(arguments, "hide", observation, out var actorReferenceId, out var fault))
        {
            return fault!;
        }

        objectStore.EnsureActorReference(actorReferenceId!);
        objectStore.TrySetField(actorReferenceId!, "isVisible", new HeadlessVmRuntimeValue(HeadlessVmRuntimeValueKind.Bool, BoolValue: false), out _);
        return Continue(observation);
    }

    private static HeadlessVmCallableResult InvokeFace(
        IReadOnlyList<HeadlessVmRuntimeValue> arguments,
        HeadlessVmObjectStore objectStore,
        HeadlessVmObservationLog observation)
    {
        if (!TryGetActorReference(arguments, "face", observation, out var actorReferenceId, out var fault))
        {
            return fault!;
        }

        if (arguments.Count < 2 || arguments[1].Kind != HeadlessVmRuntimeValueKind.String)
        {
            return Fault(observation, "Callable 'face' requires actor and expression arguments.");
        }

        objectStore.EnsureActorReference(actorReferenceId!);
        objectStore.TrySetField(actorReferenceId!, "face", arguments[1], out _);
        return Continue(observation);
    }

    private static HeadlessVmCallableResult InvokeMove(
        IReadOnlyList<HeadlessVmRuntimeValue> arguments,
        HeadlessVmObjectStore objectStore,
        HeadlessVmObservationLog observation)
    {
        if (!TryGetActorReference(arguments, "move", observation, out var actorReferenceId, out var fault))
        {
            return fault!;
        }

        if (arguments.Count < 2 || arguments[1].Kind != HeadlessVmRuntimeValueKind.Number)
        {
            return Fault(observation, "Callable 'move' requires actor and position arguments.");
        }

        objectStore.EnsureActorReference(actorReferenceId!);
        objectStore.TrySetField(actorReferenceId!, "position", arguments[1], out _);
        return Continue(observation);
    }

    private static HeadlessVmCallableResult InvokeScenarioSay(IReadOnlyList<HeadlessVmRuntimeValue> arguments, HeadlessVmObservationLog observation)
    {
        if (arguments.Count != 2)
        {
            return Fault(observation, "Syscall 'scenario.say' requires speaker and text arguments.");
        }

        return new HeadlessVmCallableResult(
            observation.AppendSay(ToDisplayString(arguments[0]), ToDisplayString(arguments[1]) ?? string.Empty),
            HeadlessVmCallableOutcomeKind.WaitForAdvance);
    }

    private static HeadlessVmCallableResult InvokeScenarioNarration(IReadOnlyList<HeadlessVmRuntimeValue> arguments, HeadlessVmObservationLog observation)
    {
        if (arguments.Count != 1)
        {
            return Fault(observation, "Syscall 'scenario.nar' requires one text argument.");
        }

        return new HeadlessVmCallableResult(
            observation.AppendNarration(ToDisplayString(arguments[0]) ?? string.Empty),
            HeadlessVmCallableOutcomeKind.WaitForAdvance);
    }

    private static string? ToDisplayString(HeadlessVmRuntimeValue value)
    {
        return value.Kind switch
        {
            HeadlessVmRuntimeValueKind.Null => null,
            HeadlessVmRuntimeValueKind.String => value.StringValue,
            HeadlessVmRuntimeValueKind.Bool => value.BoolValue is true ? "true" : "false",
            HeadlessVmRuntimeValueKind.Number => value.NumberValue?.ToString(System.Globalization.CultureInfo.InvariantCulture),
            HeadlessVmRuntimeValueKind.Reference => value.ReferenceId,
            _ => value.ToString(),
        };
    }

    private static bool TryGetActorReference(
        IReadOnlyList<HeadlessVmRuntimeValue> arguments,
        string callableName,
        HeadlessVmObservationLog observation,
        out string? actorReferenceId,
        out HeadlessVmCallableResult? fault)
    {
        if (arguments.Count == 0 ||
            arguments[0].Kind != HeadlessVmRuntimeValueKind.Reference ||
            string.IsNullOrEmpty(arguments[0].ReferenceId))
        {
            actorReferenceId = null;
            fault = Fault(observation, $"Callable '{callableName}' requires an actor reference as the first argument.");
            return false;
        }

        actorReferenceId = arguments[0].ReferenceId;
        fault = null;
        return true;
    }
}
