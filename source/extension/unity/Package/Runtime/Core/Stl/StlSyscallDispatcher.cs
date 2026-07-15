#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using KoromoEventScript.Runtime.Core.Diagnostics;
using KoromoEventScript.Runtime.Core.Effects;
using KoromoEventScript.Runtime.Core.Execution;

namespace KoromoEventScript.Runtime.Core.Stl
{

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
        return new RuntimeSyscallResult(true, returnValue, Array.Empty<RuntimeDiagnostic>(), RuntimeFailureKind.None, waitForAdvance);
    }

    public static RuntimeSyscallResult Failure(RuntimeFailureKind failureKind, params RuntimeDiagnostic[] diagnostics)
    {
        return new RuntimeSyscallResult(false, null, diagnostics, failureKind);
    }
}

public sealed class StlSyscallDispatcher : IRuntimeSyscallDispatcher
{
    public static ISet<string> SupportedSyscallIds { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "core.print",
        "core.array_len",
        "core.str_len",
        "core.range",
        "core.number_to_string",
        "core.bool_to_string",
        "core.assert",
        "scene.rt_back",
        "scene.rt_front",
        "scene.bg",
        "scene.camera_autofocus",
        "scene.trans",
        "actor.cast",
        "actor.hide",
        "actor.action_jump",
        "actor.face",
        "actor.move",
        "actor.show",
        "scenario.say",
        "scenario.nar",
        "text.p",
        "text.l",
        "text.wait_click",
        "text.r",
        "text.cm",
        "text.vo",
        "audio.vo_auto",
        "audio.bgm",
        "audio.bgm_stop",
        "audio.se",
        "audio.se_stop",
        "audio.se_stop_all",
        "audio.voice_stop",
        "state.mark_read",
        "state.is_read",
        "state.save",
        "state.autosave",
        "state.load",
        "localize.get",
        "system.wait",
        "system.set_auto",
        "system.set_skip",
        "system.set_config_string",
        "system.set_config_number",
        "system.set_config_bool",
        "system.get_config",
        "system.set_param_string",
        "system.set_param_number",
        "system.set_param_bool",
        "system.get_param",
    };

    private readonly IRuntimeEffectSink? effectSink;
    private readonly IRuntimeGameParameterStore gameParameters;
    private readonly HashSet<string> readTags = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> config = new(StringComparer.Ordinal)
    {
        ["masterVolume"] = "1",
        ["bgmVolume"] = "1",
        ["seVolume"] = "1",
        ["voiceVolume"] = "1",
        ["textSpeed"] = "1",
        ["autoSpeed"] = "1",
        ["skipMode"] = "off",
        ["fullscreen"] = "false",
        ["locale"] = "ja-JP",
    };

    public StlSyscallDispatcher(IRuntimeEffectSink? effectSink = null, IRuntimeGameParameterStore? gameParameters = null)
    {
        this.effectSink = effectSink;
        this.gameParameters = gameParameters ?? new RuntimeGameParameterStore();
    }

