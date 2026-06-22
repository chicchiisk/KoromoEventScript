using KoromoEventScript.Runtime.Core.Diagnostics;
using KoromoEventScript.Runtime.Core.Execution;
using KoromoEventScript.Runtime.Core.Klib;
using KoromoEventScript.Runtime.Core.Persistence;

namespace KoromoEventScript.Runtime.Core.Tests.Execution;

public sealed class KesVmSessionTests
{
    [Test]
    public void CaptureSnapshot_IncludesStableScriptIdAndInstructionIndex()
    {
        var document = CreateDocument();
        var session = new KesVmSession(document);
        session.SetInstructionIndex(1);

        var snapshot = session.CaptureSnapshot();

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.SchemaVersion, Is.EqualTo(1));
            Assert.That(snapshot.Position.ScriptId, Is.EqualTo("chapter001"));
            Assert.That(snapshot.Position.InstructionIndex, Is.EqualTo(1));
            Assert.That(snapshot.Position.FilePath, Is.Null);
        });
    }

    [Test]
    public void Restore_WithCapturedSnapshot_RestoresPositionOperandsAndVariables()
    {
        var document = CreateDocument();
        var original = new KesVmSession(document);
        original.SetInstructionIndex(1);
        original.PushOperand(RuntimeValue.Number(42));
        original.SetVariable(7, RuntimeValue.String("saved"));
        var snapshot = original.CaptureSnapshot();
        var restored = new KesVmSession(document);

        var result = restored.Restore(snapshot);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(restored.Position.InstructionIndex, Is.EqualTo(1));
            Assert.That(restored.OperandStack.Single().NumberValue, Is.EqualTo(42));
            Assert.That(restored.Variables[7].StringValue, Is.EqualTo("saved"));
        });
    }

    [Test]
    public void Restore_WithUnknownInstructionIndex_ReturnsRuntimeError()
    {
        var document = CreateDocument();
        var session = new KesVmSession(document);
        var snapshot = new RuntimeSaveSnapshot(
            SchemaVersion: 1,
            Position: new RuntimeExecutionPosition("chapter001", 999, FilePath: null),
            Continuation: RuntimeContinuation.Running,
            OperandStack: [],
            Variables: []);

        var result = session.Restore(snapshot);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureKind, Is.EqualTo(RuntimeFailureKind.Runtime));
            Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Code), Does.Contain("KESR3002"));
        });
    }

    [Test]
    public void Restore_WithDifferentScriptId_ReturnsRuntimeError()
    {
        var document = CreateDocument();
        var session = new KesVmSession(document);
        var snapshot = new RuntimeSaveSnapshot(
            SchemaVersion: 1,
            Position: new RuntimeExecutionPosition("chapter999", 0, FilePath: null),
            Continuation: RuntimeContinuation.Running,
            OperandStack: [],
            Variables: []);

        var result = session.Restore(snapshot);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureKind, Is.EqualTo(RuntimeFailureKind.Runtime));
            Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Code), Does.Contain("KESR3001"));
        });
    }

    private static KlibDocument CreateDocument()
    {
        return new KlibDocument(
            new KlibVersion(1, 0, 0),
            new KlibModuleInfo("chapter001", "chapter001.module", "events/chapter001.kc", EntryLabel: null),
            [],
            [],
            [],
            [
                new KlibInstruction(0, 0, KlibOpCode.PushInt, [1], Source: null, KlibMappingKind.Statement),
                new KlibInstruction(1, 1, KlibOpCode.End, [], Source: null, KlibMappingKind.Statement),
            ],
            [],
            new KlibDebugInfo(null, null, []));
    }
}
