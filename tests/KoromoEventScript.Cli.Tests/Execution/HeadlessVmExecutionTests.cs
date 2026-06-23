using KoromoEventScript.Cli.Compilation;
using KoromoEventScript.Cli.Execution;
using KoromoEventScript.Cli.Parsing;
using SourceLocation = KoromoEventScript.Runtime.Core.Klib.KlibSourceLocation;

namespace KoromoEventScript.Cli.Tests.Execution;

public class HeadlessVmExecutionTests
{
    [Test]
    public void Start_StopsAtSayAndCapturesTranscript()
    {
        var (fixture, document) = HeadlessVmTestHelper.CreateScenarioDocument(
            """
actor Riku:
    var faceName: string = "normal"
standby:
    riku : Riku
label #start
say riku:
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
    var faceName: string = "normal"
standby:
    riku : Riku
say riku:
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
    var faceName: string = "normal"
standby:
    riku : Riku
say riku:
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
    public void BroadSurfaceScenario_ReachesSelectionWithoutFaultingOnCompilerEmittedOpcodes()
    {
        var (fixture, document) = HeadlessVmTestHelper.CreateScenarioDocument(
            """
actor Riku:
    var faceName: string = "normal"
standby:
    riku : Riku
var actors: Actor[] = [riku]
var total: number = 1
if true:
    total = total + 1
else:
    total = total - 1
while total < 3:
    total = total + 1
for actor in actors:
    show actor 0
label #start
say riku:
    こんにちは
nar:
    つづく
select:
    case "続ける" #continue
    case "終わる" #end
label #continue
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
            session.ResumeAdvance();

            Assert.Multiple(() =>
            {
                Assert.That(session.State.Kind, Is.EqualTo(HeadlessVmStateKind.WaitingForSelection));
                Assert.That(session.State.Fault, Is.Null);
                Assert.That(session.Observation.Transcript.Select(static entry => entry.Text),
                    Is.EqualTo(new[] { "こんにちは", "つづく" }));
                Assert.That(session.Observation.CurrentChoices.Select(static choice => choice.Text),
                    Is.EqualTo(new[] { "続ける", "終わる" }));
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
    public void Start_WithVariableAndArithmeticInstructions_WaitsForAdvanceWithComputedNarration()
    {
        var session = HeadlessVmTestHelper.CreateSession(CreateDocument(
            [
                new KlibInstruction(0, 0, KlibOpCode.PushInt, [41], new SourceLocation(1, 1), KlibMappingKind.Statement),
                new KlibInstruction(1, 5, KlibOpCode.DefVar, [0], new SourceLocation(1, 5), KlibMappingKind.Statement),
                new KlibInstruction(2, 10, KlibOpCode.LoadVar, [0], new SourceLocation(2, 1), KlibMappingKind.Statement),
                new KlibInstruction(3, 15, KlibOpCode.PushInt, [1], new SourceLocation(2, 5), KlibMappingKind.Statement),
                new KlibInstruction(4, 20, KlibOpCode.Add, [], new SourceLocation(2, 10), KlibMappingKind.Statement),
                new KlibInstruction(5, 21, KlibOpCode.StoreVar, [0], new SourceLocation(2, 15), KlibMappingKind.Statement),
                new KlibInstruction(6, 26, KlibOpCode.LoadVar, [0], new SourceLocation(3, 1), KlibMappingKind.Statement),
                new KlibInstruction(7, 31, KlibOpCode.SysCallVoid, [0, 1], new SourceLocation(3, 5), KlibMappingKind.Statement),
                new KlibInstruction(8, 40, KlibOpCode.End, [], new SourceLocation(4, 1), KlibMappingKind.Statement),
            ],
            [
                new KlibConstant(KlibConstantKind.String, StringValue: "scenario.nar"),
            ],
            [
                new KlibVariable(0, 1, KlibVariableType.Number, KlibScopeKind.Script, 0, null),
            ]));

        Assert.Multiple(() =>
        {
            Assert.That(session.State.Kind, Is.EqualTo(HeadlessVmStateKind.WaitingForAdvance));
            Assert.That(session.Observation.Transcript, Has.Count.EqualTo(1));
            Assert.That(session.Observation.Transcript[0].Text, Is.EqualTo("42"));
        });
    }

    [Test]
    public void Start_WithJumpFalseFalseValue_SkipsToTargetInstruction()
    {
        var session = HeadlessVmTestHelper.CreateSession(CreateDocument(
            [
                new KlibInstruction(0, 0, KlibOpCode.PushFalse, [], new SourceLocation(1, 1), KlibMappingKind.Statement),
                new KlibInstruction(1, 1, KlibOpCode.JumpFalse, [14], new SourceLocation(1, 5), KlibMappingKind.Statement),
                new KlibInstruction(2, 6, KlibOpCode.PushConst, [1], new SourceLocation(2, 1), KlibMappingKind.Statement),
                new KlibInstruction(3, 11, KlibOpCode.SysCallVoid, [0, 1], new SourceLocation(2, 5), KlibMappingKind.Statement),
                new KlibInstruction(4, 20, KlibOpCode.PushConst, [2], new SourceLocation(3, 1), KlibMappingKind.Statement),
                new KlibInstruction(5, 25, KlibOpCode.SysCallVoid, [0, 1], new SourceLocation(3, 5), KlibMappingKind.Statement),
                new KlibInstruction(6, 34, KlibOpCode.End, [], new SourceLocation(4, 1), KlibMappingKind.Statement),
            ],
            [
                new KlibConstant(KlibConstantKind.String, StringValue: "scenario.nar"),
                new KlibConstant(KlibConstantKind.String, StringValue: "skip"),
                new KlibConstant(KlibConstantKind.String, StringValue: "target"),
            ]));

        Assert.Multiple(() =>
        {
            Assert.That(session.State.Kind, Is.EqualTo(HeadlessVmStateKind.WaitingForAdvance));
            Assert.That(session.Observation.Transcript, Has.Count.EqualTo(1));
            Assert.That(session.Observation.Transcript[0].Text, Is.EqualTo("target"));
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

    private static KlibDocument CreateDocument(
        IReadOnlyList<KlibInstruction> instructions,
        IReadOnlyList<KlibConstant>? constants = null,
        IReadOnlyList<KlibVariable>? variables = null)
    {
        return HeadlessVmTestHelper.CreateSyntheticDocument(instructions, constants, variables);
    }
}
