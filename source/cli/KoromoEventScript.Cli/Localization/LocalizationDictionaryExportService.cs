using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.Diagnostics;
using KoromoEventScript.Cli.ProjectSystem;
using KoromoEventScript.Cli.Semantics;

namespace KoromoEventScript.Cli.Localization;

public sealed record LocalizationExportRequest(
    ProjectConfig Config,
    IReadOnlyList<ScriptDocument> OrderedDocuments,
    TagAssignmentPlan TagPlan,
    IReadOnlyList<string> RequestedLocales,
    string OutputPath);

public sealed class LocalizationDictionaryExportService
{
    private const string PrimaryLocale = "ja";

    private readonly LocalizationTextExtractor extractor;
    private readonly LocalizationDictionaryCsvRepository repository;

    public LocalizationDictionaryExportService()
        : this(new LocalizationTextExtractor(), new LocalizationDictionaryCsvRepository())
    {
    }

    public LocalizationDictionaryExportService(
        LocalizationTextExtractor extractor,
        LocalizationDictionaryCsvRepository repository)
    {
        this.extractor = extractor;
        this.repository = repository;
    }

    public LocalizationExportResult Export(LocalizationExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var existing = repository.Load(request.OutputPath);
        if (!existing.Succeeded)
        {
            return LocalizationExportResult.Failure(CliExitCode.FileOrDirectoryError, existing.Diagnostics);
        }

        var existingDocument = existing.Document;
        var localeColumns = LocalizationLocaleSelection.Resolve(
            request.RequestedLocales,
            existingDocument?.LocaleColumns ?? [],
            PrimaryLocale);

        var extractedEntries = extractor.Extract(request.OrderedDocuments, request.TagPlan);
        var existingByTag = (existingDocument?.Entries ?? [])
            .ToDictionary(static entry => entry.Tag, StringComparer.Ordinal);

        var mergedEntries = new List<LocalizationDictionaryEntry>();
        foreach (var entry in extractedEntries)
        {
            var translations = new Dictionary<string, string>(StringComparer.Ordinal);
            if (existingByTag.TryGetValue(entry.Tag, out var existingEntry))
            {
                foreach (var locale in existingEntry.Translations.Keys)
                {
                    translations[locale] = existingEntry.Translations[locale];
                }
            }

            foreach (var locale in localeColumns)
            {
                translations.TryAdd(locale, string.Empty);
            }

            mergedEntries.Add(new LocalizationDictionaryEntry(entry.Tag, entry.Speaker, entry.Original, translations));
        }

        foreach (var existingEntry in existingDocument?.Entries ?? [])
        {
            if (mergedEntries.Any(entry => string.Equals(entry.Tag, existingEntry.Tag, StringComparison.Ordinal)))
            {
                continue;
            }

            var translations = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var locale in localeColumns)
            {
                translations[locale] = existingEntry.Translations.TryGetValue(locale, out var value) ? value : string.Empty;
            }

            mergedEntries.Add(existingEntry with { Translations = translations });
        }

        var document = new LocalizationDictionaryDocument(localeColumns, mergedEntries);
        var save = repository.Save(request.OutputPath, document);
        if (!save.Succeeded)
        {
            return LocalizationExportResult.Failure(CliExitCode.FileOrDirectoryError, save.Diagnostics);
        }

        return LocalizationExportResult.Success(document, request.OutputPath);
    }
}

public sealed record LocalizationExportResult(
    CliExitCode ExitCode,
    IReadOnlyList<Diagnostic> Diagnostics,
    LocalizationDictionaryDocument? Document,
    string OutputPath)
{
    public bool Succeeded => ExitCode == CliExitCode.Success;

    public static LocalizationExportResult Success(LocalizationDictionaryDocument document, string outputPath)
    {
        return new LocalizationExportResult(CliExitCode.Success, [], document, outputPath);
    }

    public static LocalizationExportResult Failure(CliExitCode exitCode, IReadOnlyList<Diagnostic> diagnostics)
    {
        return new LocalizationExportResult(exitCode, diagnostics, null, string.Empty);
    }
}
