using KoromoEventScript.Runtime.Core.Diagnostics;
using KoromoEventScript.Runtime.Core.Effects;
using KoromoEventScript.Runtime.Core.Execution;
using KoromoEventScript.Runtime.Core.Klib;

namespace KoromoEventScript.Runtime.Core.Tests.Stl;

public sealed class AudioStateSystemStlSyscallTests
{
    [Test]
    public void Run_WithAudioSyscalls_PublishesAudioEffectsInOrder()
    {
        var sink = new CollectingEffectSink();
        var document = CreateDocument(
            [
                String("audio.bgm"),
                String("daily_theme"),
                String("audio.se"),
                String("door_open"),
                String("audio.se_stop"),
                String("audio.se_stop_all"),
                String("audio.voice_stop"),
                String("audio.bgm_stop"),
            ],
            [
                Instruction(0, KlibOpCode.PushConst, [1]),
                Instruction(1, KlibOpCode.PushTrue),
                Instruction(2, KlibOpCode.PushInt, [2]),
                Instruction(3, KlibOpCode.SysCallVoid, [0, 3]),
                Instruction(4, KlibOpCode.PushConst, [3]),
                Instruction(5, KlibOpCode.SysCallVoid, [2, 1]),
                Instruction(6, KlibOpCode.PushConst, [3]),
                Instruction(7, KlibOpCode.SysCallVoid, [4, 1]),
                Instruction(8, KlibOpCode.SysCallVoid, [5, 0]),
                Instruction(9, KlibOpCode.SysCallVoid, [6, 0]),
                Instruction(10, KlibOpCode.PushInt, [1]),
                Instruction(11, KlibOpCode.SysCallVoid, [7, 1]),
            ]);
        var session = new KesVmSession(document);

        var result = new KesVmExecutor(effectSink: sink).Run(session);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(sink.Effects.Select(static effect => effect.Name), Is.EqualTo(
                [
                    "audio.bgm",
                    "audio.se",
                    "audio.se_stop",
                    "audio.se_stop_all",
                    "audio.voice_stop",
                    "audio.bgm_stop",
                ]));
            Assert.That(sink.Effects[0].Payload["id"], Is.EqualTo("daily_theme"));
            Assert.That(sink.Effects[0].Payload["loop"], Is.EqualTo("true"));
            Assert.That(sink.Effects[0].Payload["fade"], Is.EqualTo("2"));
        });
    }

    [Test]
    public void Run_WithAudioPureCalls_PublishesAudioEffectsInOrder()
    {
        var sink = new CollectingEffectSink();
        var document = CreateDocument(
            [
                String("bgm"),
                String("assets.audio.bgm.bgm_001_alice2"),
                String("se"),
                String("assets.audio.se.se_001_door"),
                String("se_stop"),
                String("bgm_stop"),
            ],
            [
                Instruction(0, KlibOpCode.PushConst, [1]),
                Instruction(1, KlibOpCode.PushTrue),
                Instruction(2, KlibOpCode.PushInt, [1]),
                Instruction(3, KlibOpCode.CallVoid, [0, 3]),
                Instruction(4, KlibOpCode.PushConst, [3]),
                Instruction(5, KlibOpCode.CallVoid, [2, 1]),
                Instruction(6, KlibOpCode.PushConst, [3]),
                Instruction(7, KlibOpCode.CallVoid, [4, 1]),
                Instruction(8, KlibOpCode.PushInt, [1]),
                Instruction(9, KlibOpCode.CallVoid, [5, 1]),
            ]);
        var session = new KesVmSession(document);

        var result = new KesVmExecutor(effectSink: sink).Run(session);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(sink.Effects.Select(static effect => effect.Name), Is.EqualTo(
                [
                    "audio.bgm",
                    "audio.se",
                    "audio.se_stop",
                    "audio.bgm_stop",
                ]));
            Assert.That(sink.Effects[0].Payload["id"], Is.EqualTo("assets.audio.bgm.bgm_001_alice2"));
            Assert.That(sink.Effects[0].Payload["loop"], Is.EqualTo("true"));
            Assert.That(sink.Effects[0].Payload["fade"], Is.EqualTo("1"));
            Assert.That(sink.Effects[2].Payload["id"], Is.EqualTo("assets.audio.se.se_001_door"));
        });
    }

    [Test]
    public void Run_WithStateSyscalls_TracksReadStateAndPublishesSaveEffects()
    {
        var sink = new CollectingEffectSink();
        var document = CreateDocument(
            [
                String("state.mark_read"),
                String("chapter01_start"),
                String("state.is_read"),
                String("state.save"),
                String("合流前"),
                String("state.autosave"),
                String("state.load"),
            ],
            [
                Instruction(0, KlibOpCode.PushConst, [1]),
                Instruction(1, KlibOpCode.SysCallVoid, [0, 1]),
                Instruction(2, KlibOpCode.PushConst, [1]),
                Instruction(3, KlibOpCode.SysCall, [2, 1]),
                Instruction(4, KlibOpCode.PushInt, [1]),
                Instruction(5, KlibOpCode.PushConst, [4]),
                Instruction(6, KlibOpCode.SysCallVoid, [3, 2]),
                Instruction(7, KlibOpCode.SysCallVoid, [5, 0]),
                Instruction(8, KlibOpCode.PushInt, [1]),
                Instruction(9, KlibOpCode.SysCallVoid, [6, 1]),
            ]);
        var session = new KesVmSession(document);

        var result = new KesVmExecutor(effectSink: sink).Run(session);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(session.OperandStack.Single().BoolValue, Is.True);
            Assert.That(sink.Effects.Select(static effect => effect.Name), Is.EqualTo(
                [
                    "state.mark_read",
                    "state.save",
                    "state.autosave",
                    "state.load",
                ]));
            Assert.That(sink.Effects[1].Payload["slot"], Is.EqualTo("1"));
            Assert.That(sink.Effects[1].Payload["title"], Is.EqualTo("合流前"));
        });
    }

    [Test]
    public void Run_WithSystemConfigSyscalls_UpdatesAndReturnsConfig()
    {
        var sink = new CollectingEffectSink();
        var document = CreateDocument(
            [
                String("system.set_config_number"),
                String("textSpeed"),
                String("system.set_config_bool"),
                String("fullscreen"),
                String("system.set_config_string"),
                String("locale"),
                String("en-US"),
                String("system.get_config"),
                String("system.set_auto"),
                String("system.set_skip"),
                String("read"),
                String("system.wait"),
            ],
            [
                Instruction(0, KlibOpCode.PushConst, [1]),
                Instruction(1, KlibOpCode.PushInt, [2]),
                Instruction(2, KlibOpCode.SysCallVoid, [0, 2]),
                Instruction(3, KlibOpCode.PushConst, [3]),
                Instruction(4, KlibOpCode.PushTrue),
                Instruction(5, KlibOpCode.SysCallVoid, [2, 2]),
                Instruction(6, KlibOpCode.PushConst, [5]),
                Instruction(7, KlibOpCode.PushConst, [6]),
                Instruction(8, KlibOpCode.SysCallVoid, [4, 2]),
                Instruction(9, KlibOpCode.PushConst, [5]),
                Instruction(10, KlibOpCode.SysCall, [7, 1]),
                Instruction(11, KlibOpCode.PushFalse),
                Instruction(12, KlibOpCode.SysCallVoid, [8, 1]),
                Instruction(13, KlibOpCode.PushConst, [10]),
                Instruction(14, KlibOpCode.SysCallVoid, [9, 1]),
                Instruction(15, KlibOpCode.PushInt, [3]),
                Instruction(16, KlibOpCode.SysCallVoid, [11, 1]),
            ]);
        var session = new KesVmSession(document);

        var result = new KesVmExecutor(effectSink: sink).Run(session);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(session.OperandStack.Single().StringValue, Is.EqualTo("en-US"));
            Assert.That(sink.Effects.Select(static effect => effect.Name), Does.Contain("system.wait"));
            Assert.That(sink.Effects.Single(static effect => effect.Name == "system.wait").Payload["seconds"], Is.EqualTo("3"));
            Assert.That(sink.Effects.Select(static effect => effect.Kind), Does.Contain(RuntimeEffectKind.Settings));
        });
    }

    [Test]
    public void Run_WithInvalidSkipMode_ReturnsRuntimeError()
    {
        var document = CreateDocument(
            [
                String("system.set_skip"),
                String("fast"),
            ],
            [
                Instruction(0, KlibOpCode.PushConst, [1]),
                Instruction(1, KlibOpCode.SysCallVoid, [0, 1]),
            ]);
        var session = new KesVmSession(document);

        var result = new KesVmExecutor().Run(session);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureKind, Is.EqualTo(RuntimeFailureKind.Runtime));
            Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Code), Does.Contain("KESR3405"));
        });
    }

    [Test]
    public void Run_WithSystemParamSyscalls_UpdatesAndReturnsGameParameters()
    {
        var document = CreateDocument(
            [
                String("system.set_param_string"),
                String("route_1"),
                String("chapter002_intro"),
                String("system.set_param_number"),
                String("score"),
                String("system.set_param_bool"),
                String("visited"),
                String("system.get_param"),
            ],
            [
                Instruction(0, KlibOpCode.PushConst, [1]),
                Instruction(1, KlibOpCode.PushConst, [2]),
                Instruction(2, KlibOpCode.SysCallVoid, [0, 2]),
                Instruction(3, KlibOpCode.PushConst, [4]),
                Instruction(4, KlibOpCode.PushInt, [7]),
                Instruction(5, KlibOpCode.SysCallVoid, [3, 2]),
                Instruction(6, KlibOpCode.PushConst, [6]),
                Instruction(7, KlibOpCode.PushTrue),
                Instruction(8, KlibOpCode.SysCallVoid, [5, 2]),
                Instruction(9, KlibOpCode.PushConst, [1]),
                Instruction(10, KlibOpCode.SysCall, [7, 1]),
            ]);
        var session = new KesVmSession(document);

        var result = new KesVmExecutor().Run(session);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(session.OperandStack.Single().StringValue, Is.EqualTo("chapter002_intro"));
        });
    }

    [Test]
    public void Run_WithUnknownGameParameter_ReturnsRuntimeError()
    {
        var document = CreateDocument(
            [
                String("system.get_param"),
                String("missing"),
            ],
            [
                Instruction(0, KlibOpCode.PushConst, [1]),
                Instruction(1, KlibOpCode.SysCall, [0, 1]),
            ]);
        var session = new KesVmSession(document);

        var result = new KesVmExecutor().Run(session);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Code), Does.Contain("KESR3406"));
        });
    }

    private static KlibDocument CreateDocument(
        IReadOnlyList<KlibConstant> constants,
        IReadOnlyList<KlibInstruction> instructions)
    {
        return new KlibDocument(
            new KlibVersion(1, 0, 0),
            new KlibModuleInfo("chapter001", "chapter001.module", "events/chapter001.kc", EntryLabel: null),
            [],
            constants,
            [],
            instructions,
            [],
            new KlibDebugInfo(null, null, []));
    }

    private static KlibInstruction Instruction(int index, KlibOpCode opCode, IReadOnlyList<int>? operands = null)
    {
        return new KlibInstruction(index, index, opCode, operands ?? [], Source: null, KlibMappingKind.Statement);
    }

    private static KlibConstant String(string value)
    {
        return new KlibConstant(KlibConstantKind.String, StringValue: value);
    }

    private sealed class CollectingEffectSink : IRuntimeEffectSink
    {
        private readonly List<RuntimeEffect> effects = [];

        public IReadOnlyList<RuntimeEffect> Effects => effects;

        public void Publish(RuntimeEffectBatch batch)
        {
            effects.AddRange(batch.Effects);
        }
    }
}
