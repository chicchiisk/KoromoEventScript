namespace KoromoEventScript.Cli.Execution;

public sealed record HeadlessVmChoice(string Text, int TargetOffset);

public sealed record HeadlessVmTranscriptEntry(string? Speaker, string Text, bool IsNarration);

public enum HeadlessVmObservationEventKind
{
    None = 0,
    Say = 1,
    Nar = 2,
    Select = 3,
}

public sealed record HeadlessVmObservationEvent(
    HeadlessVmObservationEventKind Kind,
    string? Speaker,
    string? Text);

public sealed record HeadlessVmObservationLog(
    IReadOnlyList<HeadlessVmTranscriptEntry> Transcript,
    HeadlessVmObservationEvent? LastEvent,
    string? CurrentPrompt,
    IReadOnlyList<HeadlessVmChoice> CurrentChoices)
{
    public static HeadlessVmObservationLog Empty()
    {
        return new HeadlessVmObservationLog(
            [],
            new HeadlessVmObservationEvent(HeadlessVmObservationEventKind.None, null, null),
            null,
            []);
    }

    public HeadlessVmObservationLog AppendSay(string? speaker, string text)
    {
        var transcript = Transcript.Concat([new HeadlessVmTranscriptEntry(speaker, text, false)]).ToArray();
        return this with
        {
            Transcript = transcript,
            LastEvent = new HeadlessVmObservationEvent(HeadlessVmObservationEventKind.Say, speaker, text),
            CurrentPrompt = null,
            CurrentChoices = [],
        };
    }

    public HeadlessVmObservationLog AppendNarration(string text)
    {
        var transcript = Transcript.Concat([new HeadlessVmTranscriptEntry(null, text, true)]).ToArray();
        return this with
        {
            Transcript = transcript,
            LastEvent = new HeadlessVmObservationEvent(HeadlessVmObservationEventKind.Nar, null, text),
            CurrentPrompt = null,
            CurrentChoices = [],
        };
    }

    public HeadlessVmObservationLog ShowChoices(string? prompt, IReadOnlyList<HeadlessVmChoice> choices)
    {
        return this with
        {
            LastEvent = new HeadlessVmObservationEvent(HeadlessVmObservationEventKind.Select, null, prompt),
            CurrentPrompt = prompt,
            CurrentChoices = choices.ToArray(),
        };
    }

    public HeadlessVmObservationLog ClearChoices()
    {
        return this with
        {
            CurrentPrompt = null,
            CurrentChoices = [],
        };
    }
}
