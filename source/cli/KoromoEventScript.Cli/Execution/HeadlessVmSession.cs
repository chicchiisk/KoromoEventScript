using KoromoEventScript.Cli.Compilation;

namespace KoromoEventScript.Cli.Execution;

public sealed class HeadlessVmSession
{
    private readonly HeadlessVmExecutor executor;
    private KlibDocument? document;

    public HeadlessVmSession()
        : this(new HeadlessVmExecutor())
    {
    }

    public HeadlessVmSession(HeadlessVmExecutor executor)
    {
        this.executor = executor;
        State = HeadlessVmState.NotStarted();
        Observation = HeadlessVmObservationLog.Empty();
    }

    public HeadlessVmState State { get; private set; }

    public HeadlessVmObservationLog Observation { get; private set; }

    public void Start(KlibDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        this.document = document;
        Observation = HeadlessVmObservationLog.Empty();

        var startOffset = document.Instructions.Count == 0 ? 0 : document.Instructions[0].Offset;
        State = HeadlessVmState.Running(document.Module.ScriptId, startOffset);
        ContinueFrom(startOffset);
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
        var result = executor.RunToBoundary(document!, offset, Observation);
        State = result.State;
        Observation = result.Observation;
    }

    private void EnsureStarted()
    {
        if (document is null)
        {
            throw new InvalidOperationException("The headless VM session has not been started.");
        }
    }
}
