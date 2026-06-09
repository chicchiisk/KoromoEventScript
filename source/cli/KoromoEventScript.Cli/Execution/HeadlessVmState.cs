namespace KoromoEventScript.Cli.Execution;

public enum HeadlessVmStateKind
{
    NotStarted = 0,
    Running = 1,
    WaitingForAdvance = 2,
    WaitingForSelection = 3,
    Completed = 4,
    Faulted = 5,
}

public enum HeadlessVmStopReason
{
    None = 0,
    AdvanceRequested = 1,
    SelectionRequested = 2,
    Completed = 3,
    Faulted = 4,
}

public sealed record HeadlessVmFault(
    string Message,
    string ScriptId,
    int InstructionOffset,
    int? Line = null,
    int? Column = null);

public sealed record HeadlessVmState(
    HeadlessVmStateKind Kind,
    HeadlessVmStopReason StopReason,
    string? ScriptId,
    int InstructionOffset,
    IReadOnlyList<HeadlessVmChoice>? PendingChoices = null,
    HeadlessVmFault? Fault = null)
{
    public static HeadlessVmState NotStarted()
    {
        return new HeadlessVmState(HeadlessVmStateKind.NotStarted, HeadlessVmStopReason.None, null, 0);
    }

    public static HeadlessVmState Running(string scriptId, int instructionOffset)
    {
        return new HeadlessVmState(HeadlessVmStateKind.Running, HeadlessVmStopReason.None, scriptId, instructionOffset);
    }

    public static HeadlessVmState WaitingForAdvance(string scriptId, int instructionOffset)
    {
        return new HeadlessVmState(
            HeadlessVmStateKind.WaitingForAdvance,
            HeadlessVmStopReason.AdvanceRequested,
            scriptId,
            instructionOffset);
    }

    public static HeadlessVmState WaitingForSelection(string scriptId, int instructionOffset, IReadOnlyList<HeadlessVmChoice> choices)
    {
        return new HeadlessVmState(
            HeadlessVmStateKind.WaitingForSelection,
            HeadlessVmStopReason.SelectionRequested,
            scriptId,
            instructionOffset,
            choices);
    }

    public static HeadlessVmState Completed(string scriptId, int instructionOffset)
    {
        return new HeadlessVmState(
            HeadlessVmStateKind.Completed,
            HeadlessVmStopReason.Completed,
            scriptId,
            instructionOffset);
    }

    public static HeadlessVmState Faulted(HeadlessVmFault fault)
    {
        ArgumentNullException.ThrowIfNull(fault);
        return new HeadlessVmState(
            HeadlessVmStateKind.Faulted,
            HeadlessVmStopReason.Faulted,
            fault.ScriptId,
            fault.InstructionOffset,
            Fault: fault);
    }
}
