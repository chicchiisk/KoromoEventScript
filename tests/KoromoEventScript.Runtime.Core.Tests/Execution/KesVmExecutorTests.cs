using KoromoEventScript.Runtime.Core.Diagnostics;
using KoromoEventScript.Runtime.Core.Execution;
using KoromoEventScript.Runtime.Core.Klib;

namespace KoromoEventScript.Runtime.Core.Tests.Execution;

public sealed class KesVmExecutorTests
{
    [Test]
    public void Run_WithStackConstantsVariablesArithmeticAndComparison_UpdatesSessionState()
    {
        var document = CreateDocument(
            [
                new KlibConstant(KlibConstantKind.Number, NumberValue: 2),
                new KlibConstant(KlibConstantKind.String, StringValue: "alpha"),
            ],
            [
                Instruction(0, KlibOpCode.PushConst, [0]),
                Instruction(1, KlibOpCode.PushInt, [3]),
                Instruction(2, KlibOpCode.Add),
                Instruction(3, KlibOpCode.DefVar, [7]),
                Instruction(4, KlibOpCode.LoadVar, [7]),
                Instruction(5, KlibOpCode.PushInt, [5]),
                Instruction(6, KlibOpCode.Eq),
                Instruction(7, KlibOpCode.PushTrue),
                Instruction(8, KlibOpCode.And),
                Instruction(9, KlibOpCode.PushConst, [1]),
                Instruction(10, KlibOpCode.Dup),
                Instruction(11, KlibOpCode.Pop),
            ]);
        var session = new KesVmSession(document);

        var result = new KesVmExecutor().Run(session);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(session.Continuation.Kind, Is.EqualTo(RuntimeContinuationKind.Completed));
            Assert.That(session.Variables[7].NumberValue, Is.EqualTo(5));
            Assert.That(session.OperandStack, Has.Count.EqualTo(2));
            Assert.That(session.OperandStack[0].BoolValue, Is.True);
            Assert.That(session.OperandStack[1].StringValue, Is.EqualTo("alpha"));
        });
    }

    [TestCase(KlibOpCode.Sub, 7)]
    [TestCase(KlibOpCode.Mul, 30)]
    [TestCase(KlibOpCode.Div, 10.0 / 3.0)]
    public void Run_WithNumericBinaryOpcode_PushesNumber(KlibOpCode opCode, double expected)
    {
        var document = CreateDocument(
            [],
            [
                Instruction(0, KlibOpCode.PushInt, [10]),
                Instruction(1, KlibOpCode.PushInt, [3]),
                Instruction(2, opCode),
            ]);
        var session = new KesVmSession(document);

        var result = new KesVmExecutor().Run(session);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(session.OperandStack.Single().NumberValue, Is.EqualTo(expected).Within(0.0001));
        });
    }

    [TestCase(KlibOpCode.Lt, true)]
    [TestCase(KlibOpCode.Le, true)]
    [TestCase(KlibOpCode.Gt, false)]
    [TestCase(KlibOpCode.Ge, false)]
    [TestCase(KlibOpCode.Neq, true)]
    public void Run_WithComparisonOpcode_PushesBool(KlibOpCode opCode, bool expected)
    {
        var document = CreateDocument(
            [],
            [
                Instruction(0, KlibOpCode.PushInt, [2]),
                Instruction(1, KlibOpCode.PushInt, [3]),
                Instruction(2, opCode),
            ]);
        var session = new KesVmSession(document);

        var result = new KesVmExecutor().Run(session);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(session.OperandStack.Single().BoolValue, Is.EqualTo(expected));
        });
    }

    [Test]
    public void Run_WithStackUnderflow_ReturnsRuntimeError()
    {
        var document = CreateDocument([], [Instruction(0, KlibOpCode.Pop)]);
        var session = new KesVmSession(document);

        var result = new KesVmExecutor().Run(session);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureKind, Is.EqualTo(RuntimeFailureKind.Runtime));
            Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Code), Does.Contain("KESR3101"));
        });
    }

    [Test]
    public void Run_WithUninitializedVariable_ReturnsRuntimeError()
    {
        var document = CreateDocument([], [Instruction(0, KlibOpCode.LoadVar, [9])]);
        var session = new KesVmSession(document);

        var result = new KesVmExecutor().Run(session);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureKind, Is.EqualTo(RuntimeFailureKind.Runtime));
            Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Code), Does.Contain("KESR3102"));
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
}
