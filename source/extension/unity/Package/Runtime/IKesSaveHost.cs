using System;
using KoromoEventScript.Runtime.Core.Persistence;

namespace KoromoEventScript.Unity
{

public sealed class KesSaveRequest
{
    public KesSaveRequest(
        int slot,
        string title,
        bool isAutosave,
        string gameId,
        string buildId,
        string eventId,
        string locale,
        RuntimeSaveSnapshot snapshot)
    {
        Slot = slot;
        Title = title ?? string.Empty;
        IsAutosave = isAutosave;
        GameId = gameId ?? string.Empty;
        BuildId = buildId ?? string.Empty;
        EventId = eventId ?? string.Empty;
        Locale = locale ?? string.Empty;
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
    }

    public int Slot { get; }

    public string Title { get; }

    public bool IsAutosave { get; }

    public string GameId { get; }

    public string BuildId { get; }

    public string EventId { get; }

    public string Locale { get; }

    public RuntimeSaveSnapshot Snapshot { get; }
}

public interface IKesSaveHost
{
    void Save(KesSaveRequest request, Action<KesHostOperationResult> completed);

    void Load(int slot, Action<RuntimeSaveSnapshot, KesHostOperationResult> completed);
}
}
