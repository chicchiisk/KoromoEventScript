using KoromoEventScript.Cli.Execution;

namespace KoromoEventScript.Cli.Tests.Execution;

public class HeadlessVmExecutionTests
{
    [Test]
    public void Start_StopsAtSayAndCapturesTranscript()
    {
        var (fixture, document) = HeadlessVmTestHelper.CreateScenarioDocument(
            """
actor Riku:
    cast Riku
label #start
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

            Assert.Multiple(() =>
            {
                Assert.That(session.State.Kind, Is.EqualTo(HeadlessVmStateKind.WaitingForAdvance));
                Assert.That(session.State.StopReason, Is.EqualTo(HeadlessVmStopReason.AdvanceRequested));
                Assert.That(session.Observation.Transcript, Has.Count.EqualTo(1));
                Assert.That(session.Observation.Transcript[0].Speaker, Is.EqualTo("actor.riku"));
                Assert.That(session.Observation.Transcript[0].Text, Is.EqualTo("こんにちは"));
            });
        }
    }

    [Test]
    public void ResumeAdvance_ReachesSelectionAndExposesChoices()
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

            Assert.Multiple(() =>
            {
                Assert.That(session.State.Kind, Is.EqualTo(HeadlessVmStateKind.WaitingForSelection));
                Assert.That(session.State.StopReason, Is.EqualTo(HeadlessVmStopReason.SelectionRequested));
                Assert.That(session.Observation.CurrentChoices.Select(static choice => choice.Text),
                    Is.EqualTo(new[] { "続ける", "終わる" }));
            });
        }
    }

    [Test]
    public void ResumeSelection_FollowsBranchUntilCompletion()
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
            session.ResumeSelection(0);
            session.ResumeAdvance();

            Assert.Multiple(() =>
            {
                Assert.That(session.State.Kind, Is.EqualTo(HeadlessVmStateKind.Completed));
                Assert.That(session.State.StopReason, Is.EqualTo(HeadlessVmStopReason.Completed));
                Assert.That(session.Observation.Transcript.Select(static entry => entry.Text),
                    Is.EqualTo(new[] { "こんにちは", "続きます" }));
            });
        }
    }

    [Test]
    public void ResumeSelection_WithInvalidIndexFaultsSession()
    {
        var (fixture, document) = HeadlessVmTestHelper.CreateScenarioDocument(
            """
select:
    case "続ける" #continue
label #continue
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

            session.ResumeSelection(1);

            Assert.Multiple(() =>
            {
                Assert.That(session.State.Kind, Is.EqualTo(HeadlessVmStateKind.Faulted));
                Assert.That(session.State.StopReason, Is.EqualTo(HeadlessVmStopReason.Faulted));
                Assert.That(session.State.Fault, Is.Not.Null);
                Assert.That(session.State.Fault!.Message, Does.Contain("selection"));
            });
        }
    }

    [Test]
    public void Start_WithInvalidJumpFaultsSession()
    {
        var session = HeadlessVmTestHelper.CreateSession(HeadlessVmTestHelper.CreateInvalidJumpDocument());

        Assert.Multiple(() =>
        {
            Assert.That(session.State.Kind, Is.EqualTo(HeadlessVmStateKind.Faulted));
            Assert.That(session.State.Fault, Is.Not.Null);
            Assert.That(session.State.Fault!.InstructionOffset, Is.EqualTo(0));
        });
    }
}