    public RuntimeSyscallResult Invoke(RuntimeSyscallInvocation invocation, KesVmSession session)
    {
        if (invocation == null)
        {
            throw new ArgumentNullException(nameof(invocation));
        }

        if (session == null)
        {
            throw new ArgumentNullException(nameof(session));
        }

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
            "audio.bgm" => AudioBgm(invocation),
            "audio.bgm_stop" => AudioBgmStop(invocation),
            "audio.se" => AudioSe(invocation),
            "audio.se_stop" => AudioSeStop(invocation),
            "audio.se_stop_all" => AudioNoArgs(invocation),
            "audio.voice_stop" => AudioNoArgs(invocation),
            "state.mark_read" => StateMarkRead(invocation),
            "state.is_read" => StateIsRead(invocation),
            "state.save" => StateSave(invocation),
            "state.autosave" => SaveNoArgs(invocation),
            "state.load" => StateLoad(invocation),
            "localize.get" => LocalizeGet(invocation),
            "system.wait" => SystemWait(invocation),
            "system.set_auto" => SystemBool(invocation, "enabled"),
            "system.set_skip" => SystemSetSkip(invocation),
            "system.set_config_string" => SystemSetConfigString(invocation),
            "system.set_config_number" => SystemSetConfigNumber(invocation),
            "system.set_config_bool" => SystemSetConfigBool(invocation),
            "system.get_config" => SystemGetConfig(invocation),
            "system.set_param_string" => SystemSetParamString(invocation),
            "system.set_param_number" => SystemSetParamNumber(invocation),
            "system.set_param_bool" => SystemSetParamBool(invocation),
            "system.get_param" => SystemGetParam(invocation),
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
        effectSink?.Publish(new RuntimeEffectBatch(
            new[] { RuntimeEffect.Diagnostic(diagnostic) },
            new[] { diagnostic }));
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
            },
            waitForAdvance: true);
    }

    private RuntimeSyscallResult ScenarioNar(RuntimeSyscallInvocation invocation)
    {
        if (!TryReadString(invocation, 0, out var text) || invocation.Arguments.Count != 1)
        {
            return ArgumentFailure(invocation, "Syscall 'scenario.nar' requires one text:string argument.");
        }

        return PublishUiEffect(invocation, new Dictionary<string, string?> { ["text"] = text }, waitForAdvance: true);
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

    private RuntimeSyscallResult AudioBgm(RuntimeSyscallInvocation invocation)
    {
        if (!TryReadString(invocation, 0, out var id) ||
            !TryReadBool(invocation, 1, out var loop) ||
            !TryReadNumber(invocation, 2, out var fade) ||
            invocation.Arguments.Count != 3)
        {
            return ArgumentFailure(invocation, "Syscall 'audio.bgm' requires id:string, loop:bool, and fade:number arguments.");
        }

        if (fade < 0)
        {
            return RuntimeSyscallResult.Failure(RuntimeFailureKind.Runtime, Error("KESR3402", invocation, "Syscall 'audio.bgm' fade must not be negative."));
        }

        return PublishAudioEffect(
            invocation,
            new Dictionary<string, string?>
            {
                ["id"] = id,
                ["loop"] = FormatBool(loop),
                ["fade"] = FormatNumber(fade),
            });
    }

    private RuntimeSyscallResult AudioBgmStop(RuntimeSyscallInvocation invocation)
    {
        if (!TryReadNumber(invocation, 0, out var fade) || invocation.Arguments.Count != 1)
        {
            return ArgumentFailure(invocation, "Syscall 'audio.bgm_stop' requires one fade:number argument.");
        }

        if (fade < 0)
        {
            return RuntimeSyscallResult.Failure(RuntimeFailureKind.Runtime, Error("KESR3402", invocation, "Syscall 'audio.bgm_stop' fade must not be negative."));
        }

        return PublishAudioEffect(invocation, new Dictionary<string, string?> { ["fade"] = FormatNumber(fade) });
    }

    private RuntimeSyscallResult AudioSe(RuntimeSyscallInvocation invocation)
    {
        if (!TryReadString(invocation, 0, out var id) || invocation.Arguments.Count != 1)
        {
            return ArgumentFailure(invocation, $"Syscall '{invocation.Id}' requires one id:string argument.");
        }

        return PublishAudioEffect(invocation, new Dictionary<string, string?> { ["id"] = id });
    }

    private RuntimeSyscallResult AudioSeStop(RuntimeSyscallInvocation invocation)
    {
        if (!TryReadString(invocation, 0, out var id) || invocation.Arguments.Count != 1)
        {
            return ArgumentFailure(invocation, "Syscall 'audio.se_stop' requires one id:string argument.");
        }

        return PublishAudioEffect(invocation, new Dictionary<string, string?> { ["id"] = id });
    }

    private RuntimeSyscallResult AudioNoArgs(RuntimeSyscallInvocation invocation)
    {
        if (invocation.Arguments.Count != 0)
        {
            return ArgumentFailure(invocation, $"Syscall '{invocation.Id}' does not take arguments.");
        }

        return PublishAudioEffect(invocation, new Dictionary<string, string?>());
    }

    private RuntimeSyscallResult StateMarkRead(RuntimeSyscallInvocation invocation)
    {
        if (!TryReadString(invocation, 0, out var tag) || invocation.Arguments.Count != 1)
        {
            return ArgumentFailure(invocation, "Syscall 'state.mark_read' requires one tag:string argument.");
        }

        readTags.Add(tag);
        return PublishSaveEffect(invocation, new Dictionary<string, string?> { ["tag"] = tag });
    }

    private RuntimeSyscallResult StateIsRead(RuntimeSyscallInvocation invocation)
    {
        if (!TryReadString(invocation, 0, out var tag) || invocation.Arguments.Count != 1)
        {
            return ArgumentFailure(invocation, "Syscall 'state.is_read' requires one tag:string argument.");
        }

        return RuntimeSyscallResult.Success(RuntimeValue.Bool(readTags.Contains(tag)));
    }

    private RuntimeSyscallResult StateSave(RuntimeSyscallInvocation invocation)
    {
        if (!TryReadSlot(invocation, 0, out var slot) ||
            !TryReadString(invocation, 1, out var title) ||
            invocation.Arguments.Count != 2)
        {
            return ArgumentFailure(invocation, "Syscall 'state.save' requires slot:number and title:string arguments.");
        }

        return PublishSaveEffect(invocation, new Dictionary<string, string?> { ["slot"] = slot.ToString(CultureInfo.InvariantCulture), ["title"] = title });
    }

    private RuntimeSyscallResult SaveNoArgs(RuntimeSyscallInvocation invocation)
    {
        if (invocation.Arguments.Count != 0)
        {
            return ArgumentFailure(invocation, $"Syscall '{invocation.Id}' does not take arguments.");
        }

        return PublishSaveEffect(invocation, new Dictionary<string, string?>());
    }

    private RuntimeSyscallResult StateLoad(RuntimeSyscallInvocation invocation)
    {
        if (!TryReadSlot(invocation, 0, out var slot) || invocation.Arguments.Count != 1)
        {
            return ArgumentFailure(invocation, "Syscall 'state.load' requires one slot:number argument.");
        }

        return PublishSaveEffect(invocation, new Dictionary<string, string?> { ["slot"] = slot.ToString(CultureInfo.InvariantCulture) });
    }

    private static RuntimeSyscallResult LocalizeGet(RuntimeSyscallInvocation invocation)
    {
        if (!TryReadString(invocation, 0, out var tag) || invocation.Arguments.Count != 1)
        {
            return ArgumentFailure(invocation, "Syscall 'localize.get' requires one tag:string argument.");
        }

        return RuntimeSyscallResult.Success(RuntimeValue.String(tag));
    }

    private RuntimeSyscallResult SystemWait(RuntimeSyscallInvocation invocation)
    {
        if (!TryReadNumber(invocation, 0, out var seconds) || invocation.Arguments.Count != 1)
        {
            return ArgumentFailure(invocation, "Syscall 'system.wait' requires one seconds:number argument.");
        }

        if (seconds < 0)
        {
            return RuntimeSyscallResult.Failure(RuntimeFailureKind.Runtime, Error("KESR3402", invocation, "Syscall 'system.wait' seconds must not be negative."));
        }

        PublishEffects(new RuntimeEffect(
            RuntimeEffectKind.Wait,
            invocation.Id,
            new Dictionary<string, string?> { ["kind"] = RuntimeWaitKind.Timed.ToString(), ["seconds"] = FormatNumber(seconds) }));
        return RuntimeSyscallResult.Success();
    }

    private RuntimeSyscallResult SystemBool(RuntimeSyscallInvocation invocation, string key)
    {
        if (!TryReadBool(invocation, 0, out var enabled) || invocation.Arguments.Count != 1)
        {
            return ArgumentFailure(invocation, $"Syscall '{invocation.Id}' requires one bool argument.");
        }

        return PublishSettingsEffect(invocation, new Dictionary<string, string?> { [key] = FormatBool(enabled) });
    }

    private RuntimeSyscallResult SystemSetSkip(RuntimeSyscallInvocation invocation)
    {
        if (!TryReadString(invocation, 0, out var mode) || invocation.Arguments.Count != 1)
        {
            return ArgumentFailure(invocation, "Syscall 'system.set_skip' requires one mode:string argument.");
        }

        if (mode is not ("off" or "read" or "all"))
        {
            return RuntimeSyscallResult.Failure(RuntimeFailureKind.Runtime, Error("KESR3405", invocation, $"Skip mode '{mode}' is not supported."));
        }

        config["skipMode"] = mode;
        return PublishSettingsEffect(invocation, new Dictionary<string, string?> { ["mode"] = mode });
    }

    private RuntimeSyscallResult SystemSetConfigString(RuntimeSyscallInvocation invocation)
    {
        if (!TryReadString(invocation, 0, out var key) ||
            !TryReadString(invocation, 1, out var value) ||
            invocation.Arguments.Count != 2)
        {
            return ArgumentFailure(invocation, "Syscall 'system.set_config_string' requires key:string and value:string arguments.");
        }

        if (key is not ("skipMode" or "locale"))
        {
            return UnknownConfig(invocation, key);
        }

        if (key == "skipMode" && value is not ("off" or "read" or "all"))
        {
            return RuntimeSyscallResult.Failure(RuntimeFailureKind.Runtime, Error("KESR3405", invocation, $"Skip mode '{value}' is not supported."));
        }

        return SetConfig(invocation, key, value);
    }

    private RuntimeSyscallResult SystemSetConfigNumber(RuntimeSyscallInvocation invocation)
    {
        if (!TryReadString(invocation, 0, out var key) ||
            !TryReadNumber(invocation, 1, out var value) ||
            invocation.Arguments.Count != 2)
        {
            return ArgumentFailure(invocation, "Syscall 'system.set_config_number' requires key:string and value:number arguments.");
        }

        if (key is not ("masterVolume" or "bgmVolume" or "seVolume" or "voiceVolume" or "textSpeed" or "autoSpeed"))
        {
            return UnknownConfig(invocation, key);
        }

        return SetConfig(invocation, key, FormatNumber(value));
    }

    private RuntimeSyscallResult SystemSetConfigBool(RuntimeSyscallInvocation invocation)
    {
        if (!TryReadString(invocation, 0, out var key) ||
            !TryReadBool(invocation, 1, out var value) ||
            invocation.Arguments.Count != 2)
        {
            return ArgumentFailure(invocation, "Syscall 'system.set_config_bool' requires key:string and value:bool arguments.");
        }

        if (key != "fullscreen")
        {
            return UnknownConfig(invocation, key);
        }

        return SetConfig(invocation, key, FormatBool(value));
    }

    private RuntimeSyscallResult SystemGetConfig(RuntimeSyscallInvocation invocation)
    {
        if (!TryReadString(invocation, 0, out var key) || invocation.Arguments.Count != 1)
        {
            return ArgumentFailure(invocation, "Syscall 'system.get_config' requires one key:string argument.");
        }

        return config.TryGetValue(key, out var value)
            ? RuntimeSyscallResult.Success(RuntimeValue.String(value))
            : UnknownConfig(invocation, key);
    }

    private RuntimeSyscallResult SystemSetParamString(RuntimeSyscallInvocation invocation)
    {
        if (!TryReadString(invocation, 0, out var key) ||
            !TryReadString(invocation, 1, out var value) ||
            invocation.Arguments.Count != 2)
        {
            return ArgumentFailure(invocation, "Syscall 'system.set_param_string' requires key:string and value:string arguments.");
        }

        gameParameters.Set(key, RuntimeValue.String(value));
        return RuntimeSyscallResult.Success();
    }

    private RuntimeSyscallResult SystemSetParamNumber(RuntimeSyscallInvocation invocation)
    {
        if (!TryReadString(invocation, 0, out var key) ||
            !TryReadNumber(invocation, 1, out var value) ||
            invocation.Arguments.Count != 2)
        {
            return ArgumentFailure(invocation, "Syscall 'system.set_param_number' requires key:string and value:number arguments.");
        }

        gameParameters.Set(key, RuntimeValue.Number(value));
        return RuntimeSyscallResult.Success();
    }

    private RuntimeSyscallResult SystemSetParamBool(RuntimeSyscallInvocation invocation)
    {
        if (!TryReadString(invocation, 0, out var key) ||
            !TryReadBool(invocation, 1, out var value) ||
            invocation.Arguments.Count != 2)
        {
            return ArgumentFailure(invocation, "Syscall 'system.set_param_bool' requires key:string and value:bool arguments.");
        }

        gameParameters.Set(key, RuntimeValue.Bool(value));
        return RuntimeSyscallResult.Success();
    }

    private RuntimeSyscallResult SystemGetParam(RuntimeSyscallInvocation invocation)
    {
        if (!TryReadString(invocation, 0, out var key) || invocation.Arguments.Count != 1)
        {
            return ArgumentFailure(invocation, "Syscall 'system.get_param' requires one key:string argument.");
        }

        if (!gameParameters.TryGet(key, out var value))
        {
            return RuntimeSyscallResult.Failure(RuntimeFailureKind.Runtime, Error("KESR3406", invocation, $"Game parameter '{key}' is not defined."));
        }

        return RuntimeSyscallResult.Success(RuntimeValue.String(value.Kind switch
        {
            RuntimeValueKind.Bool => value.BoolValue == true ? "true" : "false",
            RuntimeValueKind.Number => FormatNumber(value.NumberValue.GetValueOrDefault()),
            RuntimeValueKind.String => value.StringValue ?? string.Empty,
            RuntimeValueKind.Null => string.Empty,
            RuntimeValueKind.Reference => value.ReferenceId ?? string.Empty,
            _ => string.Empty,
        }));
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

    private RuntimeSyscallResult PublishUiEffect(
        RuntimeSyscallInvocation invocation,
        IReadOnlyDictionary<string, string?> payload,
        bool waitForAdvance = false)
    {
        PublishEffects(new RuntimeEffect(RuntimeEffectKind.Ui, invocation.Id, payload));
        return RuntimeSyscallResult.Success(waitForAdvance: waitForAdvance);
    }

    private RuntimeSyscallResult PublishAudioEffect(RuntimeSyscallInvocation invocation, IReadOnlyDictionary<string, string?> payload)
    {
        PublishEffects(new RuntimeEffect(RuntimeEffectKind.Audio, invocation.Id, payload));
        return RuntimeSyscallResult.Success();
    }

    private RuntimeSyscallResult PublishSaveEffect(RuntimeSyscallInvocation invocation, IReadOnlyDictionary<string, string?> payload)
    {
        PublishEffects(new RuntimeEffect(RuntimeEffectKind.Save, invocation.Id, payload));
        return RuntimeSyscallResult.Success();
    }

    private RuntimeSyscallResult PublishSettingsEffect(RuntimeSyscallInvocation invocation, IReadOnlyDictionary<string, string?> payload)
    {
        PublishEffects(new RuntimeEffect(RuntimeEffectKind.Settings, invocation.Id, payload));
        return RuntimeSyscallResult.Success();
    }

    private void PublishEffects(params RuntimeEffect[] effects)
    {
        effectSink?.Publish(new RuntimeEffectBatch(effects, Array.Empty<RuntimeDiagnostic>()));
    }

    private static RuntimeSyscallResult ArgumentFailure(RuntimeSyscallInvocation invocation, string message)
    {
        return RuntimeSyscallResult.Failure(RuntimeFailureKind.Runtime, Error("KESR3402", invocation, message));
    }

    private RuntimeSyscallResult SetConfig(RuntimeSyscallInvocation invocation, string key, string value)
    {
        config[key] = value;
        return PublishSettingsEffect(invocation, new Dictionary<string, string?> { ["key"] = key, ["value"] = value });
    }

    private static RuntimeSyscallResult UnknownConfig(RuntimeSyscallInvocation invocation, string key)
    {
        return RuntimeSyscallResult.Failure(RuntimeFailureKind.Runtime, Error("KESR3405", invocation, $"Configuration key '{key}' is not supported."));
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

    private static bool TryReadSlot(RuntimeSyscallInvocation invocation, int index, out int value)
    {
        value = 0;
        if (!TryReadNumber(invocation, index, out var number))
        {
            return false;
        }

        var integer = (int)number;
        if (integer < 0 || Math.Abs(number - integer) > double.Epsilon)
        {
            return false;
        }

        value = integer;
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
}
