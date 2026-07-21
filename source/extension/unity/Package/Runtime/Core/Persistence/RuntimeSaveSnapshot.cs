#nullable enable

using System.Collections.Generic;
using KoromoEventScript.Runtime.Core.Execution;

namespace KoromoEventScript.Runtime.Core.Persistence
{

public sealed record RuntimeSaveSnapshot(
    int SchemaVersion,
    RuntimeExecutionPosition Position,
    RuntimeContinuation Continuation,
    IReadOnlyList<RuntimeValue> OperandStack,
    IReadOnlyList<RuntimeVariableSnapshot> Variables,
    IReadOnlyList<RuntimeCallFrameSnapshot>? CallFrames = null)
{
    public const int CurrentSchemaVersion = 2;
}

public sealed record RuntimeVariableSnapshot(
    int StableId,
    RuntimeValue Value);

public sealed record RuntimeSavedVariableSnapshot(
    int Slot,
    bool WasInitialized,
    RuntimeValue Value);

public sealed record RuntimeCallFrameSnapshot(
    int FunctionIndex,
    int? ReturnInstructionIndex,
    bool ExpectsReturnValue,
    IReadOnlyList<RuntimeSavedVariableSnapshot> SavedVariables);
}
