namespace KoromoEventScript.Cli.Diagnostics;

public static class DiagnosticFormatter
{
    public static string FormatText(Diagnostic diagnostic)
    {
        var text = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{GetDisplayFile(diagnostic.File)}:{diagnostic.Line}:{diagnostic.Column} {FormatLevel(diagnostic.Level)} {diagnostic.Code}: {diagnostic.Message}");
        if (diagnostic.RelatedLocations.Count == 0)
        {
            return text;
        }

        return string.Concat(
            text,
            " ",
            string.Join(" ", diagnostic.RelatedLocations.Select(FormatRelatedLocationText)));
    }

    public static string FormatText(IEnumerable<Diagnostic> diagnostics)
    {
        return string.Join(Environment.NewLine, diagnostics.Select(FormatText));
    }

    private static string FormatLevel(DiagnosticLevel level)
    {
        return level switch
        {
            DiagnosticLevel.Error => "error",
            DiagnosticLevel.Warning => "warning",
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, null),
        };
    }

    private static string GetDisplayFile(string file)
    {
        return string.IsNullOrWhiteSpace(file) ? "<unknown>" : file;
    }

    private static string FormatRelatedLocationText(DiagnosticRelatedLocation location)
    {
        return string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"Related: {GetDisplayFile(location.File)}:{location.Line}:{location.Column} {location.Message}");
    }
}
