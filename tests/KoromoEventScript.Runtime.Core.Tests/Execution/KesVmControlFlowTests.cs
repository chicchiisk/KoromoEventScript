using KoromoEventScript.Runtime.Core.Execution;
using KoromoEventScript.Runtime.Core.Klib;

namespace KoromoEventScript.Runtime.Core.Tests.Execution;

public sealed class KesVmControlFlowTests
{
    [Test]
    public void Run_WithLabelAndJump_SkipsToTargetInstruction()
    {
        var document = CreateDocument(
            [],
            [
                Instruction(0, 0, KlibOpCode.PushInt, [1]),
                Instruction(1, 5, KlibOpCode.Jump, [5]),
                Instruction(2, 10, KlibOpCode.PushInt, [99]),
                Instruction(3, 15, KlibOpCode.Label, [0, 0]),
                Instruction(4, 24, KlibOpCode.PushInt, [2]),
            ]);
        var session = new KesVmSession(document);

        var result = new KesVmExecutor().Run(session);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(session.Continuation.Kind, Is.EqualTo(RuntimeContinuationKind.Completed));
            Assert.That(session.OperandStack.Select(static value => value.NumberValue), Is.EqualTo(new double?[] { 1, 2 }));
        });
    }

    [Test]
    public void Run_WithJumpFalse_UsesBoolCondition()
    {
        var document = CreateDocument(
            [],
            [
                Instruction(0, 0, KlibOpCode.PushFalse),
                Instruction(1, 1, KlibOpCode.JumpFalse, [5]),
                Instruction(2, 6, KlibOpCode.PushInt, [99]),
                Instruction(3, 11, KlibOpCode.PushInt, [42]),
            ]);
        var session = new KesVmSession(document);

        var result = new KesVmExecutor().Run(session);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(session.OperandStack.Single().NumberValue, Is.EqualTo(42));
        });
    }

    [Test]
    public void Run_WithSelect_WaitsForSelectionAndChoiceAdvancesToTarget()
    {
        var document = CreateDocument(
            [
                new KlibConstant(KlibConstantKind.String, StringValue: "どこへ行く？"),
                new KlibConstant(KlibConstantKind.String, StringValue: "街"),
                new KlibConstant(KlibConstantKind.String, StringValue: "森"),
            ],
            [
                Instruction(0, 0, KlibOpCode.PushConst, [0]),
                new KlibInstruction(
                    1,
                    5,
                    KlibOpCode.Select,
                    [2],
                    Source: null,
                    KlibMappingKind.SelectCase,
                    [
                        new KlibSelectCase(TextIndex: 1, Offset: 0),
                        new KlibSelectCase(TextIndex: 2, Offset: 10),
                    ]),
                Instruction(2, 26, KlibOpCode.PushInt, [1]),
                Instruction(3, 31, KlibOpCode.Jump, [5]),
                Instruction(4, 36, KlibOpCode.PushInt, [2]),
                Instruction(5, 41, KlibOpCode.End),
            ]);
        var session = new KesVmSession(document);
        var executor = new KesVmExecutor();

        var waitResult = executor.Run(session);
        var waitContinuation = session.Continuation;
        var chooseResult = executor.ChooseSelection(session, choiceIndex: 1);
        var runResult = executor.Run(session);

        Assert.Multiple(() =>
        {
            Assert.That(waitResult.Succeeded, Is.True);
            Assert.That(waitContinuation.Kind, Is.EqualTo(RuntimeContinuationKind.WaitingForSelection));
            Assert.That(waitContinuation.Prompt, Is.EqualTo("どこへ行く？"));
            Assert.That(waitContinuation.PendingChoices.Select(static choice => choice.Text), Is.EqualTo(new[] { "街", "森" }));
            Assert.That(chooseResult.Succeeded, Is.True);
            Assert.That(runResult.Succeeded, Is.True);
            Assert.That(session.Continuation.Kind, Is.EqualTo(RuntimeContinuationKind.Completed));
            Assert.That(session.OperandStack.Single().NumberValue, Is.EqualTo(2));
        });
    }

    [Test]
    public void Run_WithEnd_CompletesSession()
    {
        var document = CreateDocument([], [Instruction(0, 0, KlibOpCode.End)]);
        var session = new KesVmSession(document);

        var result = new KesVmExecutor().Run(session);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(session.Continuation.Kind, Is.EqualTo(RuntimeContinuationKind.Completed));
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

    private static KlibInstruction Instruction(int index, int offset, KlibOpCode opCode, IReadOnlyList<int>? operands = null)
    {
        return new KlibInstruction(index, offset, opCode, operands ?? [], Source: null, KlibMappingKind.Statement);
    }
}
