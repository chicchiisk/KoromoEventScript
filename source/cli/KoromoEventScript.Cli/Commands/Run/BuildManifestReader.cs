using System.Text.Json;
using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Commands.Run;

public sealed class BuildManifestReader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public BuildManifestReadResult Read(string manifestPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);

        var fullManifestPath = Path.GetFullPath(manifestPath);
        if (!File.Exists(fullManifestPath))
        {
            return BuildManifestReadResult.Failure(
                [FileDiagnostic("KES9002", fullManifestPath, $"manifest.json was not found: {fullManifestPath}")]);
        }

        ManifestJson? json;
        try
        {
            using var stream = File.OpenRead(fullManifestPath);
            json = JsonSerializer.Deserialize<ManifestJson>(stream, JsonOptions);
        }
        catch (JsonException exception)
        {
            return BuildManifestReadResult.Failure(
                [FileDiagnostic("KES9003", fullManifestPath, $"Invalid manifest.json: {exception.Message}")]);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return BuildManifestReadResult.Failure(
                [FileDiagnostic("KES9002", fullManifestPath, $"Could not read manifest.json: {exception.Message}")]);
        }

        if (json is null)
        {
            return BuildManifestReadResult.Failure(
                [FileDiagnostic("KES9003", fullManifestPath, "Invalid manifest.json: document is empty.")]);
        }

        var diagnostics = Validate(json, fullManifestPath);
        if (diagnostics.Count > 0)
        {
            return BuildManifestReadResult.Failure(diagnostics);
        }

        var document = new RunBuildManifestDocument(
            json.Target!,
            json.Scripts!
                .Select(static script => new RunBuildManifestScript(script.KlibPath!))
                .ToArray());
        return BuildManifestReadResult.Success(document);
    }

    private static List<Diagnostic> Validate(ManifestJson json, string manifestPath)
    {
        var diagnostics = new List<Diagnostic>();
        Require(json.Target, "target", manifestPath, diagnostics);

        if (json.Scripts is null || json.Scripts.Count == 0)
        {
            diagnostics.Add(FileDiagnostic(
                "KES9003",
                manifestPath,
                "Invalid manifest.json: required field 'scripts' must contain at least one entry."));
            return diagnostics;
        }

        foreach (var script in json.Scripts)
        {
            Require(script.KlibPath, "scripts[].klibPath", manifestPath, diagnostics);
        }

        return diagnostics;
    }

    private static void Require(string? value, string field, string manifestPath, List<Diagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            diagnostics.Add(FileDiagnostic(
                "KES9003",
                manifestPath,
                $"Invalid manifest.json: required field '{field}' is missing or empty."));
        }
    }

    private static Diagnostic FileDiagnostic(string code, string path, string message)
    {
        return new Diagnostic(DiagnosticLevel.Error, code, NormalizePath(path), 1, 1, message);
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private sealed record ManifestJson
    {
        public string? Target { get; init; }

        public List<ScriptJson>? Scripts { get; init; }
    }

    private sealed record ScriptJson
    {
        public string? KlibPath { get; init; }
    }
}
