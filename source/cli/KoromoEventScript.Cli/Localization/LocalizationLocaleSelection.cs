namespace KoromoEventScript.Cli.Localization;

public static class LocalizationLocaleSelection
{
    public static IReadOnlyList<string> Resolve(
        IReadOnlyList<string> requestedLocales,
        IReadOnlyList<string> existingLocales,
        string primaryLocale)
    {
        ArgumentNullException.ThrowIfNull(requestedLocales);
        ArgumentNullException.ThrowIfNull(existingLocales);
        ArgumentException.ThrowIfNullOrWhiteSpace(primaryLocale);

        var locales = new List<string>();
        foreach (var locale in existingLocales)
        {
            if (!locales.Contains(locale, StringComparer.Ordinal))
            {
                locales.Add(locale);
            }
        }

        foreach (var locale in requestedLocales)
        {
            if (!locales.Contains(locale, StringComparer.Ordinal))
            {
                locales.Add(locale);
            }
        }

        if (locales.Count == 0)
        {
            locales.Add(primaryLocale);
        }

        return locales;
    }
}
