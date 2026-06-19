using System.Text.Json;
using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Build;

public sealed record BuildDiagnosticsWriteResult(
    bool Succeeded,
    CliExitCode ExitCode,
    IReadOnlyList<Diagnostic> Diagnostics);

public sealed class BuildDiagnosticsWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public BuildDiagnosticsWriteResult Write(string path, IReadOnlyList<Diagnostic> diagnostics)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(diagnostics);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var payload = diagnostics.Select(static diagnostic => new
            {
                level = diagnostic.Level.ToString().ToLowerInvariant(),
                code = diagnostic.Code,
                file = diagnostic.File,
                line = diagnostic.Line,
                column = diagnostic.Column,
                message = diagnostic.Message,
                relatedLocations = diagnostic.RelatedLocations.Select(static location => new
                {
                    file = location.File,
                    line = location.Line,
                    column = location.Column,
                    message = location.Message,
                }),
            });

            File.WriteAllText(path, JsonSerializer.Serialize(payload, JsonOptions));
            return new BuildDiagnosticsWriteResult(true, CliExitCode.Success, []);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new BuildDiagnosticsWriteResult(
                false,
                CliExitCode.FileOrDirectoryError,
                [new Diagnostic(DiagnosticLevel.Error, "KES9004", NormalizePath(path), 1, 1, $"Could not write diagnostics.json: {exception.Message}")]);
        }
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }
}
