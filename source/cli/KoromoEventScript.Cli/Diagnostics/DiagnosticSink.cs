namespace KoromoEventScript.Cli.Diagnostics;

public sealed class DiagnosticSink
{
    public void Write(IEnumerable<Diagnostic> diagnostics, DiagnosticOutputFormat format, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(writer);

        var formatted = format switch
        {
            DiagnosticOutputFormat.Text => DiagnosticFormatter.FormatText(diagnostics),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
        };

        if (string.IsNullOrEmpty(formatted))
        {
            return;
        }

        writer.WriteLine(formatted);
    }
}
