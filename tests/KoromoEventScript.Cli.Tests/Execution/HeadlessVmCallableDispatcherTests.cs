using KoromoEventScript.Cli.Compilation;
using KoromoEventScript.Cli.Execution;
using KoromoEventScript.Cli.Parsing;
using SourceLocation = KoromoEventScript.Runtime.Core.Klib.KlibSourceLocation;

namespace KoromoEventScript.Cli.Tests.Execution;

public class HeadlessVmCallableDispatcherTests
{
    [Test]
    public void Start_WithCallNumberToString_WaitsForAdvanceWithConvertedNarration()
    {
        var session = HeadlessVmTestHelper.CreateSession(HeadlessVmTestHelper.CreateSyntheticDocument(
            [
                new KlibInstruction(0, 0, KlibOpCode.PushInt, [42], new SourceLocation(1, 1), KlibMappingKind.Statement),
                new KlibInstruction(1, 5, KlibOpCode.Call, [0, 1], new SourceLocation(1, 5), KlibMappingKind.Statement),
                new KlibInstruction(2, 14, KlibOpCode.SysCallVoid, [1, 1], new SourceLocation(1, 10), KlibMappingKind.Statement),
                new KlibInstruction(3, 23, KlibOpCode.End, [], new SourceLocation(2, 1), KlibMappingKind.Statement),
            ],
            [
                HeadlessVmTestHelper.StringConstant("number_to_string"),
                HeadlessVmTestHelper.StringConstant("scenario.nar"),
            ]));

        Assert.Multiple(() =>
        {
            Assert.That(session.State.Kind, Is.EqualTo(HeadlessVmStateKind.WaitingForAdvance));
            Assert.That(session.Observation.Transcript[^1].Text, Is.EqualTo("42"));
        });
    }

    [Test]
    public void Start_WithCallArrayLen_WaitsForAdvanceWithComputedLength()
    {
        var session = HeadlessVmTestHelper.CreateSession(HeadlessVmTestHelper.CreateSyntheticDocument(
            [
                new KlibInstruction(0, 0, KlibOpCode.PushInt, [1], new SourceLocation(1, 1), KlibMappingKind.Statement),
                new KlibInstruction(1, 5, KlibOpCode.PushInt, [2], new SourceLocation(1, 5), KlibMappingKind.Statement),
                new KlibInstruction(2, 10, KlibOpCode.PushInt, [3], new SourceLocation(1, 10), KlibMappingKind.Statement),
                new KlibInstruction(3, 15, KlibOpCode.ArrayNew, [3], new SourceLocation(1, 15), KlibMappingKind.Statement),
                new KlibInstruction(4, 20, KlibOpCode.Call, [0, 1], new SourceLocation(1, 20), KlibMappingKind.Statement),
                new KlibInstruction(5, 29, KlibOpCode.SysCallVoid, [1, 1], new SourceLocation(1, 25), KlibMappingKind.Statement),
                new KlibInstruction(6, 38, KlibOpCode.End, [], new SourceLocation(2, 1), KlibMappingKind.Statement),
            ],
            [
                HeadlessVmTestHelper.StringConstant("array_len"),
                HeadlessVmTestHelper.StringConstant("scenario.nar"),
            ]));

        Assert.Multiple(() =>
        {
            Assert.That(session.State.Kind, Is.EqualTo(HeadlessVmStateKind.WaitingForAdvance));
            Assert.That(session.Observation.Transcript[^1].Text, Is.EqualTo("3"));
        });
    }

    [Test]
    public void Start_WithUnknownCall_FaultsSession()
    {
        var session = HeadlessVmTestHelper.CreateSession(HeadlessVmTestHelper.CreateSyntheticDocument(
            [
                new KlibInstruction(0, 0, KlibOpCode.CallVoid, [0, 0], new SourceLocation(1, 1), KlibMappingKind.Statement),
                new KlibInstruction(1, 9, KlibOpCode.End, [], new SourceLocation(2, 1), KlibMappingKind.Statement),
            ],
            [
                HeadlessVmTestHelper.StringConstant("unknown_builtin"),
            ]));

        Assert.Multiple(() =>
        {
            Assert.That(session.State.Kind, Is.EqualTo(HeadlessVmStateKind.Faulted));
            Assert.That(session.State.Fault?.Message, Does.Contain("unknown_builtin"));
        });
    }

    [Test]
    public void Start_WithRuntimeCommandCallVoid_ContinuesToNextInstruction()
    {
        var session = HeadlessVmTestHelper.CreateSession(HeadlessVmTestHelper.CreateSyntheticDocument(
            [
                new KlibInstruction(0, 0, KlibOpCode.PushConst, [1], new SourceLocation(1, 1), KlibMappingKind.Statement),
                new KlibInstruction(1, 5, KlibOpCode.CallVoid, [0, 1], new SourceLocation(1, 5), KlibMappingKind.Statement),
                new KlibInstruction(2, 14, KlibOpCode.PushConst, [3], new SourceLocation(2, 1), KlibMappingKind.Statement),
                new KlibInstruction(3, 19, KlibOpCode.SysCallVoid, [2, 1], new SourceLocation(2, 5), KlibMappingKind.Statement),
                new KlibInstruction(4, 28, KlibOpCode.End, [], new SourceLocation(3, 1), KlibMappingKind.Statement),
            ],
            [
                HeadlessVmTestHelper.StringConstant("bg"),
                HeadlessVmTestHelper.StringConstant("bg001"),
                HeadlessVmTestHelper.StringConstant("scenario.nar"),
                HeadlessVmTestHelper.StringConstant("after_bg"),
            ]));

        Assert.Multiple(() =>
        {
            Assert.That(session.State.Kind, Is.EqualTo(HeadlessVmStateKind.WaitingForAdvance));
            Assert.That(session.Observation.Transcript[^1].Text, Is.EqualTo("after_bg"));
        });
    }
}
