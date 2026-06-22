using KoromoEventScript.Runtime.Core.Diagnostics;
using KoromoEventScript.Runtime.Core.Klib;
using KoromoEventScript.Runtime.Core.Persistence;

namespace KoromoEventScript.Runtime.Core.Execution;

public sealed class KesVmSession
{
    private readonly Dictionary<int, RuntimeValue> variables = [];
    private readonly List<RuntimeValue> operandStack = [];

    public KesVmSession(KlibDocument document)
    {
        Document = document;
        Position = new RuntimeExecutionPosition(
            document.Module.ScriptId,
            document.Instructions.Count > 0 ? document.Instructions[0].Index : 0,
            FilePath: null);
        Continuation = RuntimeContinuation.Running;
    }

    public KlibDocument Document { get; }

    public RuntimeExecutionPosition Position { get; private set; }

    public RuntimeContinuation Continuation { get; private set; }

    public IReadOnlyList<RuntimeValue> OperandStack => operandStack;

    public IReadOnlyDictionary<int, RuntimeValue> Variables => variables;

    public void SetInstructionIndex(int instructionIndex)
    {
        if (!IsKnownInstructionIndex(instructionIndex))
        {
            throw new ArgumentOutOfRangeException(nameof(instructionIndex), instructionIndex, "Instruction index does not exist in the current document.");
        }

        Position = Position with { InstructionIndex = instructionIndex };
    }

    public void PushOperand(RuntimeValue value)
    {
        operandStack.Add(value);
    }

    public void SetVariable(int stableId, RuntimeValue value)
    {
        variables[stableId] = value;
    }

    public RuntimeSaveSnapshot CaptureSnapshot()
    {
        return new RuntimeSaveSnapshot(
            SchemaVersion: 1,
            Position,
            Continuation,
            operandStack.ToArray(),
            variables
                .OrderBy(static pair => pair.Key)
                .Select(static pair => new RuntimeVariableSnapshot(pair.Key, pair.Value))
                .ToArray());
    }

    public RuntimeSessionRestoreResult Restore(RuntimeSaveSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.SchemaVersion != 1)
        {
            return RuntimeSessionRestoreResult.Failure(
                RuntimeFailureKind.Runtime,
                RuntimeDiagnostic.Error(
                    "KESR3000",
                    $"Unsupported runtime save snapshot schema version: {snapshot.SchemaVersion}.",
                    RuntimeFailureKind.Runtime));
        }

        if (!StringComparer.Ordinal.Equals(snapshot.Position.ScriptId, Document.Module.ScriptId))
        {
            return RuntimeSessionRestoreResult.Failure(
                RuntimeFailureKind.Runtime,
                RuntimeDiagnostic.Error(
                    "KESR3001",
                    $"Snapshot script id '{snapshot.Position.ScriptId}' does not match current script id '{Document.Module.ScriptId}'.",
                    RuntimeFailureKind.Runtime));
        }

        if (!IsKnownInstructionIndex(snapshot.Position.InstructionIndex))
        {
            return RuntimeSessionRestoreResult.Failure(
                RuntimeFailureKind.Runtime,
                RuntimeDiagnostic.Error(
                    "KESR3002",
                    $"Snapshot instruction index '{snapshot.Position.InstructionIndex}' does not exist in script '{Document.Module.ScriptId}'.",
                    RuntimeFailureKind.Runtime,
                    new RuntimeSourceLocation(Document.Module.ScriptId, snapshot.Position.InstructionIndex, null, null, null)));
        }

        Position = snapshot.Position;
        Continuation = snapshot.Continuation;
        operandStack.Clear();
        operandStack.AddRange(snapshot.OperandStack);
        variables.Clear();
        foreach (var variable in snapshot.Variables)
        {
            variables[variable.StableId] = variable.Value;
        }

        return RuntimeSessionRestoreResult.Success();
    }

    private bool IsKnownInstructionIndex(int instructionIndex)
    {
        return Document.Instructions.Any(instruction => instruction.Index == instructionIndex);
    }
}

public sealed record RuntimeSessionRestoreResult(
    bool Succeeded,
    IReadOnlyList<RuntimeDiagnostic> Diagnostics,
    RuntimeFailureKind FailureKind)
{
    public static RuntimeSessionRestoreResult Success()
    {
        return new RuntimeSessionRestoreResult(true, [], RuntimeFailureKind.None);
    }

    public static RuntimeSessionRestoreResult Failure(RuntimeFailureKind failureKind, params RuntimeDiagnostic[] diagnostics)
    {
        return new RuntimeSessionRestoreResult(false, diagnostics, failureKind);
    }
}

public readonly record struct RuntimeExecutionPosition(
    string ScriptId,
    int InstructionIndex,
    string? FilePath);

public enum RuntimeContinuationKind
{
    Running = 0,
    WaitingForAdvance = 1,
    WaitingForSelection = 2,
    Completed = 3,
}

public sealed record RuntimeContinuation(
    RuntimeContinuationKind Kind,
    int? ResumeInstructionIndex,
    IReadOnlyList<int> PendingChoiceOffsets)
{
    public static RuntimeContinuation Running { get; } = new(RuntimeContinuationKind.Running, null, []);
}

public enum RuntimeValueKind
{
    Null = 0,
    Number = 1,
    Bool = 2,
    String = 3,
    Reference = 4,
}

public sealed record RuntimeValue(
    RuntimeValueKind Kind,
    double? NumberValue = null,
    bool? BoolValue = null,
    string? StringValue = null,
    string? ReferenceId = null)
{
    public static RuntimeValue Null { get; } = new(RuntimeValueKind.Null);

    public static RuntimeValue Number(double value)
    {
        return new RuntimeValue(RuntimeValueKind.Number, NumberValue: value);
    }

    public static RuntimeValue Bool(bool value)
    {
        return new RuntimeValue(RuntimeValueKind.Bool, BoolValue: value);
    }

    public static RuntimeValue String(string value)
    {
        return new RuntimeValue(RuntimeValueKind.String, StringValue: value);
    }

    public static RuntimeValue Reference(string referenceId)
    {
        return new RuntimeValue(RuntimeValueKind.Reference, ReferenceId: referenceId);
    }
}
