using KoromoEventScript.Runtime.Core.Diagnostics;
using KoromoEventScript.Runtime.Core.Execution;
using KoromoEventScript.Runtime.Core.Klib;

namespace KoromoEventScript.Runtime.Core.Tests.Execution;

public sealed class KesVmObjectModelTests
{
    [Test]
    public void Run_WithArrayNewSetAndGet_CompletesWithStoredValue()
    {
        var document = CreateDocument(
            [],
            [
                Instruction(0, KlibOpCode.PushInt, [1]),
                Instruction(1, KlibOpCode.PushInt, [2]),
                Instruction(2, KlibOpCode.ArrayNew, [2]),
                Instruction(3, KlibOpCode.Dup),
                Instruction(4, KlibOpCode.PushInt, [1]),
                Instruction(5, KlibOpCode.PushInt, [42]),
                Instruction(6, KlibOpCode.ArraySet),
                Instruction(7, KlibOpCode.PushInt, [1]),
                Instruction(8, KlibOpCode.ArrayGet),
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
    public void ObjectStore_UsesTypedIntegerHandlesForInternalObjects()
    {
        var store = new RuntimeObjectStore();
        var firstArray = store.CreateArray([RuntimeValue.Number(1)]);
        var secondArray = store.CreateArray([RuntimeValue.Number(2)]);
        var instance = store.CreateInstance("Hero");

        Assert.Multiple(() =>
        {
            Assert.That(firstArray.ReferenceKind, Is.EqualTo(RuntimeReferenceKind.Array));
            Assert.That(firstArray.ObjectHandle, Is.EqualTo(0));
            Assert.That(firstArray.ReferenceId, Is.Null);
            Assert.That(secondArray.ReferenceKind, Is.EqualTo(RuntimeReferenceKind.Array));
            Assert.That(secondArray.ObjectHandle, Is.EqualTo(1));
            Assert.That(instance.ReferenceKind, Is.EqualTo(RuntimeReferenceKind.Instance));
            Assert.That(instance.ObjectHandle, Is.EqualTo(0));
        });
    }

    [Test]
    public void Run_WithNewSetFieldAndGetField_CompletesWithStoredValue()
    {
        var document = CreateDocument(
            [
                String("Hero"),
                String("hp"),
                Ref(KlibConstantKind.ClassRef, 0),
                Ref(KlibConstantKind.FieldRef, 1),
            ],
            [
                Instruction(0, KlibOpCode.New, [2, 0]),
                Instruction(1, KlibOpCode.Dup),
                Instruction(2, KlibOpCode.PushInt, [100]),
                Instruction(3, KlibOpCode.SetField, [3]),
                Instruction(4, KlibOpCode.GetField, [3]),
            ]);
        var session = new KesVmSession(document);

        var result = new KesVmExecutor().Run(session);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(session.OperandStack.Single().NumberValue, Is.EqualTo(100));
        });
    }

    [Test]
    public void Run_WithCallAndCallVoid_ExecutesPureCallTargets()
    {
        var document = CreateDocument(
            [
                String("number_to_string"),
                String("assert"),
            ],
            [
                Instruction(0, KlibOpCode.PushInt, [42]),
                Instruction(1, KlibOpCode.Call, [0, 1]),
                Instruction(2, KlibOpCode.PushTrue),
                Instruction(3, KlibOpCode.CallVoid, [1, 1]),
            ]);
        var session = new KesVmSession(document);

        var result = new KesVmExecutor().Run(session);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(session.OperandStack.Single().StringValue, Is.EqualTo("42"));
        });
    }

    [Test]
    public void Run_WithCallMethodAndDispose_ExecutesObjectMethod()
    {
        var document = CreateDocument(
            [
                String("Hero"),
                String("dispose"),
                Ref(KlibConstantKind.ClassRef, 0),
                Ref(KlibConstantKind.MethodRef, 1),
            ],
            [
                Instruction(0, KlibOpCode.New, [2, 0]),
                Instruction(1, KlibOpCode.CallMethod, [3, 0]),
                Instruction(2, KlibOpCode.Pop),
            ]);
        var session = new KesVmSession(document);

        var result = new KesVmExecutor().Run(session);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(session.OperandStack, Is.Empty);
        });
    }

    [Test]
    public void Run_WithArrayGetOutOfRange_ReturnsRuntimeError()
    {
        var document = CreateDocument(
            [],
            [
                Instruction(0, KlibOpCode.PushInt, [1]),
                Instruction(1, KlibOpCode.ArrayNew, [1]),
                Instruction(2, KlibOpCode.PushInt, [9]),
                Instruction(3, KlibOpCode.ArrayGet),
            ]);
        var session = new KesVmSession(document);

        var result = new KesVmExecutor().Run(session);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureKind, Is.EqualTo(RuntimeFailureKind.Runtime));
            Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Code), Does.Contain("KESR3302"));
        });
    }

    [Test]
    public void Run_WithUnknownCallTarget_ReturnsRuntimeError()
    {
        var document = CreateDocument(
            [String("missing_call")],
            [Instruction(0, KlibOpCode.CallVoid, [0, 0])]);
        var session = new KesVmSession(document);

        var result = new KesVmExecutor().Run(session);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureKind, Is.EqualTo(RuntimeFailureKind.Runtime));
            Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Code), Does.Contain("KESR3310"));
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

    private static KlibConstant Ref(KlibConstantKind kind, int stringIndex)
    {
        return new KlibConstant(kind, ReferenceIndex: stringIndex);
    }
}
