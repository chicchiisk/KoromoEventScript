using KoromoEventScript.Cli.Compilation;

namespace KoromoEventScript.Cli.Execution;

public sealed class HeadlessVmSaveStateMapper
{
    public HeadlessVmSaveState Export(HeadlessVmSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var document = session.Document ?? throw new InvalidOperationException("The headless VM session has not been started.");
        if (session.State.Kind == HeadlessVmStateKind.Faulted)
        {
            throw new InvalidOperationException("Cannot export a faulted headless VM session.");
        }

        var variableStates = document.Variables
            .Where(variable => session.RuntimeState.VariableValues.ContainsKey(variable.StableIdIndex))
            .Select(variable => new HeadlessVmVariableSnapshot(
                variable.StableIdIndex,
                variable.ScopeKind,
                variable.ScopeId,
                ToSnapshotValue(session.RuntimeState.VariableValues[variable.StableIdIndex])))
            .ToArray();

        var continuation = session.State.Kind switch
        {
            HeadlessVmStateKind.Running => HeadlessVmContinuationState.Running(session.State.InstructionOffset),
            HeadlessVmStateKind.WaitingForAdvance => HeadlessVmContinuationState.WaitingForAdvance(session.State.InstructionOffset),
            HeadlessVmStateKind.WaitingForSelection => HeadlessVmContinuationState.WaitingForSelection(
                session.State.InstructionOffset,
                session.Observation.CurrentPrompt,
                session.State.PendingChoices?.Select(static choice => new HeadlessVmChoiceSnapshot(choice.Text, choice.TargetOffset)).ToArray() ?? []),
            HeadlessVmStateKind.Completed => HeadlessVmContinuationState.Completed(session.State.InstructionOffset),
            _ => throw new InvalidOperationException($"Cannot export session state '{session.State.Kind}'."),
        };

        return new HeadlessVmSaveState(
            HeadlessVmSaveState.CurrentSchemaVersion,
            new HeadlessVmExecutionPosition(document.Module.ScriptId, session.State.InstructionOffset),
            variableStates,
            session.RuntimeState.ExportOperands().Select(ToSnapshotValue).ToArray(),
            session.RuntimeState.CallFrames.ToArray(),
            session.RuntimeState.ObjectStore.ExportSnapshots(),
            continuation);
    }

    public void Restore(HeadlessVmSession session, KlibDocument document, HeadlessVmSaveState snapshot)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!string.Equals(snapshot.Position.ScriptId, document.Module.ScriptId, StringComparison.Ordinal))
        {
            session.RestoreFault($"Snapshot script '{snapshot.Position.ScriptId}' does not match document script '{document.Module.ScriptId}'.", document.Module.ScriptId, snapshot.Position.InstructionOffset);
            return;
        }

        if (!IsKnownOffset(document, snapshot.Position.InstructionOffset))
        {
            session.RestoreFault($"Snapshot instruction offset '{snapshot.Position.InstructionOffset}' does not exist.", document.Module.ScriptId, snapshot.Position.InstructionOffset);
            return;
        }

        if (snapshot.Continuation.Kind == HeadlessVmContinuationKind.WaitingForSelection && snapshot.Continuation.PendingChoices is null)
        {
            session.RestoreFault("Snapshot selection continuation is missing pending choices.", document.Module.ScriptId, snapshot.Position.InstructionOffset);
            return;
        }

        session.RestoreSession(document, snapshot, BuildRuntimeState(snapshot));
    }

    private static HeadlessVmRuntimeState BuildRuntimeState(HeadlessVmSaveState snapshot)
    {
        var runtimeState = new HeadlessVmRuntimeState();
        foreach (var variable in snapshot.VariableStates)
        {
            if (variable.Value.Kind == HeadlessVmValueKind.Unsupported)
            {
                continue;
            }

            runtimeState.VariableValues[variable.StableIdIndex] = ToRuntimeValue(variable.Value);
        }

        runtimeState.RestoreOperands(snapshot.OperandStack.Select(ToRuntimeValue));
        runtimeState.CallFrames.AddRange(snapshot.CallFrames);
        runtimeState.ObjectStore.RestoreSnapshots(snapshot.Objects);
        return runtimeState;
    }

    private static HeadlessVmValueSnapshot ToSnapshotValue(HeadlessVmRuntimeValue value)
    {
        return HeadlessVmObjectSnapshot.ToSnapshotValue(value);
    }

    private static HeadlessVmRuntimeValue ToRuntimeValue(HeadlessVmValueSnapshot snapshot)
    {
        return HeadlessVmObjectSnapshot.ToRuntimeValue(snapshot);
    }

    private static bool IsKnownOffset(KlibDocument document, int offset)
    {
        if (document.Instructions.Count == 0)
        {
            return offset == 0;
        }

        return document.Instructions.Any(instruction => instruction.Offset == offset);
    }
}
