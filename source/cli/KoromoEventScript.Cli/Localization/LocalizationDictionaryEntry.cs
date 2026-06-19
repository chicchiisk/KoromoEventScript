namespace KoromoEventScript.Cli.Localization;

public sealed record LocalizationDictionaryEntry(
    string Tag,
    string Speaker,
    string Original,
    IReadOnlyDictionary<string, string> Translations);
