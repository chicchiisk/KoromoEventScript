using KoromoEventScript.Runtime.Core.Diagnostics;
using KoromoEventScript.Runtime.Core.Execution;
using KoromoEventScript.Runtime.Core.Klib;
using KoromoEventScript.Runtime.Core.Persistence;
using System.Text.Json;

namespace KoromoEventScript.Runtime.Core.Tests.Execution;

public sealed class KesVmSessionTests
{
    [Test]
    public void Variables_SupportSparseStableIdsThroughArrayBackedView()
    {
        var session = new KesVmSession(CreateDocument());

        session.SetVariable(1_024, RuntimeValue.Number(42));
        session.SetVariable(2, RuntimeValue.String("two"));
        session.SetVariable(1_024, RuntimeValue.Number(43));
        var found = session.Variables.TryGetValue(2, out var value);

        Assert.Multiple(() =>
        {
            Assert.That(session.Variables.Count, Is.EqualTo(2));
            Assert.That(session.Variables[1_024].NumberValue, Is.EqualTo(43));
            Assert.That(found, Is.True);
            Assert.That(value.StringValue, Is.EqualTo("two"));
            Assert.That(session.Variables.Keys, Is.EqualTo(new[] { 2, 1_024 }));
        });
    }

    [Test]
    public void RuntimeValue_IsValueTypeWithStableEqualityAndJsonRoundTrip()
    {
        var original = RuntimeValue.Reference("actor:alice");
        var copy = original;
        var number = RuntimeValue.Number(42);
        var equalNumber = RuntimeValue.Number(42);
        var differentNumber = RuntimeValue.Number(43);
        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<RuntimeValue>(json);

        Assert.Multiple(() =>
        {
            Assert.That(typeof(RuntimeValue).IsValueType, Is.True);
            Assert.That(copy, Is.EqualTo(original));
            Assert.That(restored, Is.EqualTo(original));
            Assert.That(number, Is.EqualTo(equalNumber));
            Assert.That(number, Is.Not.EqualTo(differentNumber));
        });
    }

    [Test]
    public void Snapshot_RestoresActiveFunctionFrameAndLocalVariables()
    {
        var instructions = new[]
        {
            new KlibInstruction(0, 0, KlibOpCode.PushInt, [6], null, KlibMappingKind.Statement),
            new KlibInstruction(1, 1, KlibOpCode.CallFunction, [0, 1], null, KlibMappingKind.Statement),
            new KlibInstruction(2, 2, KlibOpCode.DefVar, [0], null, KlibMappingKind.Statement),
            new KlibInstruction(3, 3, KlibOpCode.End, [], null, KlibMappingKind.Statement),
            new KlibInstruction(4, 4, KlibOpCode.Label, [0, 0], null, KlibMappingKind.Statement),
            new KlibInstruction(5, 5, KlibOpCode.LoadVar, [1], null, KlibMappingKind.Statement),
            new KlibInstruction(6, 6, KlibOpCode.PushInt, [2], null, KlibMappingKind.Statement),
            new KlibInstruction(7, 7, KlibOpCode.Mul, [], null, KlibMappingKind.Statement),
            new KlibInstruction(8, 8, KlibOpCode.ReturnValue, [], null, KlibMappingKind.Statement),
        };
        var document = new KlibDocument(
            new KlibVersion(1, 1, 0),
            new KlibModuleInfo("function-save", "function-save.module", "events/function-save.kc", null),
            [],
            [new KlibConstant(KlibConstantKind.String, StringValue: "double")],
            [],
            instructions,
            [],
            new KlibDebugInfo(null, null, []),
            [new KlibFunction(0, 4, [1], [1], ReturnsValue: true)]);
        var original = new KesVmSession(document);
        var partial = new KesVmExecutor().Run(original, maxInstructionCount: 2);

        var snapshot = original.CaptureSnapshot();
        var restored = new KesVmSession(document);
        var restoreResult = restored.Restore(snapshot);
        var completed = new KesVmExecutor().Run(restored);

        Assert.Multiple(() =>
        {
            Assert.That(partial.Succeeded, Is.False);
            Assert.That(snapshot.SchemaVersion, Is.EqualTo(2));
            Assert.That(snapshot.CallFrames, Has.Count.EqualTo(1));
            Assert.That(snapshot.CallFrames![0].FunctionIndex, Is.Zero);
            Assert.That(snapshot.CallFrames[0].ReturnInstructionIndex, Is.EqualTo(2));
            Assert.That(restoreResult.Succeeded, Is.True);
            Assert.That(completed.Succeeded, Is.True);
            Assert.That(restored.Variables[0].NumberValue, Is.EqualTo(12));
            Assert.That(restored.Continuation.Kind, Is.EqualTo(RuntimeContinuationKind.Completed));
        });
    }
    [Test]
    public void CaptureSnapshot_IncludesStableScriptIdAndInstructionIndex()
    {
        var document = CreateDocument();
        var session = new KesVmSession(document);
        session.SetInstructionIndex(1);

        var snapshot = session.CaptureSnapshot();

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.SchemaVersion, Is.EqualTo(RuntimeSaveSnapshot.CurrentSchemaVersion));
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

    [Test]
    public void ResumeHostOperation_RestoresRunningContinuationExactlyOnce()
    {
        var session = new KesVmSession(CreateDocument());
        var restored = session.Restore(new RuntimeSaveSnapshot(
            1,
            new RuntimeExecutionPosition("chapter001", 1, null),
            new RuntimeContinuation(
                RuntimeContinuationKind.WaitingForHost,
                1,
                [],
                "scene.bg",
                []),
            [],
            []));

        var first = session.ResumeHostOperation();
        var second = session.ResumeHostOperation();

        Assert.Multiple(() =>
        {
            Assert.That(restored.Succeeded, Is.True);
            Assert.That(first.Succeeded, Is.True);
            Assert.That(session.Continuation.Kind, Is.EqualTo(RuntimeContinuationKind.Running));
            Assert.That(second.Succeeded, Is.False);
            Assert.That(second.Diagnostics.Single().Code, Is.EqualTo("KESR3003"));
        });
    }

    [Test]
    public void CaptureSnapshotAfterHostOperation_StoresPostOperationPositionAndRunningState()
    {
        var session = new KesVmSession(CreateDocument());
        var restored = session.Restore(new RuntimeSaveSnapshot(
            1,
            new RuntimeExecutionPosition("chapter001", 0, null),
            new RuntimeContinuation(
                RuntimeContinuationKind.WaitingForHost,
                1,
                [],
                "state.save",
                []),
            [RuntimeValue.Number(7)],
            [new RuntimeVariableSnapshot(3, RuntimeValue.String("saved"))]));

        var snapshot = session.CaptureSnapshotAfterHostOperation();

        Assert.Multiple(() =>
        {
            Assert.That(restored.Succeeded, Is.True);
            Assert.That(snapshot.Position.InstructionIndex, Is.EqualTo(1));
            Assert.That(snapshot.Continuation.Kind, Is.EqualTo(RuntimeContinuationKind.Running));
            Assert.That(snapshot.OperandStack.Single().NumberValue, Is.EqualTo(7));
            Assert.That(snapshot.Variables.Single().Value.StringValue, Is.EqualTo("saved"));
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
