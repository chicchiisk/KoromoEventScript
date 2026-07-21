#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using KoromoEventScript.Runtime.Core.Diagnostics;
using KoromoEventScript.Runtime.Core.Klib;
using KoromoEventScript.Runtime.Core.Persistence;

namespace KoromoEventScript.Runtime.Core.Execution
{

public sealed class KesVmSession
{
    private readonly Dictionary<int, int> instructionOrdinals;
    private readonly Dictionary<int, int> instructionOffsetOrdinals;
    private readonly List<RuntimeValue> operandStack = new List<RuntimeValue>();
    private readonly Stack<RuntimeCallFrame> callFrames = new Stack<RuntimeCallFrame>();
    private readonly RuntimeVariableView variableView;
    private RuntimeValue[] variables;
    private bool[] initializedVariables;
    private int initializedVariableCount;
    private int currentInstructionOrdinal;

    public KesVmSession(KlibDocument document)
    {
        Document = document;
        instructionOrdinals = new Dictionary<int, int>(document.Instructions.Count);
        instructionOffsetOrdinals = new Dictionary<int, int>(document.Instructions.Count);
        for (var ordinal = 0; ordinal < document.Instructions.Count; ordinal++)
        {
            instructionOrdinals.Add(document.Instructions[ordinal].Index, ordinal);
            instructionOffsetOrdinals.Add(document.Instructions[ordinal].Offset, ordinal);
        }

        currentInstructionOrdinal = document.Instructions.Count > 0 ? 0 : -1;
        var initialVariableCapacity = Math.Max(document.Variables.Count, 8);
        variables = new RuntimeValue[initialVariableCapacity];
        initializedVariables = new bool[initialVariableCapacity];
        variableView = new RuntimeVariableView(this);
        Position = new RuntimeExecutionPosition(
            document.Module.ScriptId,
            document.Instructions.Count > 0 ? document.Instructions[0].Index : 0,
            FilePath: null);
        Continuation = RuntimeContinuation.Running;
    }

    public KlibDocument Document { get; }

    internal RuntimeObjectStore ObjectStore { get; } = new();

    public RuntimeExecutionPosition Position { get; private set; }

    public RuntimeContinuation Continuation { get; private set; }

    public IReadOnlyList<RuntimeValue> OperandStack => operandStack;

    public IReadOnlyDictionary<int, RuntimeValue> Variables => variableView;

    public void SetInstructionIndex(int instructionIndex)
    {
        if (!IsKnownInstructionIndex(instructionIndex))
        {
            throw new ArgumentOutOfRangeException(nameof(instructionIndex), instructionIndex, "Instruction index does not exist in the current document.");
        }

        currentInstructionOrdinal = instructionOrdinals[instructionIndex];
        Position = Position.WithInstructionIndex(instructionIndex);
    }

    internal bool TrySetInstructionOffset(int instructionOffset)
    {
        if (!instructionOffsetOrdinals.TryGetValue(instructionOffset, out var ordinal))
        {
            return false;
        }

        currentInstructionOrdinal = ordinal;
        Position = Position.WithInstructionIndex(Document.Instructions[ordinal].Index);
        return true;
    }

    public void PushOperand(RuntimeValue value)
    {
        operandStack.Add(value);
    }

    internal bool TryPopOperand(out RuntimeValue value)
    {
        if (operandStack.Count == 0)
        {
            value = RuntimeValue.Null;
            return false;
        }

        var lastIndex = operandStack.Count - 1;
        value = operandStack[lastIndex];
        operandStack.RemoveAt(lastIndex);
        return true;
    }

    internal bool TryPeekOperand(out RuntimeValue value)
    {
        if (operandStack.Count == 0)
        {
            value = RuntimeValue.Null;
            return false;
        }

        value = operandStack[^1];
        return true;
    }

    public void SetVariable(int stableId, RuntimeValue value)
    {
        if (stableId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stableId), stableId, "Variable stable id must be non-negative.");
        }

