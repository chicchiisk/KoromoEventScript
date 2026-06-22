using System.Text.Json;

namespace KoromoEventScript.Runtime.Windows.Persistence;

public sealed record WindowsRuntimeUserSettings(
    double MasterVolume,
    double BgmVolume,
    double SeVolume,
    double VoiceVolume,
    int TextSpeed,
    double AutoSpeed,
    string SkipMode,
    bool Fullscreen,
    int WindowWidth,
    int WindowHeight,
    string Locale);

public sealed class WindowsUserSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly WindowsUserDataLocator locator;

    public WindowsUserSettingsStore(WindowsUserDataLocator locator)
    {
        this.locator = locator;
    }

    public async Task SaveAsync(WindowsRuntimeUserSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Directory.CreateDirectory(locator.GameDataRoot);
        await using var stream = File.Create(locator.SettingsPath);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
    }

    public async Task<WindowsRuntimeUserSettings?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(locator.SettingsPath))
        {
            return null;
        }

        await using var stream = File.OpenRead(locator.SettingsPath);
        return await JsonSerializer.DeserializeAsync<WindowsRuntimeUserSettings>(stream, JsonOptions, cancellationToken);
    }
}
