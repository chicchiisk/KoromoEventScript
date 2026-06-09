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
                Assert.That(snapshot.SchemaVersion, Is.EqualTo(1));
                Assert.That(snapshot.Position.ScriptId, Is.EqualTo(document.Module.ScriptId));
                Assert.That(snapshot.Position.InstructionOffset, Is.EqualTo(session.State.InstructionOffset));
                Assert.That(snapshot.Continuation.Kind, Is.EqualTo(HeadlessVmContinuationKind.WaitingForAdvance));
                Assert.That(snapshot.VariableStates, Is.Empty);
                Assert.That(snapshot.CallFrames, Is.Empty);
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
}
