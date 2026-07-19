using KoromoEventScript.Runtime.Core.Diagnostics;
using KoromoEventScript.Runtime.Core.Effects;
using KoromoEventScript.Runtime.Core.Execution;
using KoromoEventScript.Runtime.Core.Klib;

namespace KoromoEventScript.Runtime.Core.Tests.Stl;

public sealed class SceneActorStlSyscallTests
{
    [Test]
    public void Run_WithSceneSyscalls_PublishesSceneEffectsInOrder()
    {
        var sink = new CollectingEffectSink();
        var document = CreateDocument(
            [
                String("scene.rt_back"),
                String("scene.bg"),
                String("bg_living"),
                String("scene.rt_front"),
                String("scene.trans"),
                String("crossfade"),
                String("scene.camera_autofocus"),
            ],
            [
                Instruction(0, KlibOpCode.SysCallVoid, [0, 0]),
                Instruction(1, KlibOpCode.PushConst, [2]),
                Instruction(2, KlibOpCode.SysCallVoid, [1, 1]),
                Instruction(3, KlibOpCode.SysCallVoid, [3, 0]),
                Instruction(4, KlibOpCode.PushConst, [5]),
                Instruction(5, KlibOpCode.PushInt, [1]),
                Instruction(6, KlibOpCode.SysCallVoid, [4, 2]),
                Instruction(7, KlibOpCode.PushTrue),
                Instruction(8, KlibOpCode.SysCallVoid, [6, 1]),
            ]);
        var session = new KesVmSession(document);

        var result = new KesVmExecutor(effectSink: sink).Run(session);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(sink.Effects.Select(static effect => effect.Name), Is.EqualTo(
                [
                    "scene.rt_back",
                    "scene.bg",
                    "scene.rt_front",
                    "scene.trans",
                    "scene.camera_autofocus",
                ]));
            Assert.That(sink.Effects[1].Payload["id"], Is.EqualTo("bg_living"));
            Assert.That(sink.Effects[3].Payload["effect"], Is.EqualTo("crossfade"));
            Assert.That(sink.Effects[3].Payload["duration"], Is.EqualTo("1"));
            Assert.That(sink.Effects[4].Payload["enabled"], Is.EqualTo("true"));
        });
    }

    [Test]
    public void Run_WithUnityHostWaiting_StopsUntilHostCompletion()
    {
        var sink = new CollectingEffectSink();
        var document = CreateDocument(
            [
                String("scene.bg"),
                String("bg_living"),
                String("core.print"),
                String("after"),
            ],
            [
                Instruction(0, KlibOpCode.PushConst, [1]),
                Instruction(1, KlibOpCode.SysCallVoid, [0, 1]),
                Instruction(2, KlibOpCode.PushConst, [3]),
                Instruction(3, KlibOpCode.SysCallVoid, [2, 1]),
                Instruction(4, KlibOpCode.End),
            ]);
        var session = new KesVmSession(document);
        var executor = new KesVmExecutor(effectSink: sink, waitForHostEffects: true);

        var firstRun = executor.Run(session);

        Assert.Multiple(() =>
        {
            Assert.That(firstRun.Succeeded, Is.True);
            Assert.That(session.Continuation.Kind, Is.EqualTo(RuntimeContinuationKind.WaitingForHost));
            Assert.That(session.Position.InstructionIndex, Is.EqualTo(2));
            Assert.That(sink.Effects.Select(static effect => effect.Name), Is.EqualTo(["scene.bg"]));
        });

        var resumed = session.ResumeHostOperation();
        var secondRun = executor.Run(session);

        Assert.Multiple(() =>
        {
            Assert.That(resumed.Succeeded, Is.True);
            Assert.That(secondRun.Succeeded, Is.True);
            Assert.That(session.Continuation.Kind, Is.EqualTo(RuntimeContinuationKind.Completed));
            Assert.That(sink.Effects.Select(static effect => effect.Name), Is.EqualTo(["scene.bg", "KESR3401"]));
        });
    }

    [Test]
    public void Run_WithActorSyscalls_PublishesActorEffectsInOrder()
    {
        var sink = new CollectingEffectSink();
        var document = CreateDocument(
            [
                String("actor.cast"),
                Actor("Noa"),
                String("actor.show"),
                String("normal"),
                String("actor.face"),
                String("smile"),
                String("actor.move"),
                String("actor.action_jump"),
                String("actor.hide"),
            ],
            [
                Instruction(0, KlibOpCode.PushConst, [1]),
                Instruction(1, KlibOpCode.SysCallVoid, [0, 1]),
                Instruction(2, KlibOpCode.PushConst, [1]),
                Instruction(3, KlibOpCode.PushInt, [0]),
                Instruction(4, KlibOpCode.PushConst, [3]),
                Instruction(5, KlibOpCode.PushInt, [1]),
                Instruction(6, KlibOpCode.PushInt, [2]),
                Instruction(7, KlibOpCode.PushFalse),
                Instruction(8, KlibOpCode.SysCallVoid, [2, 6]),
                Instruction(9, KlibOpCode.PushConst, [1]),
                Instruction(10, KlibOpCode.PushConst, [5]),
                Instruction(11, KlibOpCode.SysCallVoid, [4, 2]),
                Instruction(12, KlibOpCode.PushConst, [1]),
                Instruction(13, KlibOpCode.PushInt, [1]),
                Instruction(14, KlibOpCode.PushInt, [3]),
                Instruction(15, KlibOpCode.SysCallVoid, [6, 3]),
                Instruction(16, KlibOpCode.PushConst, [1]),
                Instruction(17, KlibOpCode.SysCallVoid, [7, 1]),
                Instruction(18, KlibOpCode.PushConst, [1]),
                Instruction(19, KlibOpCode.SysCallVoid, [8, 1]),
            ]);
        var session = new KesVmSession(document);

        var result = new KesVmExecutor(effectSink: sink).Run(session);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(sink.Effects.Select(static effect => effect.Name), Is.EqualTo(
                [
                    "actor.cast",
                    "actor.show",
                    "actor.face",
                    "actor.move",
                    "actor.action_jump",
                    "actor.hide",
                ]));
            Assert.That(sink.Effects[1].Payload["actor"], Is.EqualTo("Noa"));
            Assert.That(sink.Effects[0].Payload["assetBaseName"], Is.EqualTo("Noa"));
            Assert.That(sink.Effects[0].Payload["face"], Is.EqualTo("normal"));
            Assert.That(sink.Effects[1].Payload["face"], Is.EqualTo("normal"));
            Assert.That(sink.Effects[1].Payload["layer"], Is.EqualTo("1"));
            Assert.That(sink.Effects[2].Payload["exp"], Is.EqualTo("smile"));
            Assert.That(sink.Effects[3].Payload["duration"], Is.EqualTo("3"));
        });
    }

    [Test]
    public void Run_WithInvalidActorShowArguments_ReturnsRuntimeError()
    {
        var document = CreateDocument(
            [
                String("actor.show"),
                String("Noa"),
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
            Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Code), Does.Contain("KESR3402"));
        });
    }

    [Test]
    public void Run_WithActorPureCalls_PublishesAssetBaseNameSceneEffects()
    {
        var sink = new CollectingEffectSink();
        var document = CreateDocument(
            [
                Actor("actor.riku"),
                String("assetBaseName"),
                String("riku"),
                String("defaultFace"),
                String("normal"),
                String("show"),
                String("smile"),
                String("face"),
                String("serious"),
            ],
            [
                Instruction(0, KlibOpCode.PushConst, [0]),
                Instruction(1, KlibOpCode.PushConst, [2]),
                Instruction(2, KlibOpCode.SetField, [1]),
                Instruction(3, KlibOpCode.PushConst, [0]),
                Instruction(4, KlibOpCode.PushConst, [4]),
                Instruction(5, KlibOpCode.SetField, [3]),
                Instruction(6, KlibOpCode.PushConst, [0]),
                Instruction(7, KlibOpCode.PushInt, [0]),
                Instruction(8, KlibOpCode.PushConst, [6]),
                Instruction(9, KlibOpCode.CallVoid, [5, 3]),
                Instruction(10, KlibOpCode.PushConst, [0]),
                Instruction(11, KlibOpCode.PushConst, [8]),
                Instruction(12, KlibOpCode.CallVoid, [7, 2]),
                Instruction(13, KlibOpCode.End),
            ]);
        var session = new KesVmSession(document);

        var result = new KesVmExecutor(effectSink: sink).Run(session);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(sink.Effects.Select(static effect => effect.Name), Is.EqualTo(["actor.show", "actor.face"]));
            Assert.That(sink.Effects[0].Payload["actor"], Is.EqualTo("actor.riku"));
            Assert.That(sink.Effects[0].Payload["assetBaseName"], Is.EqualTo("riku"));
            Assert.That(sink.Effects[0].Payload["face"], Is.EqualTo("smile"));
            Assert.That(sink.Effects[1].Payload["assetBaseName"], Is.EqualTo("riku"));
            Assert.That(sink.Effects[1].Payload["exp"], Is.EqualTo("serious"));
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

    private static KlibConstant Actor(string id)
    {
        return new KlibConstant(KlibConstantKind.ActorRef, StringValue: id);
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
