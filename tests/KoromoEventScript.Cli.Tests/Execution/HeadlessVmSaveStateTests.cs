using KoromoEventScript.Cli.Compilation;
using KoromoEventScript.Cli.Execution;

namespace KoromoEventScript.Cli.Tests.Execution;

public class HeadlessVmSaveStateTests
{
    [Test]
    public void ValueSnapshot_FromObject_NormalizesPrimitiveAndUnsupportedValues()
    {
        var primitive = HeadlessVmValueSnapshot.FromObject(42d);
        var array = HeadlessVmValueSnapshot.FromObject((IReadOnlyList<object?>) ["alpha", true, null]);
        var unsupported = HeadlessVmValueSnapshot.FromObject(new object());

        Assert.Multiple(() =>
        {
            Assert.That(primitive.Kind, Is.EqualTo(HeadlessVmValueKind.Number));
            Assert.That(primitive.NumberValue, Is.EqualTo(42d));
            Assert.That(array.Kind, Is.EqualTo(HeadlessVmValueKind.Array));
            Assert.That(array.ArrayItems?.Select(static item => item.Kind),
                Is.EqualTo(new[]
                {
                    HeadlessVmValueKind.String,
                    HeadlessVmValueKind.Bool,
                    HeadlessVmValueKind.Null,
                }));
            Assert.That(unsupported.Kind, Is.EqualTo(HeadlessVmValueKind.Unsupported));
        });
    }

    [Test]
    public void ContinuationState_WaitingForSelection_PreservesPromptAndChoices()
    {
        var continuation = HeadlessVmContinuationState.WaitingForSelection(
            12,
            "prompt",
            [new HeadlessVmChoiceSnapshot("A", 100), new HeadlessVmChoiceSnapshot("B", 200)]);

        Assert.Multiple(() =>
        {
            Assert.That(continuation.Kind, Is.EqualTo(HeadlessVmContinuationKind.WaitingForSelection));
            Assert.That(continuation.ResumeOffset, Is.EqualTo(12));
            Assert.That(continuation.Prompt, Is.EqualTo("prompt"));
            Assert.That(continuation.PendingChoices?.Select(static choice => choice.TargetOffset),
                Is.EqualTo(new[] { 100, 200 }));
        });
    }

