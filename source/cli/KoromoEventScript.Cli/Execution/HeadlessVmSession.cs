using KoromoEventScript.Cli.Compilation;

namespace KoromoEventScript.Cli.Execution;

public sealed class HeadlessVmSession
{
    private readonly HeadlessVmExecutor executor;
    private readonly HeadlessVmSaveStateMapper saveStateMapper;
    private KlibDocument? document;
    private HeadlessVmRuntimeState runtimeState;

    public HeadlessVmSession()
        : this(new HeadlessVmExecutor(), new HeadlessVmSaveStateMapper())
    {
    }

    public HeadlessVmSession(HeadlessVmExecutor executor, HeadlessVmSaveStateMapper saveStateMapper)
    {
        this.executor = executor;
        this.saveStateMapper = saveStateMapper;
        runtimeState = new HeadlessVmRuntimeState();
        State = HeadlessVmState.NotStarted();
        Observation = HeadlessVmObservationLog.Empty();
    }

    public HeadlessVmState State { get; private set; }

    public HeadlessVmObservationLog Observation { get; private set; }

    internal KlibDocument? Document => document;

    internal HeadlessVmRuntimeState RuntimeState => runtimeState;

    public void Start(KlibDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        this.document = document;
        runtimeState = new HeadlessVmRuntimeState();
        Observation = HeadlessVmObservationLog.Empty();

        var startOffset = document.Instructions.Count == 0 ? 0 : document.Instructions[0].Offset;
        State = HeadlessVmState.Running(document.Module.ScriptId, startOffset);
        ContinueFrom(startOffset);
    }

    public HeadlessVmSaveState ExportSaveState()
    {
        EnsureStarted();
        return saveStateMapper.Export(this);
    }

    public void Restore(KlibDocument document, HeadlessVmSaveState snapshot)
    {
        saveStateMapper.Restore(this, document, snapshot);
    }

    public void ResumeAdvance()
    {
        EnsureStarted();
        if (State.Kind != HeadlessVmStateKind.WaitingForAdvance)
        {
            throw new InvalidOperationException($"Cannot resume advance from state '{State.Kind}'.");
        }

        ContinueFrom(State.InstructionOffset);
    }

    public void ResumeSelection(int selectedIndex)
    {
        EnsureStarted();
        if (State.Kind != HeadlessVmStateKind.WaitingForSelection || State.PendingChoices is null)
        {
            throw new InvalidOperationException($"Cannot resume selection from state '{State.Kind}'.");
        }

        if (selectedIndex < 0 || selectedIndex >= State.PendingChoices.Count)
        {
            State = HeadlessVmState.Faulted(new HeadlessVmFault(
                $"Invalid selection index '{selectedIndex}'.",
                document!.Module.ScriptId,
                State.InstructionOffset));
            Observation = Observation.ClearChoices();
            return;
        }

        var choice = State.PendingChoices[selectedIndex];
        Observation = Observation.ClearChoices();
        ContinueFrom(choice.TargetOffset);
    }

    private void ContinueFrom(int offset)
    {
        var result = executor.RunToBoundary(document!, runtimeState, offset, Observation);
        State = result.State;
        Observation = result.Observation;
    }

    internal void RestoreFault(string message, string scriptId, int instructionOffset)
    {
        State = HeadlessVmState.Faulted(new HeadlessVmFault(message, scriptId, instructionOffset));
        Observation = HeadlessVmObservationLog.Empty();
    }

    internal void RestoreSession(KlibDocument document, HeadlessVmSaveState snapshot, HeadlessVmRuntimeState runtimeState)
    {
        this.document = document;
        this.runtimeState = runtimeState;

        Observation = HeadlessVmObservationLog.Empty();
        State = snapshot.Continuation.Kind switch
        {
            HeadlessVmContinuationKind.Running => HeadlessVmState.Running(document.Module.ScriptId, snapshot.Position.InstructionOffset),
            HeadlessVmContinuationKind.WaitingForAdvance => HeadlessVmState.WaitingForAdvance(document.Module.ScriptId, snapshot.Continuation.ResumeOffset),
            HeadlessVmContinuationKind.WaitingForSelection => RestoreWaitingForSelection(document.Module.ScriptId, snapshot.Continuation),
            HeadlessVmContinuationKind.Completed => HeadlessVmState.Completed(document.Module.ScriptId, snapshot.Position.InstructionOffset),
            _ => HeadlessVmState.Faulted(new HeadlessVmFault(
                $"Unsupported continuation kind '{snapshot.Continuation.Kind}'.",
                document.Module.ScriptId,
                snapshot.Position.InstructionOffset)),
        };

        if (snapshot.Continuation.Kind == HeadlessVmContinuationKind.WaitingForSelection &&
            snapshot.Continuation.PendingChoices is not null)
        {
            Observation = Observation.ShowChoices(
                snapshot.Continuation.Prompt,
                snapshot.Continuation.PendingChoices
                    .Select(static choice => new HeadlessVmChoice(choice.Text, choice.TargetOffset))
                    .ToArray());
        }
    }

    private static HeadlessVmState RestoreWaitingForSelection(string scriptId, HeadlessVmContinuationState continuation)
    {
        var pendingChoices = continuation.PendingChoices?.Select(static choice => new HeadlessVmChoice(choice.Text, choice.TargetOffset)).ToArray() ?? [];
        return HeadlessVmState.WaitingForSelection(scriptId, continuation.ResumeOffset, pendingChoices);
    }

    private void EnsureStarted()
    {
        if (document is null)
        {
            throw new InvalidOperationException("The headless VM session has not been started.");
        }
    }
}
