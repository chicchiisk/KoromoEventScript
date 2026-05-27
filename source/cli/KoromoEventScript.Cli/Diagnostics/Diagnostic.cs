namespace KoromoEventScript.Cli.Diagnostics;

public sealed record DiagnosticRelatedLocation(
    string File,
    int Line,
    int Column,
    string Message);

public sealed record Diagnostic
{
    public Diagnostic(
        DiagnosticLevel level,
        string code,
        string file,
        int line,
        int column,
        string message,
        IReadOnlyList<DiagnosticRelatedLocation>? relatedLocations = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(message);

        Level = level;
        Code = code;
        File = file;
        Line = line;
        Column = column;
        Message = message;
        RelatedLocations = relatedLocations?.ToArray() ?? [];
    }

    public DiagnosticLevel Level { get; init; }

    public string Code { get; init; }

    public string File { get; init; }

    public int Line { get; init; }

    public int Column { get; init; }

    public string Message { get; init; }

    public IReadOnlyList<DiagnosticRelatedLocation> RelatedLocations { get; init; }
}