    [Test]
    public void ExportSaveState_WaitingForAdvance_CapturesExecutionPositionAndContinuation()
    {
        var (fixture, document) = HeadlessVmTestHelper.CreateScenarioDocument(
            """
actor Riku:
    cast Riku
say Riku:
    こんにちは
nar:
    つづく
""",
            """
entry = intro
intro = {
    chapter = "events/main.kc"
}
""");
        using (fixture)
        {
            var session = HeadlessVmTestHelper.CreateSession(document);

            var snapshot = session.ExportSaveState();

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.SchemaVersion, Is.EqualTo(2));
                Assert.That(snapshot.Position.ScriptId, Is.EqualTo(document.Module.ScriptId));
                Assert.That(snapshot.Position.InstructionOffset, Is.EqualTo(session.State.InstructionOffset));
                Assert.That(snapshot.Continuation.Kind, Is.EqualTo(HeadlessVmContinuationKind.WaitingForAdvance));
                Assert.That(snapshot.VariableStates, Is.Empty);
                Assert.That(snapshot.OperandStack, Is.Empty);
                Assert.That(snapshot.CallFrames, Is.Empty);
                Assert.That(snapshot.Objects, Is.Empty);
            });
        }
    }

    [Test]
    public void ExportSaveState_WaitingForSelection_CapturesPendingChoices()
    {
        var (fixture, document) = HeadlessVmTestHelper.CreateScenarioDocument(
            """
actor Riku:
    cast Riku
say Riku:
    こんにちは
select:
    case "続ける" #continue
    case "終わる" #end
label #continue
nar:
    続きます
jump #end
label #end
""",
            """
entry = intro
intro = {
    chapter = "events/main.kc"
}
""");
        using (fixture)
        {
            var session = HeadlessVmTestHelper.CreateSession(document);
            session.ResumeAdvance();

            var snapshot = session.ExportSaveState();

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.Continuation.Kind, Is.EqualTo(HeadlessVmContinuationKind.WaitingForSelection));
                Assert.That(snapshot.Continuation.PendingChoices?.Select(static choice => choice.Text),
                    Is.EqualTo(new[] { "続ける", "終わる" }));
                Assert.That(snapshot.Continuation.ResumeOffset, Is.EqualTo(session.State.InstructionOffset));
            });
        }
    }

    [Test]
    public void Restore_RoundTripsWaitingForSelectionState()
    {
        var (fixture, document) = HeadlessVmTestHelper.CreateScenarioDocument(
            """
actor Riku:
    cast Riku
say Riku:
    こんにちは
select:
    case "続ける" #continue
    case "終わる" #end
label #continue
nar:
    続きます
jump #end
label #end
""",
            """
entry = intro
intro = {
    chapter = "events/main.kc"
}
""");
        using (fixture)
        {
            var originalSession = HeadlessVmTestHelper.CreateSession(document);
            originalSession.ResumeAdvance();
            var snapshot = originalSession.ExportSaveState();

            var restoredSession = new HeadlessVmSession();
            restoredSession.Restore(document, snapshot);
            restoredSession.ResumeSelection(0);
            restoredSession.ResumeAdvance();

            Assert.Multiple(() =>
            {
                Assert.That(restoredSession.State.Kind, Is.EqualTo(HeadlessVmStateKind.Completed));
                Assert.That(restoredSession.Observation.Transcript.Select(static entry => entry.Text),
                    Is.EqualTo(new[] { "続きます" }));
            });
        }
    }

    [Test]
    public void Restore_WithInvalidScriptIdFaultsSession()
    {
        var (fixture, document) = HeadlessVmTestHelper.CreateScenarioDocument(
            """
nar:
    こんにちは
""",
            """
entry = intro
intro = {
    chapter = "events/main.kc"
}
""");
        using (fixture)
        {
            var session = HeadlessVmTestHelper.CreateSession(document);
            var snapshot = session.ExportSaveState() with
            {
                Position = session.ExportSaveState().Position with { ScriptId = "events/other" }
            };

            var restoredSession = new HeadlessVmSession();
            restoredSession.Restore(document, snapshot);

            Assert.Multiple(() =>
            {
                Assert.That(restoredSession.State.Kind, Is.EqualTo(HeadlessVmStateKind.Faulted));
                Assert.That(restoredSession.State.Fault, Is.Not.Null);
                Assert.That(restoredSession.State.Fault!.Message, Does.Contain("script"));
            });
        }
    }

    [Test]
    public void Restore_WithInvalidInstructionOffsetFaultsSession()
    {
        var (fixture, document) = HeadlessVmTestHelper.CreateScenarioDocument(
            """
nar:
    こんにちは
""",
            """
entry = intro
intro = {
    chapter = "events/main.kc"
}
""");
        using (fixture)
        {
            var session = HeadlessVmTestHelper.CreateSession(document);
            var exported = session.ExportSaveState();
            var snapshot = exported with
            {
                Position = exported.Position with { InstructionOffset = 9999 }
            };

            var restoredSession = new HeadlessVmSession();
            restoredSession.Restore(document, snapshot);

            Assert.Multiple(() =>
            {
                Assert.That(restoredSession.State.Kind, Is.EqualTo(HeadlessVmStateKind.Faulted));
                Assert.That(restoredSession.State.Fault, Is.Not.Null);
                Assert.That(restoredSession.State.Fault!.Message, Does.Contain("offset"));
            });
        }
    }

    [Test]
    public void ExportSaveState_WithArrayVariable_CapturesObjectSnapshots()
    {
        var session = HeadlessVmTestHelper.CreateSession(HeadlessVmTestHelper.CreateSyntheticDocument(
            [
                new KlibInstruction(0, 0, KlibOpCode.PushInt, [1], new KoromoEventScript.Cli.Parsing.SourceLocation(1, 1), KlibMappingKind.Statement),
                new KlibInstruction(1, 5, KlibOpCode.PushInt, [2], new KoromoEventScript.Cli.Parsing.SourceLocation(1, 5), KlibMappingKind.Statement),
                new KlibInstruction(2, 10, KlibOpCode.ArrayNew, [2], new KoromoEventScript.Cli.Parsing.SourceLocation(1, 10), KlibMappingKind.Statement),
                new KlibInstruction(3, 15, KlibOpCode.DefVar, [0], new KoromoEventScript.Cli.Parsing.SourceLocation(1, 15), KlibMappingKind.Statement),
                new KlibInstruction(4, 20, KlibOpCode.PushNull, [], new KoromoEventScript.Cli.Parsing.SourceLocation(2, 1), KlibMappingKind.Statement),
                new KlibInstruction(5, 21, KlibOpCode.Select, [1], new KoromoEventScript.Cli.Parsing.SourceLocation(2, 5), KlibMappingKind.Statement, [new KlibSelectCase(0, 0)]),
                new KlibInstruction(6, 34, KlibOpCode.End, [], new KoromoEventScript.Cli.Parsing.SourceLocation(3, 1), KlibMappingKind.Statement),
            ],
            [new KlibConstant(KlibConstantKind.String, StringValue: "続ける")],
            [new KlibVariable(0, 0, KlibVariableType.Array, KlibScopeKind.Script, 0, null)]));

        var snapshot = session.ExportSaveState();

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.VariableStates, Has.Count.EqualTo(1));
            Assert.That(snapshot.VariableStates[0].Value.Kind, Is.EqualTo(HeadlessVmValueKind.Reference));
            Assert.That(snapshot.Objects, Has.Count.EqualTo(1));
            Assert.That(snapshot.Objects[0].Kind, Is.EqualTo(HeadlessVmObjectSnapshotKind.Array));
            Assert.That(snapshot.Objects[0].ArrayItems?.Select(static item => item.NumberValue),
                Is.EqualTo(new double?[] { 1d, 2d }));
        });
    }

    [Test]
    public void Restore_AfterWaitingForAdvance_PreservesOperandStackAndObjectStore()
    {
        var document = HeadlessVmTestHelper.CreateSyntheticDocument(
            [
                new KlibInstruction(0, 0, KlibOpCode.PushInt, [40], new KoromoEventScript.Cli.Parsing.SourceLocation(1, 1), KlibMappingKind.Statement),
                new KlibInstruction(1, 5, KlibOpCode.PushInt, [2], new KoromoEventScript.Cli.Parsing.SourceLocation(1, 5), KlibMappingKind.Statement),
                new KlibInstruction(2, 10, KlibOpCode.PushConst, [0], new KoromoEventScript.Cli.Parsing.SourceLocation(1, 10), KlibMappingKind.Statement),
                new KlibInstruction(3, 15, KlibOpCode.SysCallVoid, [1, 1], new KoromoEventScript.Cli.Parsing.SourceLocation(1, 15), KlibMappingKind.Statement),
                new KlibInstruction(4, 24, KlibOpCode.Add, [], new KoromoEventScript.Cli.Parsing.SourceLocation(2, 1), KlibMappingKind.Statement),
                new KlibInstruction(5, 25, KlibOpCode.SysCallVoid, [1, 1], new KoromoEventScript.Cli.Parsing.SourceLocation(2, 5), KlibMappingKind.Statement),
                new KlibInstruction(6, 34, KlibOpCode.End, [], new KoromoEventScript.Cli.Parsing.SourceLocation(3, 1), KlibMappingKind.Statement),
            ],
            [
                new KlibConstant(KlibConstantKind.String, StringValue: "pause"),
                new KlibConstant(KlibConstantKind.String, StringValue: "scenario.nar"),
            ]);

        var originalSession = HeadlessVmTestHelper.CreateSession(document);
        var snapshot = originalSession.ExportSaveState();

        var restoredSession = new HeadlessVmSession();
        restoredSession.Restore(document, snapshot);
        restoredSession.ResumeAdvance();

        Assert.Multiple(() =>
        {
            Assert.That(restoredSession.State.Kind, Is.EqualTo(HeadlessVmStateKind.WaitingForAdvance));
            Assert.That(restoredSession.Observation.Transcript.Select(static entry => entry.Text),
                Is.EqualTo(new[] { "42" }));
        });
    }

    [Test]
    public void Restore_WaitingForSelection_PreservesArrayReferenceAcrossResume()
    {
        var document = HeadlessVmTestHelper.CreateSyntheticDocument(
            [
                new KlibInstruction(0, 0, KlibOpCode.PushInt, [1], new KoromoEventScript.Cli.Parsing.SourceLocation(1, 1), KlibMappingKind.Statement),
                new KlibInstruction(1, 5, KlibOpCode.PushInt, [2], new KoromoEventScript.Cli.Parsing.SourceLocation(1, 5), KlibMappingKind.Statement),
                new KlibInstruction(2, 10, KlibOpCode.ArrayNew, [2], new KoromoEventScript.Cli.Parsing.SourceLocation(1, 10), KlibMappingKind.Statement),
                new KlibInstruction(3, 15, KlibOpCode.DefVar, [0], new KoromoEventScript.Cli.Parsing.SourceLocation(1, 15), KlibMappingKind.Statement),
                new KlibInstruction(4, 20, KlibOpCode.PushNull, [], new KoromoEventScript.Cli.Parsing.SourceLocation(2, 1), KlibMappingKind.Statement),
                new KlibInstruction(5, 21, KlibOpCode.Select, [1], new KoromoEventScript.Cli.Parsing.SourceLocation(2, 5), KlibMappingKind.Statement, [new KlibSelectCase(0, 0)]),
                new KlibInstruction(6, 34, KlibOpCode.LoadVar, [0], new KoromoEventScript.Cli.Parsing.SourceLocation(3, 1), KlibMappingKind.Statement),
                new KlibInstruction(7, 39, KlibOpCode.PushInt, [1], new KoromoEventScript.Cli.Parsing.SourceLocation(3, 5), KlibMappingKind.Statement),
                new KlibInstruction(8, 44, KlibOpCode.ArrayGet, [], new KoromoEventScript.Cli.Parsing.SourceLocation(3, 10), KlibMappingKind.Statement),
                new KlibInstruction(9, 45, KlibOpCode.SysCallVoid, [1, 1], new KoromoEventScript.Cli.Parsing.SourceLocation(3, 15), KlibMappingKind.Statement),
                new KlibInstruction(10, 54, KlibOpCode.End, [], new KoromoEventScript.Cli.Parsing.SourceLocation(4, 1), KlibMappingKind.Statement),
            ],
            [
                new KlibConstant(KlibConstantKind.String, StringValue: "続ける"),
                new KlibConstant(KlibConstantKind.String, StringValue: "scenario.nar"),
            ],
            [new KlibVariable(0, 0, KlibVariableType.Array, KlibScopeKind.Script, 0, null)]);

        var originalSession = HeadlessVmTestHelper.CreateSession(document);
        var snapshot = originalSession.ExportSaveState();

        var restoredSession = new HeadlessVmSession();
        restoredSession.Restore(document, snapshot);
        restoredSession.ResumeSelection(0);

        Assert.Multiple(() =>
        {
            Assert.That(restoredSession.State.Kind, Is.EqualTo(HeadlessVmStateKind.WaitingForAdvance));
            Assert.That(restoredSession.Observation.Transcript.Select(static entry => entry.Text),
                Is.EqualTo(new[] { "2" }));
        });
    }
}
