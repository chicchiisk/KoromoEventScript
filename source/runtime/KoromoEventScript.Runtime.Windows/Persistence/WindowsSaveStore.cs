using System.Text.Json;
using KoromoEventScript.Runtime.Core.Persistence;

namespace KoromoEventScript.Runtime.Windows.Persistence;

public sealed record WindowsSaveEnvelope(
    int SchemaVersion,
    string Title,
    DateTimeOffset SavedAt,
    RuntimeSaveSnapshot Snapshot,
    string? Locale,
    WindowsHostSaveState? HostState = null);

public sealed record WindowsHostSaveState(
    WindowsUiSaveState Ui,
    WindowsAudioSaveState Audio,
    string Locale);

public sealed record WindowsUiSaveState(
    string? MessageText,
    string? SpeakerName,
    IReadOnlyList<string> Choices,
    int? SelectedChoiceIndex);

public sealed record WindowsAudioSaveState(
    string? BgmAssetId,
    string? VoiceAssetId);

public sealed class WindowsSaveStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly WindowsUserDataLocator locator;

    public WindowsSaveStore(WindowsUserDataLocator locator)
    {
        this.locator = locator;
    }

    public async Task SaveAsync(SaveSlot slot, WindowsSaveEnvelope envelope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        Directory.CreateDirectory(locator.SaveDirectory);
        var savePath = locator.GetSavePath(slot);
        await using var stream = File.Create(savePath);
        await JsonSerializer.SerializeAsync(stream, envelope, JsonOptions, cancellationToken);
    }

    public async Task<WindowsSaveEnvelope> LoadAsync(SaveSlot slot, CancellationToken cancellationToken = default)
    {
        var savePath = locator.GetSavePath(slot);
        await using var stream = File.OpenRead(savePath);
        var envelope = await JsonSerializer.DeserializeAsync<WindowsSaveEnvelope>(stream, JsonOptions, cancellationToken);
        return envelope ?? throw new InvalidDataException($"Save slot '{slot.FileName}' is empty.");
    }
}
