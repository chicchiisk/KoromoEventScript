using KoromoEventScript.Runtime.Core.Effects;
using KoromoEventScript.Runtime.Core.Execution;
using KoromoEventScript.Runtime.Core.Klib;

namespace KoromoEventScript.Runtime.Core.Tests.Stl;

public sealed class TextFlowStlSyscallTests
{
    [Test]
    public void Run_WithSayNarAndTextSyscalls_WaitsAfterEachMessage()
    {
        var sink = new CollectingEffectSink();
        var document = CreateDocument(
            [
                String("scenario.say"),
                Actor("Noa"),
                String("おはよう。"),
                String("scenario.nar"),
                String("朝になった。"),
                String("text.r"),
                String("text.cm"),
                String("text.vo"),
                String("voice_001"),
                String("audio.vo_auto"),
            ],
            [
                Instruction(0, KlibOpCode.PushConst, [1]),
                Instruction(1, KlibOpCode.PushConst, [2]),
                Instruction(2, KlibOpCode.SysCallVoid, [0, 2]),
                Instruction(3, KlibOpCode.PushConst, [4]),
                Instruction(4, KlibOpCode.SysCallVoid, [3, 1]),
                Instruction(5, KlibOpCode.SysCallVoid, [5, 0]),
                Instruction(6, KlibOpCode.SysCallVoid, [6, 0]),
                Instruction(7, KlibOpCode.PushConst, [8]),
                Instruction(8, KlibOpCode.SysCallVoid, [7, 1]),
                Instruction(9, KlibOpCode.SysCallVoid, [9, 0]),
            ]);
        var session = new KesVmSession(document);

        var executor = new KesVmExecutor(effectSink: sink);

        var sayResult = executor.Run(session);
        var sayAdvance = executor.ContinueAdvance(session);
        var narResult = executor.Run(session);
        var narAdvance = executor.ContinueAdvance(session);
        var completed = executor.Run(session);

        Assert.Multiple(() =>
        {
            Assert.That(sayResult.Succeeded, Is.True);
            Assert.That(sayAdvance.Succeeded, Is.True);
            Assert.That(narResult.Succeeded, Is.True);
            Assert.That(narAdvance.Succeeded, Is.True);
            Assert.That(completed.Succeeded, Is.True);
            Assert.That(session.Continuation.Kind, Is.EqualTo(RuntimeContinuationKind.Completed));
            Assert.That(sink.Effects.Select(static effect => effect.Name), Is.EqualTo(
                [
                    "scenario.say",
                    "scenario.nar",
                    "text.r",
                    "text.cm",
                    "text.vo",
                    "audio.vo_auto",
                ]));
            Assert.That(sink.Effects[0].Kind, Is.EqualTo(RuntimeEffectKind.Ui));
            Assert.That(sink.Effects[0].Payload["actor"], Is.EqualTo("Noa"));
            Assert.That(sink.Effects[0].Payload["text"], Is.EqualTo("おはよう。"));
            Assert.That(sink.Effects[1].Payload["text"], Is.EqualTo("朝になった。"));
            Assert.That(sink.Effects[4].Kind, Is.EqualTo(RuntimeEffectKind.Audio));
            Assert.That(sink.Effects[4].Payload["id"], Is.EqualTo("voice_001"));
        });
    }

    [TestCase("text.p")]
    [TestCase("text.l")]
    [TestCase("text.wait_click")]
    public void Run_WithTextWaitSyscall_WaitsForAdvanceAndCanResume(string syscall)
    {
        var sink = new CollectingEffectSink();
        var document = CreateDocument(
            [String(syscall)],
            [
                Instruction(0, KlibOpCode.SysCallVoid, [0, 0]),
                Instruction(1, KlibOpCode.PushInt, [7]),
            ]);
        var session = new KesVmSession(document);
        var executor = new KesVmExecutor(effectSink: sink);

        var waitResult = executor.Run(session);
        var continueResult = executor.ContinueAdvance(session);
        var runResult = executor.Run(session);

        Assert.Multiple(() =>
        {
            Assert.That(waitResult.Succeeded, Is.True);
            Assert.That(session.OperandStack.Single().NumberValue, Is.EqualTo(7));
            Assert.That(continueResult.Succeeded, Is.True);
            Assert.That(runResult.Succeeded, Is.True);
            Assert.That(session.Continuation.Kind, Is.EqualTo(RuntimeContinuationKind.Completed));
            Assert.That(sink.Effects.Select(static effect => effect.Kind), Does.Contain(RuntimeEffectKind.Wait));
            Assert.That(sink.Effects.Last(static effect => effect.Kind == RuntimeEffectKind.Wait).Name, Is.EqualTo("Click"));
        });
    }

    [Test]
    public void Run_WithSelectAfterTextWait_WaitsForClickThenSelectionAndResumesChoice()
    {
        var document = CreateDocument(
            [
                String("text.wait_click"),
                String("どちら？"),
                String("続ける"),
                String("終わる"),
            ],
            [
                Instruction(0, 0, KlibOpCode.SysCallVoid, [0, 0]),
                Instruction(1, 5, KlibOpCode.PushConst, [1]),
                new KlibInstruction(
                    2,
                    10,
                    KlibOpCode.Select,
                    [2],
                    Source: null,
                    KlibMappingKind.SelectCase,
                    [
                        new KlibSelectCase(TextIndex: 2, Offset: 0),
                        new KlibSelectCase(TextIndex: 3, Offset: 10),
                    ]),
                Instruction(3, 31, KlibOpCode.PushInt, [1]),
                Instruction(4, 36, KlibOpCode.Jump, [5]),
                Instruction(5, 41, KlibOpCode.PushInt, [2]),
                Instruction(6, 46, KlibOpCode.End),
            ]);
        var session = new KesVmSession(document);
        var executor = new KesVmExecutor();

        var clickWait = executor.Run(session);
        var clickResume = executor.ContinueAdvance(session);
        var selectionWait = executor.Run(session);
        var selectionContinuation = session.Continuation;
        var choice = executor.ChooseSelection(session, choiceIndex: 1);
        var completed = executor.Run(session);

        Assert.Multiple(() =>
        {
            Assert.That(clickWait.Succeeded, Is.True);
            Assert.That(clickResume.Succeeded, Is.True);
            Assert.That(selectionWait.Succeeded, Is.True);
            Assert.That(selectionContinuation.PendingChoices.Select(static choice => choice.Text), Is.EqualTo(["続ける", "終わる"]));
            Assert.That(choice.Succeeded, Is.True);
            Assert.That(completed.Succeeded, Is.True);
            Assert.That(session.OperandStack.Single().NumberValue, Is.EqualTo(2));
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

    private static KlibInstruction Instruction(int index, int offset, KlibOpCode opCode, IReadOnlyList<int>? operands = null)
    {
        return new KlibInstruction(index, offset, opCode, operands ?? [], Source: null, KlibMappingKind.Statement);
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
