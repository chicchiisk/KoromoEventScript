namespace KoromoEventScript.Cli.Semantics;

public sealed record CallableParameter(
    string Name,
    KesType Type,
    bool IsOptional = false);

public sealed record CallableSignature(
    string Name,
    IReadOnlyList<CallableParameter> Parameters,
    KesType ReturnType,
    bool AcceptsAnyArray = false);

public sealed class BuiltInSignatureRegistry
{
    private readonly Dictionary<string, CallableSignature> signatures;

    public BuiltInSignatureRegistry()
    {
        signatures = new Dictionary<string, CallableSignature>(StringComparer.Ordinal)
        {
            ["print"] = Void("print", Param("text", KesType.String)),
            ["array_len"] = new("array_len", [Param("values", KesType.Array(KesType.Unknown))], KesType.Number, AcceptsAnyArray: true),
            ["str_len"] = Fn("str_len", KesType.Number, Param("text", KesType.String)),
            ["range"] = Fn("range", KesType.Array(KesType.Number), Param("start", KesType.Number), Param("end", KesType.Number)),
            ["number_to_string"] = Fn("number_to_string", KesType.String, Param("value", KesType.Number)),
            ["bool_to_string"] = Fn("bool_to_string", KesType.String, Param("value", KesType.Bool)),
            ["assert"] = Void("assert", Param("condition", KesType.Bool), Param("message", KesType.String, isOptional: true)),

            ["rt_back"] = Void("rt_back"),
            ["rt_front"] = Void("rt_front"),
            ["bg"] = Void("bg", Param("id", KesType.String)),
            ["trans"] = Void("trans", Param("effect", KesType.String, isOptional: true), Param("duration", KesType.Number, isOptional: true)),
            ["camera_autofocus"] = Void("camera_autofocus", Param("enabled", KesType.Bool)),

            ["cast"] = Void("cast", Param("actor", KesType.Actor)),
            ["show"] = Void("show", Param("actor", KesType.Actor), Param("pos", KesType.Number, isOptional: true), Param("face", KesType.String, isOptional: true), Param("layer", KesType.Number, isOptional: true), Param("z", KesType.Number, isOptional: true), Param("bustup", KesType.Bool, isOptional: true)),
            ["hide"] = Void("hide", Param("actor", KesType.Actor)),
            ["face"] = Void("face", Param("actor", KesType.Actor), Param("exp", KesType.String)),
            ["move"] = Void("move", Param("actor", KesType.Actor), Param("pos", KesType.Number), Param("duration", KesType.Number, isOptional: true)),
            ["action_jump"] = Void("action_jump", Param("actor", KesType.Actor)),

            ["vo"] = Void("vo", Param("id", KesType.String, isOptional: true)),
            ["vf"] = Void("vf", Param("actor", KesType.Actor, isOptional: true), Param("exp", KesType.String)),
            ["p"] = Void("p"),
            ["r"] = Void("r"),
            ["l"] = Void("l"),
            ["cm"] = Void("cm"),
            ["wait_click"] = Void("wait_click"),

            ["bgm"] = Void("bgm", Param("id", KesType.String), Param("loop", KesType.Bool, isOptional: true), Param("fade", KesType.Number, isOptional: true)),
            ["bgm_stop"] = Void("bgm_stop", Param("fade", KesType.Number, isOptional: true)),
            ["se"] = Void("se", Param("id", KesType.String)),
            ["se_stop"] = Void("se_stop", Param("id", KesType.String, isOptional: true)),
            ["se_stop_all"] = Void("se_stop_all"),
            ["voice_stop"] = Void("voice_stop"),

            ["save"] = Void("save", Param("slot", KesType.Number), Param("title", KesType.String, isOptional: true)),
            ["load"] = Void("load", Param("slot", KesType.Number)),
            ["autosave"] = Void("autosave"),
            ["mark_read"] = Void("mark_read", Param("tag", KesType.String)),
            ["is_read"] = Fn("is_read", KesType.Bool, Param("tag", KesType.String)),

            ["wait"] = Void("wait", Param("seconds", KesType.Number)),
            ["set_auto"] = Void("set_auto", Param("enabled", KesType.Bool)),
            ["set_skip"] = Void("set_skip", Param("mode", KesType.String)),
            ["set_config_string"] = Void("set_config_string", Param("key", KesType.String), Param("value", KesType.String)),
            ["set_config_number"] = Void("set_config_number", Param("key", KesType.String), Param("value", KesType.Number)),
            ["set_config_bool"] = Void("set_config_bool", Param("key", KesType.String), Param("value", KesType.Bool)),
            ["get_config"] = Fn("get_config", KesType.String, Param("key", KesType.String)),
        };
    }

    public bool TryResolve(string name, out CallableSignature signature)
    {
        return signatures.TryGetValue(name, out signature!);
    }

    public IReadOnlyCollection<string> Names => signatures.Keys;

    private static CallableSignature Void(string name, params CallableParameter[] parameters)
    {
        return new CallableSignature(name, parameters, KesType.Void);
    }

    private static CallableSignature Fn(string name, KesType returnType, params CallableParameter[] parameters)
    {
        return new CallableSignature(name, parameters, returnType);
    }

    private static CallableParameter Param(string name, KesType type, bool isOptional = false)
    {
        return new CallableParameter(name, type, isOptional);
    }
}
