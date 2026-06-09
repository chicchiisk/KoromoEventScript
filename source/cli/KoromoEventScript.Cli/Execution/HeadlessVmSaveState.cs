using KoromoEventScript.Cli.Compilation;

namespace KoromoEventScript.Cli.Execution;

public enum HeadlessVmValueKind
{
    Null = 0,
    String = 1,
    Number = 2,
    Bool = 3,
    Array = 4,
    Reference = 5,
    Unsupported = 255,
}

public sealed record HeadlessVmValueSnapshot(
    HeadlessVmValueKind Kind,
    string? StringValue = null,
    double? NumberValue = null,
    bool? BoolValue = null,
    IReadOnlyList<HeadlessVmValueSnapshot>? ArrayItems = null,
    string? ReferenceId = null)
{
    public static HeadlessVmValueSnapshot FromObject(object? value)
    {
        return value switch
        {
            null => new HeadlessVmValueSnapshot(HeadlessVmValueKind.Null),
            string text => new HeadlessVmValueSnapshot(HeadlessVmValueKind.String, StringValue: text),
            double number => new HeadlessVmValueSnapshot(HeadlessVmValueKind.Number, NumberValue: number),
            bool boolean => new HeadlessVmValueSnapshot(HeadlessVmValueKind.Bool, BoolValue: boolean),
            IReadOnlyList<object?> list => new HeadlessVmValueSnapshot(
                HeadlessVmValueKind.Array,
                ArrayItems: list.Select(FromObject).ToArray()),
            _ => new HeadlessVmValueSnapshot(
                HeadlessVmValueKind.Unsupported,
                StringValue: value.GetType().FullName),
        };
    }
}

public sealed record HeadlessVmExecutionPosition(string ScriptId, int InstructionOffset);

public sealed record HeadlessVmVariableSnapshot(
    int StableIdIndex,
    KlibScopeKind ScopeKind,
    int ScopeId,
    HeadlessVmValueSnapshot Value);

public sealed record HeadlessVmCallFrameSnapshot(string ScriptId, int ReturnOffset);

public sealed record HeadlessVmChoiceSnapshot(string Text, int TargetOffset);

public enum HeadlessVmContinuationKind
{
    Running = 0,
    WaitingForAdvance = 1,
    WaitingForSelection = 2,
    Completed = 3,
}

public sealed record HeadlessVmContinuationState(
    HeadlessVmContinuationKind Kind,
    int ResumeOffset,
    string? Prompt = null,
    IReadOnlyList<HeadlessVmChoiceSnapshot>? PendingChoices = null)
{
    public static HeadlessVmContinuationState Running(int resumeOffset)
    {
        return new HeadlessVmContinuationState(HeadlessVmContinuationKind.Running, resumeOffset);
    }

    public static HeadlessVmContinuationState WaitingForAdvance(int resumeOffset)
    {
        return new HeadlessVmContinuationState(HeadlessVmContinuationKind.WaitingForAdvance, resumeOffset);
    }

    public static HeadlessVmContinuationState WaitingForSelection(
        int resumeOffset,
        string? prompt,
        IReadOnlyList<HeadlessVmChoiceSnapshot> pendingChoices)
    {
        return new HeadlessVmContinuationState(
            HeadlessVmContinuationKind.WaitingForSelection,
            resumeOffset,
            prompt,
            pendingChoices);
    }

    public static HeadlessVmContinuationState Completed(int resumeOffset)
    {
        return new HeadlessVmContinuationState(HeadlessVmContinuationKind.Completed, resumeOffset);
    }
}

public sealed record HeadlessVmSaveState(
    int SchemaVersion,
    HeadlessVmExecutionPosition Position,
    IReadOnlyList<HeadlessVmVariableSnapshot> VariableStates,
    IReadOnlyList<HeadlessVmCallFrameSnapshot> CallFrames,
    HeadlessVmContinuationState Continuation)
{
    public const int CurrentSchemaVersion = 1;
}
