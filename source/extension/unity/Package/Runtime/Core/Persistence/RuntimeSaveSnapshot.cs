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
    IReadOnlyList<RuntimeVariableSnapshot> Variables);

public sealed record RuntimeVariableSnapshot(
    int StableId,
    RuntimeValue Value);
}
