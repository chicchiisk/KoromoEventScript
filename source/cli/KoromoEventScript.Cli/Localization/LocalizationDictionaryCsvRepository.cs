using System.Text;
using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Localization;

public sealed class LocalizationDictionaryCsvRepository
{
    private static readonly UTF8Encoding Utf8BomEncoding = new(encoderShouldEmitUTF8Identifier: true);

    public LocalizationDictionaryLoadResult Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            return LocalizationDictionaryLoadResult.NotFound();
        }

        string source;
        try
        {
            source = File.ReadAllText(path, Encoding.UTF8);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return LocalizationDictionaryLoadResult.Failure(FileDiagnostic(path, $"Could not read localization dictionary: {exception.Message}"));
        }

        List<string[]> rows;
        try
        {
            rows = ParseCsv(source);
        }
        catch (InvalidDataException exception)
        {
            return LocalizationDictionaryLoadResult.Failure(DictionaryDiagnostic(path, exception.Message));
        }

        if (rows.Count == 0)
        {
            return LocalizationDictionaryLoadResult.Failure(DictionaryDiagnostic(path, "Localization dictionary must include a header row."));
        }

        var header = rows[0];
        if (header.Length < 3 || !string.Equals(header[0], "tag", StringComparison.Ordinal) || !string.Equals(header[1], "say", StringComparison.Ordinal) || !string.Equals(header[2], "original", StringComparison.Ordinal))
        {
            return LocalizationDictionaryLoadResult.Failure(DictionaryDiagnostic(path, "Localization dictionary must start with 'tag,say,original'."));
        }

        var localeColumns = new List<string>();
        for (var index = 3; index < header.Length; index++)
        {
            var locale = header[index];
            if (string.IsNullOrWhiteSpace(locale) || !locale.All(static character => char.IsAsciiLetterOrDigit(character) || character == '-'))
            {
                return LocalizationDictionaryLoadResult.Failure(DictionaryDiagnostic(path, $"Invalid locale column '{locale}'."));
            }

            localeColumns.Add(locale);
        }

        var entries = new List<LocalizationDictionaryEntry>();
        var seenTags = new HashSet<string>(StringComparer.Ordinal);
        for (var rowIndex = 1; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            if (row.Length == 0 || row.All(string.IsNullOrEmpty))
            {
                continue;
            }

            var paddedRow = PadRow(row, header.Length);
            var tag = paddedRow[0];
            if (string.IsNullOrWhiteSpace(tag))
            {
                return LocalizationDictionaryLoadResult.Failure(DictionaryDiagnostic(path, $"Row {rowIndex + 1} is missing a tag value."));
            }

            if (!seenTags.Add(tag))
            {
                return LocalizationDictionaryLoadResult.Failure(DictionaryDiagnostic(path, $"Duplicate tag '{tag}' was found."));
            }

            var translations = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var localeIndex = 0; localeIndex < localeColumns.Count; localeIndex++)
            {
                translations[localeColumns[localeIndex]] = paddedRow[localeIndex + 3];
            }

            entries.Add(new LocalizationDictionaryEntry(tag, paddedRow[1], paddedRow[2], translations));
        }

        return LocalizationDictionaryLoadResult.Success(new LocalizationDictionaryDocument(localeColumns, entries));
    }

    public LocalizationDictionarySaveResult Save(string path, LocalizationDictionaryDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(document);

        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var lines = new List<string>
            {
                BuildCsvRow(["tag", "say", "original", .. document.LocaleColumns])
            };

            foreach (var entry in document.Entries)
            {
                var row = new List<string> { entry.Tag, entry.Speaker, entry.Original };
                foreach (var locale in document.LocaleColumns)
                {
                    row.Add(entry.Translations.TryGetValue(locale, out var value) ? value : string.Empty);
                }

                lines.Add(BuildCsvRow(row));
            }

            File.WriteAllText(path, string.Join("\r\n", lines) + "\r\n", Utf8BomEncoding);
            return LocalizationDictionarySaveResult.Success();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return LocalizationDictionarySaveResult.Failure(FileDiagnostic(path, $"Could not write localization dictionary: {exception.Message}"));
        }
    }

    private static string[] PadRow(string[] row, int expectedLength)
    {
        if (row.Length >= expectedLength)
        {
            return row;
        }

        var padded = new string[expectedLength];
        Array.Copy(row, padded, row.Length);
        for (var index = row.Length; index < padded.Length; index++)
        {
            padded[index] = string.Empty;
        }

        return padded;
    }

    private static Diagnostic FileDiagnostic(string path, string message)
    {
        return new Diagnostic(DiagnosticLevel.Error, "KES9004", NormalizePath(path), 1, 1, message);
    }

    private static Diagnostic DictionaryDiagnostic(string path, string message)
    {
        return new Diagnostic(DiagnosticLevel.Error, "KES9006", NormalizePath(path), 1, 1, message);
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private static string BuildCsvRow(IEnumerable<string> values)
    {
        return string.Join(",", values.Select(EscapeCsv));
    }

    private static string EscapeCsv(string value)
    {
        value ??= string.Empty;
        if (value.Contains('"'))
        {
            value = value.Replace("\"", "\"\"");
        }

        if (value.IndexOfAny([',', '\r', '\n', '"']) >= 0)
        {
            return $"\"{value}\"";
        }

        return value;
    }

    private static List<string[]> ParseCsv(string source)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var value = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < source.Length; index++)
        {
            var character = source[index];
            if (inQuotes)
            {
                if (character == '"')
                {
                    if (index + 1 < source.Length && source[index + 1] == '"')
                    {
                        value.Append('"');
                        index++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    value.Append(character);
                }

                continue;
            }

            switch (character)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    row.Add(value.ToString());
                    value.Clear();
                    break;
                case '\r':
                    row.Add(value.ToString());
                    value.Clear();
                    rows.Add(row.ToArray());
                    row = [];
                    if (index + 1 < source.Length && source[index + 1] == '\n')
                    {
                        index++;
                    }

                    break;
                case '\n':
                    row.Add(value.ToString());
                    value.Clear();
                    rows.Add(row.ToArray());
                    row = [];
                    break;
                default:
                    value.Append(character);
                    break;
            }
        }

        if (inQuotes)
        {
            throw new InvalidDataException("Unterminated quoted field was found in localization dictionary.");
        }

        if (value.Length > 0 || row.Count > 0)
        {
            row.Add(value.ToString());
            rows.Add(row.ToArray());
        }

        return rows;
    }
}

public sealed record LocalizationDictionaryLoadResult(
    bool Succeeded,
    bool Exists,
    LocalizationDictionaryDocument? Document,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    public static LocalizationDictionaryLoadResult Success(LocalizationDictionaryDocument document)
    {
        return new LocalizationDictionaryLoadResult(true, true, document, []);
    }

    public static LocalizationDictionaryLoadResult NotFound()
    {
        return new LocalizationDictionaryLoadResult(true, false, null, []);
    }

    public static LocalizationDictionaryLoadResult Failure(params Diagnostic[] diagnostics)
    {
        return new LocalizationDictionaryLoadResult(false, true, null, diagnostics);
    }
}

public sealed record LocalizationDictionarySaveResult(
    bool Succeeded,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    public static LocalizationDictionarySaveResult Success()
    {
        return new LocalizationDictionarySaveResult(true, []);
    }

    public static LocalizationDictionarySaveResult Failure(params Diagnostic[] diagnostics)
    {
        return new LocalizationDictionarySaveResult(false, diagnostics);
    }
}
