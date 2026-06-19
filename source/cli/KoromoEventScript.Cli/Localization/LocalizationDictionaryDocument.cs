namespace KoromoEventScript.Cli.Localization;

public sealed record LocalizationDictionaryDocument(
    IReadOnlyList<string> LocaleColumns,
    IReadOnlyList<LocalizationDictionaryEntry> Entries);