        EnsureVariableCapacity(stableId);
        variables[stableId] = value;
        if (!initializedVariables[stableId])
        {
            initializedVariables[stableId] = true;
            initializedVariableCount++;
        }
    }

    internal bool TryGetVariable(int stableId, out RuntimeValue value)
    {
        if (stableId >= 0 &&
            stableId < initializedVariables.Length &&
            initializedVariables[stableId])
        {
            value = variables[stableId];
            return true;
        }

        value = RuntimeValue.Null;
        return false;
    }

    internal bool IsVariableInitialized(int stableId)
    {
        return stableId >= 0 &&
            stableId < initializedVariables.Length &&
            initializedVariables[stableId];
    }

    internal void ClearVariable(int stableId)
    {
        if (!IsVariableInitialized(stableId))
        {
            return;
        }

        variables[stableId] = RuntimeValue.Null;
        initializedVariables[stableId] = false;
        initializedVariableCount--;
    }

    internal void PushCallFrame(RuntimeCallFrame frame)
    {
        callFrames.Push(frame);
    }

    internal bool TryPopCallFrame(out RuntimeCallFrame frame)
    {
        if (callFrames.Count == 0)
        {
            frame = null!;
            return false;
        }

        frame = callFrames.Pop();
        return true;
    }

    internal int CallFrameDepth => callFrames.Count;

    public RuntimeSaveSnapshot CaptureSnapshot()
    {
        return CreateSnapshot(Position, Continuation);
    }

    public RuntimeSaveSnapshot CaptureSnapshotAfterHostOperation()
    {
        if (Continuation.Kind != RuntimeContinuationKind.WaitingForHost)
        {
            return CaptureSnapshot();
        }

        if (Continuation.ResumeInstructionIndex is int resumeInstructionIndex)
        {
            return CreateSnapshot(
                Position.WithInstructionIndex(resumeInstructionIndex),
                RuntimeContinuation.Running);
        }

        return CreateSnapshot(Position, RuntimeContinuation.Completed);
    }

    private RuntimeSaveSnapshot CreateSnapshot(
        RuntimeExecutionPosition position,
        RuntimeContinuation continuation)
    {
        return new RuntimeSaveSnapshot(
            SchemaVersion: RuntimeSaveSnapshot.CurrentSchemaVersion,
            position,
            continuation,
            operandStack.ToArray(),
            CaptureVariables(),
            CaptureCallFrames());
    }

    public RuntimeSessionRestoreResult Restore(RuntimeSaveSnapshot snapshot)
    {
        if (snapshot == null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        if (snapshot.SchemaVersion is not (1 or RuntimeSaveSnapshot.CurrentSchemaVersion))
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

        currentInstructionOrdinal = instructionOrdinals[snapshot.Position.InstructionIndex];
        Position = snapshot.Position;
        Continuation = snapshot.Continuation;
        operandStack.Clear();
        operandStack.AddRange(snapshot.OperandStack);
        Array.Clear(initializedVariables, 0, initializedVariables.Length);
        initializedVariableCount = 0;
        foreach (var variable in snapshot.Variables)
        {
            SetVariable(variable.StableId, variable.Value);
        }

        callFrames.Clear();
        if (snapshot.SchemaVersion >= 2)
        {
            foreach (var frame in snapshot.CallFrames ?? Array.Empty<RuntimeCallFrameSnapshot>())
            {
                if (frame.FunctionIndex < 0 ||
                    frame.FunctionIndex >= (Document.Functions?.Count ?? 0) ||
                    (frame.ReturnInstructionIndex is int returnIndex && !IsKnownInstructionIndex(returnIndex)))
                {
                    callFrames.Clear();
                    return RuntimeSessionRestoreResult.Failure(
                        RuntimeFailureKind.Runtime,
                        RuntimeDiagnostic.Error(
                            "KESR3004",
                            "Snapshot contains an invalid function call frame.",
                            RuntimeFailureKind.Runtime));
                }

                callFrames.Push(new RuntimeCallFrame(
                    frame.FunctionIndex,
                    frame.ReturnInstructionIndex,
                    frame.ExpectsReturnValue,
                    frame.SavedVariables
                        .Select(static saved => new RuntimeSavedVariable(
                            saved.Slot,
                            saved.WasInitialized,
                            saved.Value))
                        .ToArray()));
            }
        }

        return RuntimeSessionRestoreResult.Success();
    }

    public RuntimeSessionRestoreResult ResumeHostOperation()
    {
        if (Continuation.Kind != RuntimeContinuationKind.WaitingForHost)
        {
            return RuntimeSessionRestoreResult.Failure(
                RuntimeFailureKind.Runtime,
                RuntimeDiagnostic.Error(
                    "KESR3003",
                    "The runtime session is not waiting for a host operation.",
                    RuntimeFailureKind.Runtime));
        }

        if (Continuation.ResumeInstructionIndex is int resumeInstructionIndex)
        {
            SetInstructionIndex(resumeInstructionIndex);
            Continuation = RuntimeContinuation.Running;
        }
        else
        {
            Continuation = RuntimeContinuation.Completed;
        }

        return RuntimeSessionRestoreResult.Success();
    }

    public void Fault()
    {
        Continuation = RuntimeContinuation.Faulted;
    }

    public void Stop()
    {
        Continuation = RuntimeContinuation.Stopped;
    }

    private bool IsKnownInstructionIndex(int instructionIndex)
    {
        return instructionOrdinals.ContainsKey(instructionIndex);
    }

    internal KlibInstruction? CurrentInstruction()
    {
        return currentInstructionOrdinal >= 0 &&
            currentInstructionOrdinal < Document.Instructions.Count
            ? Document.Instructions[currentInstructionOrdinal]
            : null;
    }

    internal void AdvanceAfter(KlibInstruction instruction)
    {
        if (!instructionOrdinals.TryGetValue(instruction.Index, out var instructionOrdinal) ||
            instructionOrdinal + 1 >= Document.Instructions.Count)
        {
            Continuation = RuntimeContinuation.Completed;
            return;
        }

        currentInstructionOrdinal = instructionOrdinal + 1;
        Position = Position.WithInstructionIndex(Document.Instructions[currentInstructionOrdinal].Index);
    }

    internal int? GetNextInstructionIndex(KlibInstruction instruction)
    {
        return instructionOrdinals.TryGetValue(instruction.Index, out var instructionOrdinal) &&
            instructionOrdinal + 1 < Document.Instructions.Count
            ? Document.Instructions[instructionOrdinal + 1].Index
            : null;
    }

    internal void SetContinuation(RuntimeContinuation continuation)
    {
        Continuation = continuation;
    }

    private void EnsureVariableCapacity(int stableId)
    {
        if (stableId < variables.Length)
        {
            return;
        }

        var newLength = variables.Length;
        while (newLength <= stableId)
        {
            newLength = checked(newLength * 2);
        }

        Array.Resize(ref variables, newLength);
        Array.Resize(ref initializedVariables, newLength);
    }

    private RuntimeVariableSnapshot[] CaptureVariables()
    {
        var snapshots = new RuntimeVariableSnapshot[initializedVariableCount];
        var destination = 0;
        for (var stableId = 0; stableId < initializedVariables.Length; stableId++)
        {
            if (initializedVariables[stableId])
            {
                snapshots[destination++] = new RuntimeVariableSnapshot(stableId, variables[stableId]);
            }
        }

        return snapshots;
    }

    private RuntimeCallFrameSnapshot[] CaptureCallFrames()
    {
        return callFrames
            .Reverse()
            .Select(static frame => new RuntimeCallFrameSnapshot(
                frame.FunctionIndex,
                frame.ReturnInstructionIndex,
                frame.ExpectsReturnValue,
                frame.SavedVariables
                    .Select(static saved => new RuntimeSavedVariableSnapshot(
                        saved.Slot,
                        saved.WasInitialized,
                        saved.Value))
                    .ToArray()))
            .ToArray();
    }

    private sealed class RuntimeVariableView : IReadOnlyDictionary<int, RuntimeValue>
    {
        private readonly KesVmSession session;

        public RuntimeVariableView(KesVmSession session)
        {
            this.session = session;
        }

        public int Count => session.initializedVariableCount;

        public IEnumerable<int> Keys
        {
            get
            {
                for (var stableId = 0; stableId < session.initializedVariables.Length; stableId++)
                {
                    if (session.initializedVariables[stableId])
                    {
                        yield return stableId;
                    }
                }
            }
        }

        public IEnumerable<RuntimeValue> Values
        {
            get
            {
                for (var stableId = 0; stableId < session.initializedVariables.Length; stableId++)
                {
                    if (session.initializedVariables[stableId])
                    {
                        yield return session.variables[stableId];
                    }
                }
            }
        }

        public RuntimeValue this[int key]
        {
            get
            {
                if (TryGetValue(key, out var value))
                {
                    return value;
                }

                throw new KeyNotFoundException($"Variable stable id '{key}' is not initialized.");
            }
        }

        public bool ContainsKey(int key)
        {
            return key >= 0 &&
                key < session.initializedVariables.Length &&
                session.initializedVariables[key];
        }

        public bool TryGetValue(int key, out RuntimeValue value)
        {
            return session.TryGetVariable(key, out value);
        }

        public IEnumerator<KeyValuePair<int, RuntimeValue>> GetEnumerator()
        {
            for (var stableId = 0; stableId < session.initializedVariables.Length; stableId++)
            {
                if (session.initializedVariables[stableId])
                {
                    yield return new KeyValuePair<int, RuntimeValue>(
                        stableId,
                        session.variables[stableId]);
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}

internal sealed record RuntimeSavedVariable(
    int Slot,
    bool WasInitialized,
    RuntimeValue Value);

internal sealed record RuntimeCallFrame(
    int FunctionIndex,
    int? ReturnInstructionIndex,
    bool ExpectsReturnValue,
    IReadOnlyList<RuntimeSavedVariable> SavedVariables);

public sealed record RuntimeSessionRestoreResult(
    bool Succeeded,
    IReadOnlyList<RuntimeDiagnostic> Diagnostics,
    RuntimeFailureKind FailureKind)
{
    public static RuntimeSessionRestoreResult Success()
    {
        return new RuntimeSessionRestoreResult(true, Array.Empty<RuntimeDiagnostic>(), RuntimeFailureKind.None);
    }

    public static RuntimeSessionRestoreResult Failure(RuntimeFailureKind failureKind, params RuntimeDiagnostic[] diagnostics)
    {
        return new RuntimeSessionRestoreResult(false, diagnostics, failureKind);
    }
}

public readonly struct RuntimeExecutionPosition
{
    [JsonConstructor]
    public RuntimeExecutionPosition(string scriptId, int instructionIndex, string? FilePath)
    {
        ScriptId = scriptId;
        InstructionIndex = instructionIndex;
        this.FilePath = FilePath;
    }

    public string ScriptId { get; }

    public int InstructionIndex { get; }

    public string? FilePath { get; }

    public RuntimeExecutionPosition WithInstructionIndex(int instructionIndex)
    {
        return new RuntimeExecutionPosition(ScriptId, instructionIndex, FilePath);
    }
}

public enum RuntimeContinuationKind
{
    Running = 0,
    WaitingForAdvance = 1,
    WaitingForSelection = 2,
    WaitingForHost = 3,
    Completed = 4,
    Faulted = 5,
    Stopped = 6,
}

public sealed record RuntimeContinuation(
    RuntimeContinuationKind Kind,
    int? ResumeInstructionIndex,
    IReadOnlyList<int> PendingChoiceOffsets,
    string? Prompt,
    IReadOnlyList<RuntimeSelectionChoice> PendingChoices)
{
    public static RuntimeContinuation Running { get; } = new(RuntimeContinuationKind.Running, null, Array.Empty<int>(), null, Array.Empty<RuntimeSelectionChoice>());

    public static RuntimeContinuation Completed { get; } = new(RuntimeContinuationKind.Completed, null, Array.Empty<int>(), null, Array.Empty<RuntimeSelectionChoice>());

    public static RuntimeContinuation Faulted { get; } = new(RuntimeContinuationKind.Faulted, null, Array.Empty<int>(), null, Array.Empty<RuntimeSelectionChoice>());

    public static RuntimeContinuation Stopped { get; } = new(RuntimeContinuationKind.Stopped, null, Array.Empty<int>(), null, Array.Empty<RuntimeSelectionChoice>());
}

public sealed record RuntimeSelectionChoice(
    string Text,
    int TargetInstructionIndex);

public enum RuntimeValueKind
{
    Null = 0,
    Number = 1,
    Bool = 2,
    String = 3,
    Reference = 4,
}

public enum RuntimeReferenceKind
{
    External = 0,
    Array = 1,
    Instance = 2,
}

public readonly struct RuntimeValue : IEquatable<RuntimeValue>
{
    [JsonConstructor]
    public RuntimeValue(
        RuntimeValueKind Kind,
        double? NumberValue = null,
        bool? BoolValue = null,
        string? StringValue = null,
        string? ReferenceId = null,
        RuntimeReferenceKind ReferenceKind = RuntimeReferenceKind.External,
        int ObjectHandle = -1)
    {
        this.Kind = Kind;
        this.NumberValue = NumberValue;
        this.BoolValue = BoolValue;
        this.StringValue = StringValue;
        this.ReferenceId = ReferenceId;
        this.ReferenceKind = ReferenceKind;
        this.ObjectHandle = ObjectHandle;
    }

    public RuntimeValueKind Kind { get; }

    public double? NumberValue { get; }

    public bool? BoolValue { get; }

    public string? StringValue { get; }

    public string? ReferenceId { get; }

    public RuntimeReferenceKind ReferenceKind { get; }

    public int ObjectHandle { get; }

    public static RuntimeValue Null { get; } = new RuntimeValue(RuntimeValueKind.Null);

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

    public static RuntimeValue ObjectReference(RuntimeReferenceKind referenceKind, int objectHandle)
    {
        if (referenceKind == RuntimeReferenceKind.External)
        {
            throw new ArgumentOutOfRangeException(nameof(referenceKind), "An object reference must have an internal reference kind.");
        }

        if (objectHandle < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(objectHandle), "Object handle must be non-negative.");
        }

        return new RuntimeValue(
            RuntimeValueKind.Reference,
            ReferenceKind: referenceKind,
            ObjectHandle: objectHandle);
    }

    public object? ToObject()
    {
        return Kind switch
        {
            RuntimeValueKind.Null => null,
            RuntimeValueKind.Number => NumberValue,
            RuntimeValueKind.Bool => BoolValue,
            RuntimeValueKind.String => StringValue,
            RuntimeValueKind.Reference => ReferenceId,
            _ => null,
        };
    }

    public bool Equals(RuntimeValue other)
    {
        return Kind == other.Kind &&
            NumberValue == other.NumberValue &&
            BoolValue == other.BoolValue &&
            StringComparer.Ordinal.Equals(StringValue, other.StringValue) &&
            StringComparer.Ordinal.Equals(ReferenceId, other.ReferenceId) &&
            ReferenceKind == other.ReferenceKind &&
            ObjectHandle == other.ObjectHandle;
    }

    public override bool Equals(object? obj)
    {
        return obj is RuntimeValue other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = (int)Kind;
            hashCode = (hashCode * 397) ^ NumberValue.GetHashCode();
            hashCode = (hashCode * 397) ^ BoolValue.GetHashCode();
            hashCode = (hashCode * 397) ^ (StringValue == null ? 0 : StringComparer.Ordinal.GetHashCode(StringValue));
            hashCode = (hashCode * 397) ^ (ReferenceId == null ? 0 : StringComparer.Ordinal.GetHashCode(ReferenceId));
            hashCode = (hashCode * 397) ^ (int)ReferenceKind;
            hashCode = (hashCode * 397) ^ ObjectHandle;
            return hashCode;
        }
    }

    public static bool operator ==(RuntimeValue left, RuntimeValue right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(RuntimeValue left, RuntimeValue right)
    {
        return !left.Equals(right);
    }
}
}
