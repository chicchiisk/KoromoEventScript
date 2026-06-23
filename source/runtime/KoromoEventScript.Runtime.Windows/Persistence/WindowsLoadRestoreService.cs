using System.Text.Json;
using KoromoEventScript.Runtime.Core.Diagnostics;
using KoromoEventScript.Runtime.Core.Execution;

namespace KoromoEventScript.Runtime.Windows.Persistence;

public enum PlayerNotificationSeverity
{
    Info,
    Warning,
    Error,
}

public sealed record PlayerNotification(
    PlayerNotificationSeverity Severity,
    string Code,
    string Message);

public interface IPlayerNotificationSink
{
    void Notify(PlayerNotification notification);
}

public sealed record WindowsLoadRestoreResult(
    bool Succeeded,
    WindowsSaveEnvelope? Envelope,
    IReadOnlyList<RuntimeDiagnostic> Diagnostics)
{
    public static WindowsLoadRestoreResult Success(WindowsSaveEnvelope envelope)
    {
        return new WindowsLoadRestoreResult(true, envelope, []);
    }

    public static WindowsLoadRestoreResult Failure(IReadOnlyList<RuntimeDiagnostic> diagnostics)
    {
        return new WindowsLoadRestoreResult(false, null, diagnostics);
    }
}

public sealed class WindowsLoadRestoreService
{
    private readonly WindowsSaveStore saveStore;
    private readonly IPlayerNotificationSink notificationSink;

    public WindowsLoadRestoreService(WindowsSaveStore saveStore, IPlayerNotificationSink notificationSink)
    {
        this.saveStore = saveStore;
        this.notificationSink = notificationSink;
    }

    public async Task<WindowsLoadRestoreResult> LoadAsync(
        SaveSlot slot,
        KesVmSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        WindowsSaveEnvelope envelope;
        try
        {
            envelope = await saveStore.LoadAsync(slot, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            var diagnostic = RuntimeDiagnostic.Error(
                "KESR8001",
                $"Save slot '{slot.FileName}' could not be loaded: {exception.Message}",
                RuntimeFailureKind.Runtime);
            NotifyLoadFailed(diagnostic);
            return WindowsLoadRestoreResult.Failure([diagnostic]);
        }

        var restoreResult = session.Restore(envelope.Snapshot);
        if (!restoreResult.Succeeded)
        {
            foreach (var diagnostic in restoreResult.Diagnostics)
            {
                NotifyLoadFailed(diagnostic);
            }

            return WindowsLoadRestoreResult.Failure(restoreResult.Diagnostics);
        }

        return WindowsLoadRestoreResult.Success(envelope);
    }

    private void NotifyLoadFailed(RuntimeDiagnostic diagnostic)
    {
        notificationSink.Notify(new PlayerNotification(
            PlayerNotificationSeverity.Error,
            diagnostic.Code,
            $"Save data could not be loaded. {diagnostic.Message}"));
    }
}
