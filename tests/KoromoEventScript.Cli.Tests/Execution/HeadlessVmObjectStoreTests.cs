using KoromoEventScript.Cli.Compilation;
using KoromoEventScript.Cli.Execution;
using KoromoEventScript.Cli.Parsing;
using SourceLocation = KoromoEventScript.Runtime.Core.Klib.KlibSourceLocation;

namespace KoromoEventScript.Cli.Tests.Execution;

public class HeadlessVmObjectStoreTests
{
    [Test]
    public void Start_WithArraySetAndArrayGet_WaitsForAdvanceWithUpdatedValue()
    {
        var session = HeadlessVmTestHelper.CreateSession(HeadlessVmTestHelper.CreateSyntheticDocument(
            [
                new KlibInstruction(0, 0, KlibOpCode.PushInt, [1], new SourceLocation(1, 1), KlibMappingKind.Statement),
                new KlibInstruction(1, 5, KlibOpCode.PushInt, [2], new SourceLocation(1, 5), KlibMappingKind.Statement),
                new KlibInstruction(2, 10, KlibOpCode.ArrayNew, [2], new SourceLocation(1, 10), KlibMappingKind.Statement),
                new KlibInstruction(3, 15, KlibOpCode.Dup, [], new SourceLocation(1, 15), KlibMappingKind.Statement),
                new KlibInstruction(4, 16, KlibOpCode.PushInt, [1], new SourceLocation(1, 20), KlibMappingKind.Statement),
                new KlibInstruction(5, 21, KlibOpCode.PushInt, [42], new SourceLocation(1, 25), KlibMappingKind.Statement),
                new KlibInstruction(6, 26, KlibOpCode.ArraySet, [], new SourceLocation(1, 30), KlibMappingKind.Statement),
                new KlibInstruction(7, 27, KlibOpCode.PushInt, [1], new SourceLocation(2, 1), KlibMappingKind.Statement),
                new KlibInstruction(8, 32, KlibOpCode.ArrayGet, [], new SourceLocation(2, 5), KlibMappingKind.Statement),
                new KlibInstruction(9, 33, KlibOpCode.SysCallVoid, [0, 1], new SourceLocation(2, 10), KlibMappingKind.Statement),
                new KlibInstruction(10, 42, KlibOpCode.End, [], new SourceLocation(3, 1), KlibMappingKind.Statement),
            ],
            [
                HeadlessVmTestHelper.StringConstant("scenario.nar"),
            ]));

        Assert.Multiple(() =>
        {
            Assert.That(session.State.Kind, Is.EqualTo(HeadlessVmStateKind.WaitingForAdvance));
            Assert.That(session.Observation.Transcript[^1].Text, Is.EqualTo("42"));
        });
    }

    [Test]
    public void Start_WithNewSetFieldAndGetField_WaitsForAdvanceWithStoredFieldValue()
    {
        var session = HeadlessVmTestHelper.CreateSession(HeadlessVmTestHelper.CreateSyntheticDocument(
            [
                new KlibInstruction(0, 0, KlibOpCode.New, [1, 0], new SourceLocation(1, 1), KlibMappingKind.Statement),
                new KlibInstruction(1, 9, KlibOpCode.Dup, [], new SourceLocation(1, 5), KlibMappingKind.Statement),
                new KlibInstruction(2, 10, KlibOpCode.PushInt, [7], new SourceLocation(1, 10), KlibMappingKind.Statement),
                new KlibInstruction(3, 15, KlibOpCode.SetField, [2], new SourceLocation(1, 15), KlibMappingKind.Statement),
                new KlibInstruction(4, 20, KlibOpCode.GetField, [2], new SourceLocation(2, 1), KlibMappingKind.Statement),
                new KlibInstruction(5, 25, KlibOpCode.SysCallVoid, [0, 1], new SourceLocation(2, 5), KlibMappingKind.Statement),
                new KlibInstruction(6, 34, KlibOpCode.End, [], new SourceLocation(3, 1), KlibMappingKind.Statement),
            ],
            HeadlessVmTestHelper.CreateConstants(
                HeadlessVmTestHelper.StringConstant("scenario.nar"),
                HeadlessVmTestHelper.StringConstant("class.player"),
                HeadlessVmTestHelper.ReferenceConstant(KlibConstantKind.ClassRef, "class.player"),
                HeadlessVmTestHelper.StringConstant("field.hp"),
                HeadlessVmTestHelper.ReferenceConstant(KlibConstantKind.FieldRef, "field.hp"))));

        Assert.Multiple(() =>
        {
            Assert.That(session.State.Kind, Is.EqualTo(HeadlessVmStateKind.WaitingForAdvance));
            Assert.That(session.Observation.Transcript[^1].Text, Is.EqualTo("7"));
        });
    }

