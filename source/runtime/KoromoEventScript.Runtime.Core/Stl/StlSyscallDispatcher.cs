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
    RuntimeFailureKind FailureKind,
    bool WaitForAdvance = false)
{
    public static RuntimeSyscallResult Success(RuntimeValue? returnValue = null, bool waitForAdvance = false)
    {
        return new RuntimeSyscallResult(true, returnValue, [], RuntimeFailureKind.None, waitForAdvance);
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
            "scene.rt_back" => SceneNoArgs(invocation),
            "scene.rt_front" => SceneNoArgs(invocation),
            "scene.bg" => SceneString(invocation, "id"),
            "scene.camera_autofocus" => SceneBool(invocation, "enabled"),
            "scene.trans" => SceneTransition(invocation),
            "actor.cast" => ActorSingle(invocation),
            "actor.hide" => ActorSingle(invocation),
            "actor.action_jump" => ActorSingle(invocation),
            "actor.face" => ActorFace(invocation),
            "actor.move" => ActorMove(invocation),
            "actor.show" => ActorShow(invocation),
            "scenario.say" => ScenarioSay(invocation),
            "scenario.nar" => ScenarioNar(invocation),
            "text.p" => TextWait(invocation),
            "text.l" => TextWait(invocation),
            "text.wait_click" => TextWait(invocation),
            "text.r" => TextNoArgs(invocation),
            "text.cm" => TextNoArgs(invocation),
            "text.vo" => TextVoice(invocation),
            "audio.vo_auto" => AutoVoice(invocation),
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

    private RuntimeSyscallResult SceneNoArgs(RuntimeSyscallInvocation invocation)
    {
        if (invocation.Arguments.Count != 0)
        {
            return ArgumentFailure(invocation, $"Syscall '{invocation.Id}' does not take arguments.");
        }

        return PublishSceneEffect(invocation, new Dictionary<string, string?>());
    }

    private RuntimeSyscallResult SceneString(RuntimeSyscallInvocation invocation, string key)
    {
        if (!TryReadString(invocation, 0, out var value) || invocation.Arguments.Count != 1)
        {
            return ArgumentFailure(invocation, $"Syscall '{invocation.Id}' requires one string argument.");
        }

        return PublishSceneEffect(invocation, new Dictionary<string, string?> { [key] = value });
    }

    private RuntimeSyscallResult SceneBool(RuntimeSyscallInvocation invocation, string key)
    {
        if (!TryReadBool(invocation, 0, out var value) || invocation.Arguments.Count != 1)
        {
            return ArgumentFailure(invocation, $"Syscall '{invocation.Id}' requires one bool argument.");
        }

        return PublishSceneEffect(invocation, new Dictionary<string, string?> { [key] = FormatBool(value) });
    }

    private RuntimeSyscallResult SceneTransition(RuntimeSyscallInvocation invocation)
    {
        if (!TryReadString(invocation, 0, out var effect) ||
            !TryReadNumber(invocation, 1, out var duration) ||
            invocation.Arguments.Count != 2)
        {
            return ArgumentFailure(invocation, "Syscall 'scene.trans' requires effect:string and duration:number arguments.");
        }

        if (duration < 0)
        {
            return RuntimeSyscallResult.Failure(
                RuntimeFailureKind.Runtime,
                Error("KESR3402", invocation, "Syscall 'scene.trans' duration must not be negative."));
        }

        return PublishSceneEffect(
            invocation,
            new Dictionary<string, string?>
            {
                ["effect"] = effect,
                ["duration"] = FormatNumber(duration),
            });
    }

    private RuntimeSyscallResult ActorSingle(RuntimeSyscallInvocation invocation)
    {
        if (!TryReadActor(invocation, 0, out var actor) || invocation.Arguments.Count != 1)
        {
            return ArgumentFailure(invocation, $"Syscall '{invocation.Id}' requires one actor argument.");
        }

        return PublishSceneEffect(invocation, new Dictionary<string, string?> { ["actor"] = actor });
    }

    private RuntimeSyscallResult ActorFace(RuntimeSyscallInvocation invocation)
    {
        if (!TryReadActor(invocation, 0, out var actor) ||
            !TryReadString(invocation, 1, out var expression) ||
            invocation.Arguments.Count != 2)
        {
            return ArgumentFailure(invocation, "Syscall 'actor.face' requires actor and exp:string arguments.");
        }

        return PublishSceneEffect(
            invocation,
            new Dictionary<string, string?>
            {
                ["actor"] = actor,
                ["exp"] = expression,
            });
    }

    private RuntimeSyscallResult ActorMove(RuntimeSyscallInvocation invocation)
    {
        if (!TryReadActor(invocation, 0, out var actor) ||
            !TryReadNumber(invocation, 1, out var position) ||
            !TryReadNumber(invocation, 2, out var duration) ||
            invocation.Arguments.Count != 3)
        {
            return ArgumentFailure(invocation, "Syscall 'actor.move' requires actor, pos:number, and duration:number arguments.");
        }

        if (duration < 0)
        {
            return RuntimeSyscallResult.Failure(
                RuntimeFailureKind.Runtime,
                Error("KESR3402", invocation, "Syscall 'actor.move' duration must not be negative."));
        }

        return PublishSceneEffect(
            invocation,
            new Dictionary<string, string?>
            {
                ["actor"] = actor,
                ["pos"] = FormatNumber(position),
                ["duration"] = FormatNumber(duration),
            });
    }

    private RuntimeSyscallResult ActorShow(RuntimeSyscallInvocation invocation)
    {
        if (!TryReadActor(invocation, 0, out var actor) ||
            !TryReadNumber(invocation, 1, out var position) ||
            !TryReadString(invocation, 2, out var face) ||
            !TryReadNumber(invocation, 3, out var layer) ||
            !TryReadNumber(invocation, 4, out var z) ||
            !TryReadBool(invocation, 5, out var bustup) ||
            invocation.Arguments.Count != 6)
        {
            return ArgumentFailure(invocation, "Syscall 'actor.show' requires actor, pos, face, layer, z, and bustup arguments.");
        }

        return PublishSceneEffect(
            invocation,
            new Dictionary<string, string?>
            {
                ["actor"] = actor,
                ["pos"] = FormatNumber(position),
                ["face"] = face,
                ["layer"] = FormatNumber(layer),
                ["z"] = FormatNumber(z),
                ["bustup"] = FormatBool(bustup),
            });
    }

    private RuntimeSyscallResult ScenarioSay(RuntimeSyscallInvocation invocation)
    {
        if (!TryReadActor(invocation, 0, out var actor) ||
            !TryReadString(invocation, 1, out var text) ||
            invocation.Arguments.Count != 2)
        {
            return ArgumentFailure(invocation, "Syscall 'scenario.say' requires actor and text:string arguments.");
        }

        return PublishUiEffect(
            invocation,
            new Dictionary<string, string?>
            {
                ["actor"] = actor,
                ["text"] = text,
            });
    }

    private RuntimeSyscallResult ScenarioNar(RuntimeSyscallInvocation invocation)
    {
        if (!TryReadString(invocation, 0, out var text) || invocation.Arguments.Count != 1)
        {
            return ArgumentFailure(invocation, "Syscall 'scenario.nar' requires one text:string argument.");
        }

        return PublishUiEffect(invocation, new Dictionary<string, string?> { ["text"] = text });
    }

    private RuntimeSyscallResult TextNoArgs(RuntimeSyscallInvocation invocation)
    {
        if (invocation.Arguments.Count != 0)
        {
            return ArgumentFailure(invocation, $"Syscall '{invocation.Id}' does not take arguments.");
        }

        return PublishUiEffect(invocation, new Dictionary<string, string?>());
    }

    private RuntimeSyscallResult TextWait(RuntimeSyscallInvocation invocation)
    {
        if (invocation.Arguments.Count != 0)
        {
            return ArgumentFailure(invocation, $"Syscall '{invocation.Id}' does not take arguments.");
        }

        PublishEffects(
            new RuntimeEffect(RuntimeEffectKind.Ui, invocation.Id, new Dictionary<string, string?>()),
            RuntimeEffect.Wait(RuntimeWaitKind.Click));
        return RuntimeSyscallResult.Success(waitForAdvance: true);
    }

    private RuntimeSyscallResult TextVoice(RuntimeSyscallInvocation invocation)
    {
        if (!TryReadString(invocation, 0, out var id) || invocation.Arguments.Count != 1)
        {
            return ArgumentFailure(invocation, "Syscall 'text.vo' requires one voice id string argument.");
        }

        return PublishAudioEffect(invocation, new Dictionary<string, string?> { ["id"] = id });
    }

    private RuntimeSyscallResult AutoVoice(RuntimeSyscallInvocation invocation)
    {
        if (invocation.Arguments.Count != 0)
        {
            return ArgumentFailure(invocation, "Syscall 'audio.vo_auto' does not take arguments.");
        }

        return PublishAudioEffect(invocation, new Dictionary<string, string?> { ["auto"] = "true" });
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

    private RuntimeSyscallResult PublishSceneEffect(RuntimeSyscallInvocation invocation, IReadOnlyDictionary<string, string?> payload)
    {
        PublishEffects(new RuntimeEffect(RuntimeEffectKind.Scene, invocation.Id, payload));
        return RuntimeSyscallResult.Success();
    }

    private RuntimeSyscallResult PublishUiEffect(RuntimeSyscallInvocation invocation, IReadOnlyDictionary<string, string?> payload)
    {
        PublishEffects(new RuntimeEffect(RuntimeEffectKind.Ui, invocation.Id, payload));
        return RuntimeSyscallResult.Success();
    }

    private RuntimeSyscallResult PublishAudioEffect(RuntimeSyscallInvocation invocation, IReadOnlyDictionary<string, string?> payload)
    {
        PublishEffects(new RuntimeEffect(RuntimeEffectKind.Audio, invocation.Id, payload));
        return RuntimeSyscallResult.Success();
    }

    private void PublishEffects(params RuntimeEffect[] effects)
    {
        effectSink?.Publish(new RuntimeEffectBatch(effects, []));
    }

    private static RuntimeSyscallResult ArgumentFailure(RuntimeSyscallInvocation invocation, string message)
    {
        return RuntimeSyscallResult.Failure(RuntimeFailureKind.Runtime, Error("KESR3402", invocation, message));
    }

    private static bool TryReadString(RuntimeSyscallInvocation invocation, int index, out string value)
    {
        value = string.Empty;
        if (index < 0 ||
            index >= invocation.Arguments.Count ||
            invocation.Arguments[index].Kind != RuntimeValueKind.String)
        {
            return false;
        }

        value = invocation.Arguments[index].StringValue ?? string.Empty;
        return true;
    }

    private static bool TryReadNumber(RuntimeSyscallInvocation invocation, int index, out double value)
    {
        value = 0;
        if (index < 0 ||
            index >= invocation.Arguments.Count ||
            invocation.Arguments[index].Kind != RuntimeValueKind.Number ||
            invocation.Arguments[index].NumberValue is null)
        {
            return false;
        }

        value = invocation.Arguments[index].NumberValue.GetValueOrDefault();
        return true;
    }

    private static bool TryReadBool(RuntimeSyscallInvocation invocation, int index, out bool value)
    {
        value = false;
        if (index < 0 ||
            index >= invocation.Arguments.Count ||
            invocation.Arguments[index].Kind != RuntimeValueKind.Bool ||
            invocation.Arguments[index].BoolValue is null)
        {
            return false;
        }

        value = invocation.Arguments[index].BoolValue.GetValueOrDefault();
        return true;
    }

    private static bool TryReadActor(RuntimeSyscallInvocation invocation, int index, out string value)
    {
        value = string.Empty;
        if (index < 0 ||
            index >= invocation.Arguments.Count ||
            invocation.Arguments[index].Kind != RuntimeValueKind.Reference ||
            string.IsNullOrEmpty(invocation.Arguments[index].ReferenceId))
        {
            return false;
        }

        value = invocation.Arguments[index].ReferenceId!;
        return true;
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("G", CultureInfo.InvariantCulture);
    }

    private static string FormatBool(bool value)
    {
        return value ? "true" : "false";
    }
}
