namespace KoromoEventScript.Runtime.Windows.Persistence;

public sealed class WindowsUserDataLocator
{
    public WindowsUserDataLocator(string userDataRoot, string publisher, string gameId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userDataRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(publisher);
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);

        UserDataRoot = userDataRoot;
        Publisher = SanitizeSegment(publisher);
        GameId = SanitizeSegment(gameId);
        GameDataRoot = Path.Combine(UserDataRoot, Publisher, GameId);
    }

    public string UserDataRoot { get; }

    public string Publisher { get; }

    public string GameId { get; }

    public string GameDataRoot { get; }

    public string SaveDirectory => Path.Combine(GameDataRoot, "saves");

    public string SettingsPath => Path.Combine(GameDataRoot, "settings.json");

    public static WindowsUserDataLocator ForLocalApplicationData(string publisher, string gameId)
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KoromoEventScript");
        return new WindowsUserDataLocator(root, publisher, gameId);
    }

    public string GetSavePath(SaveSlot slot)
    {
        return Path.Combine(SaveDirectory, slot.FileName);
    }

    private static string SanitizeSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value
            .Trim()
            .Select(character => invalid.Contains(character) || char.IsWhiteSpace(character) ? '_' : character)
            .ToArray();
        var sanitized = new string(chars).Trim('_');
        return string.IsNullOrWhiteSpace(sanitized) ? "game" : sanitized;
    }
}

public sealed record SaveSlot(int Number, bool IsAuto = false)
{
    public string FileName
    {
        get
        {
            if (IsAuto)
            {
                return "autosave.json";
            }

            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(Number);
            return $"slot{Number:000}.json";
        }
    }
}