    [Test]
    public void Start_WithDisposeMethodCall_ContinuesToNextInstruction()
    {
        var session = HeadlessVmTestHelper.CreateSession(HeadlessVmTestHelper.CreateSyntheticDocument(
            [
                new KlibInstruction(0, 0, KlibOpCode.New, [1, 0], new SourceLocation(1, 1), KlibMappingKind.Statement),
                new KlibInstruction(1, 9, KlibOpCode.CallMethodVoid, [3, 0], new SourceLocation(1, 5), KlibMappingKind.Statement),
                new KlibInstruction(2, 18, KlibOpCode.PushConst, [0], new SourceLocation(2, 1), KlibMappingKind.Statement),
                new KlibInstruction(3, 23, KlibOpCode.SysCallVoid, [5, 1], new SourceLocation(2, 5), KlibMappingKind.Statement),
                new KlibInstruction(4, 32, KlibOpCode.End, [], new SourceLocation(3, 1), KlibMappingKind.Statement),
            ],
            HeadlessVmTestHelper.CreateConstants(
                HeadlessVmTestHelper.StringConstant("after_dispose"),
                HeadlessVmTestHelper.StringConstant("class.player"),
                HeadlessVmTestHelper.ReferenceConstant(KlibConstantKind.ClassRef, "class.player"),
                HeadlessVmTestHelper.StringConstant("dispose"),
                HeadlessVmTestHelper.ReferenceConstant(KlibConstantKind.MethodRef, "dispose"),
                HeadlessVmTestHelper.StringConstant("scenario.nar"))));

        Assert.Multiple(() =>
        {
            Assert.That(session.State.Kind, Is.EqualTo(HeadlessVmStateKind.WaitingForAdvance));
            Assert.That(session.Observation.Transcript[^1].Text, Is.EqualTo("after_dispose"));
        });
    }

    [Test]
    public void Start_WithArrayGetOutOfRange_FaultsSession()
    {
        var session = HeadlessVmTestHelper.CreateSession(HeadlessVmTestHelper.CreateSyntheticDocument(
            [
                new KlibInstruction(0, 0, KlibOpCode.PushInt, [1], new SourceLocation(1, 1), KlibMappingKind.Statement),
                new KlibInstruction(1, 5, KlibOpCode.ArrayNew, [1], new SourceLocation(1, 5), KlibMappingKind.Statement),
                new KlibInstruction(2, 10, KlibOpCode.PushInt, [9], new SourceLocation(1, 10), KlibMappingKind.Statement),
                new KlibInstruction(3, 15, KlibOpCode.ArrayGet, [], new SourceLocation(1, 15), KlibMappingKind.Statement),
                new KlibInstruction(4, 16, KlibOpCode.End, [], new SourceLocation(2, 1), KlibMappingKind.Statement),
            ]));

        Assert.Multiple(() =>
        {
            Assert.That(session.State.Kind, Is.EqualTo(HeadlessVmStateKind.Faulted));
            Assert.That(session.State.Fault?.Message, Does.Contain("out of range"));
        });
    }

    [Test]
    public void Start_WithUnknownMethod_FaultsSession()
    {
        var session = HeadlessVmTestHelper.CreateSession(HeadlessVmTestHelper.CreateSyntheticDocument(
            [
                new KlibInstruction(0, 0, KlibOpCode.New, [1, 0], new SourceLocation(1, 1), KlibMappingKind.Statement),
                new KlibInstruction(1, 9, KlibOpCode.CallMethodVoid, [3, 0], new SourceLocation(1, 5), KlibMappingKind.Statement),
                new KlibInstruction(2, 18, KlibOpCode.End, [], new SourceLocation(2, 1), KlibMappingKind.Statement),
            ],
            HeadlessVmTestHelper.CreateConstants(
                HeadlessVmTestHelper.StringConstant("class.player"),
                HeadlessVmTestHelper.ReferenceConstant(KlibConstantKind.ClassRef, "class.player"),
                HeadlessVmTestHelper.StringConstant("unknown_method"),
                HeadlessVmTestHelper.ReferenceConstant(KlibConstantKind.MethodRef, "unknown_method"))));

        Assert.Multiple(() =>
        {
            Assert.That(session.State.Kind, Is.EqualTo(HeadlessVmStateKind.Faulted));
            Assert.That(session.State.Fault?.Message, Does.Contain("unknown_method"));
        });
    }
}
