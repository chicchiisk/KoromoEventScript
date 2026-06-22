using KoromoEventScript.Runtime.Core.Diagnostics;
using KoromoEventScript.Runtime.Core.Effects;
using KoromoEventScript.Runtime.Core.Execution;
using KoromoEventScript.Runtime.Core.Klib;

namespace KoromoEventScript.Runtime.Core.Tests.Stl;

public sealed class CoreStlSyscallTests
{
    [Test]
    public void Run_WithCoreValueSyscalls_PushesReturnValues()
    {
        var document = CreateDocument(
            [
                String("core.array_len"),
                String("core.str_len"),
                String("hello"),
                String("core.number_to_string"),
                String("core.bool_to_string"),
            ],
            [
                Instruction(0, KlibOpCode.PushInt, [10]),
                Instruction(1, KlibOpCode.PushInt, [20]),
                Instruction(2, KlibOpCode.PushInt, [30]),
                Instruction(3, KlibOpCode.ArrayNew, [3]),
                Instruction(4, KlibOpCode.SysCall, [0, 1]),
                Instruction(5, KlibOpCode.PushConst, [2]),
                Instruction(6, KlibOpCode.SysCall, [1, 1]),
                Instruction(7, KlibOpCode.PushInt, [42]),
                Instruction(8, KlibOpCode.SysCall, [3, 1]),
                Instruction(9, KlibOpCode.PushTrue),
                Instruction(10, KlibOpCode.SysCall, [4, 1]),
            ]);
        var session = new KesVmSession(document);

        var result = new KesVmExecutor().Run(session);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(session.OperandStack.Select(static value => value.ToObject()), Is.EqualTo(new object?[] { 3d, 5d, "42", "true" }));
        });
    }

    [Test]
    public void Run_WithCoreRangeSyscall_ReturnsNumberArrayReference()
    {
        var document = CreateDocument(
            [
                String("core.range"),
                String("core.array_len"),
            ],
            [
                Instruction(0, KlibOpCode.PushInt, [2]),
                Instruction(1, KlibOpCode.PushInt, [5]),
                Instruction(2, KlibOpCode.SysCall, [0, 2]),
                Instruction(3, KlibOpCode.Dup),
                Instruction(4, KlibOpCode.SysCall, [1, 1]),
            ]);
        var session = new KesVmSession(document);

        var result = new KesVmExecutor().Run(session);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(session.OperandStack[0].Kind, Is.EqualTo(RuntimeValueKind.Reference));
            Assert.That(session.OperandStack[1].NumberValue, Is.EqualTo(3));
        });
    }

    [Test]
    public void Run_WithCorePrintSyscall_PublishesDebugEffect()
    {
        var sink = new CollectingEffectSink();
        var document = CreateDocument(
            [
                String("core.print"),
                String("trace message"),
            ],
            [
                Instruction(0, KlibOpCode.PushConst, [1]),
                Instruction(1, KlibOpCode.SysCallVoid, [0, 1]),
            ]);
        var session = new KesVmSession(document);

        var result = new KesVmExecutor(effectSink: sink).Run(session);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(sink.Effects, Has.Count.EqualTo(1));
            Assert.That(sink.Effects[0].Kind, Is.EqualTo(RuntimeEffectKind.Diagnostic));
            Assert.That(sink.Effects[0].Payload["message"], Is.EqualTo("trace message"));
        });
    }

    [Test]
    public void Run_WithFailingCoreAssertSyscall_ReturnsRuntimeError()
    {
        var document = CreateDocument(
            [
                String("core.assert"),
                String("must be true"),
            ],
            [
                Instruction(0, KlibOpCode.PushFalse),
                Instruction(1, KlibOpCode.PushConst, [1]),
                Instruction(2, KlibOpCode.SysCallVoid, [0, 2]),
            ]);
        var session = new KesVmSession(document);

        var result = new KesVmExecutor().Run(session);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureKind, Is.EqualTo(RuntimeFailureKind.Runtime));
            Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Code), Does.Contain("KESR3403"));
            Assert.That(result.Diagnostics.Single().Message, Does.Contain("must be true"));
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
