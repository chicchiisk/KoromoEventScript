using KoromoEventScript.Cli.Compilation;
using KoromoEventScript.Cli.Execution;
using KoromoEventScript.Cli.Parsing;

namespace KoromoEventScript.Cli.Tests.Execution;

public class HeadlessVmExecutionTests
{
    private static readonly KlibOpCode[] SupportedOpCodes =
    [
        KlibOpCode.PushConst,
        KlibOpCode.PushNull,
        KlibOpCode.Jump,
        KlibOpCode.Label,
        KlibOpCode.Select,
        KlibOpCode.End,
        KlibOpCode.SysCallVoid,
    ];

    private static IEnumerable<TestCaseData> UnsupportedOpCodeCases()
    {
        return Enum.GetValues<KlibOpCode>()
            .Except(SupportedOpCodes)
            .Select(static opCode => new TestCaseData(opCode).SetName($"Start_WithUnsupportedOpcode_{opCode}_FaultsSession"));
    }

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

    [Test]
    public void Start_WithPushNullNarration_WaitsForAdvanceAndClearsText()
    {
        var session = HeadlessVmTestHelper.CreateSession(CreateDocument(
            [
                new KlibInstruction(0, 0, KlibOpCode.PushNull, [], new SourceLocation(1, 1), KlibMappingKind.Statement),
                new KlibInstruction(1, 1, KlibOpCode.SysCallVoid, [0, 1], new SourceLocation(2, 1), KlibMappingKind.Statement),
                new KlibInstruction(2, 10, KlibOpCode.End, [], new SourceLocation(3, 1), KlibMappingKind.Statement),
            ],
            [
                new KlibConstant(KlibConstantKind.String, StringValue: "scenario.nar"),
            ]));

        Assert.Multiple(() =>
        {
            Assert.That(session.State.Kind, Is.EqualTo(HeadlessVmStateKind.WaitingForAdvance));
            Assert.That(session.Observation.Transcript, Has.Count.EqualTo(1));
            Assert.That(session.Observation.Transcript[0].Text, Is.EqualTo(string.Empty));
        });
    }

    [Test]
    public void Start_WithLabelOnlyDocument_CompletesThroughEnd()
    {
        var session = HeadlessVmTestHelper.CreateSession(CreateDocument(
            [
                new KlibInstruction(0, 0, KlibOpCode.Label, [0, 0], new SourceLocation(1, 1), KlibMappingKind.Statement),
                new KlibInstruction(1, 9, KlibOpCode.End, [], new SourceLocation(2, 1), KlibMappingKind.Statement),
            ],
            [
                new KlibConstant(KlibConstantKind.String, StringValue: "#start"),
            ]));

        Assert.That(session.State.Kind, Is.EqualTo(HeadlessVmStateKind.Completed));
    }

    [Test]
    public void Start_WithEndOnlyDocument_CompletesImmediately()
    {
        var session = HeadlessVmTestHelper.CreateSession(CreateDocument(
            [
                new KlibInstruction(0, 0, KlibOpCode.End, [], new SourceLocation(1, 1), KlibMappingKind.Statement),
            ]));

        Assert.That(session.State.Kind, Is.EqualTo(HeadlessVmStateKind.Completed));
    }

    [TestCaseSource(nameof(UnsupportedOpCodeCases))]
    public void Start_WithUnsupportedOpcode_FaultsSession(KlibOpCode opCode)
    {
        var session = HeadlessVmTestHelper.CreateSession(CreateDocument(
            [
                new KlibInstruction(0, 0, opCode, [], new SourceLocation(1, 1), KlibMappingKind.Statement),
                new KlibInstruction(1, 1, KlibOpCode.End, [], new SourceLocation(2, 1), KlibMappingKind.Statement),
            ]));

        Assert.Multiple(() =>
        {
            Assert.That(session.State.Kind, Is.EqualTo(HeadlessVmStateKind.Faulted));
            Assert.That(session.State.Fault, Is.Not.Null);
            Assert.That(session.State.Fault!.Message, Does.Contain(opCode.ToString()));
        });
    }

    private static KlibDocument CreateDocument(
        IReadOnlyList<KlibInstruction> instructions,
        IReadOnlyList<KlibConstant>? constants = null)
    {
        constants ??= [];
        return new KlibDocument(
            new KlibVersion(1, 0, 0),
            new KlibModuleInfo("events/main", "module.main", "events/main.kc", "#start"),
            [],
            constants,
            [],
            instructions,
            [],
            new KlibDebugInfo(
                null,
                null,
                instructions.Select(static instruction => new KlibSourceMapping(
                    instruction.Offset,
                    0,
                    instruction.Index + 1,
                    1,
                    instruction.Index + 1,
                    1,
                    instruction.MappingKind)).ToArray()));
    }
}
