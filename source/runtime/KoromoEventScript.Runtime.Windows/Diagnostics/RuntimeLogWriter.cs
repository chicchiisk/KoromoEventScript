namespace KoromoEventScript.Runtime.Windows.Diagnostics;

public sealed class RuntimeLogWriter
{
    private readonly string logPath;
    private readonly RuntimeDebugOverlayFormatter formatter;

    public RuntimeLogWriter(string logPath, RuntimeDebugOverlayFormatter? formatter = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logPath);

        this.logPath = logPath;
        this.formatter = formatter ?? new RuntimeDebugOverlayFormatter();
    }

    public async Task WriteAsync(
        RuntimeDiagnosticsSnapshot snapshot,
        RuntimeDiagnosticsMode mode,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(logPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var lines = formatter.Format(snapshot, mode);
        await File.AppendAllLinesAsync(logPath, lines, cancellationToken);
    }
}
