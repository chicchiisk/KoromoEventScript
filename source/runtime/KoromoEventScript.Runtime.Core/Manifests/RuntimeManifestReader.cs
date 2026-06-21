using System.Text.Json;
using System.Text.Json.Serialization;
using KoromoEventScript.Runtime.Core.Diagnostics;

namespace KoromoEventScript.Runtime.Core.Manifests;

public interface IRuntimeManifestReader
{
    RuntimeManifestReadResult Read(string manifestPath);
}

public sealed class RuntimeManifestReader : IRuntimeManifestReader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public RuntimeManifestReadResult Read(string manifestPath)
    {
        var fullManifestPath = Path.GetFullPath(manifestPath);
        if (!File.Exists(fullManifestPath))
        {
            return RuntimeManifestReadResult.Failure(Diagnostic("KESR1001", $"Runtime manifest was not found: {fullManifestPath}"));
        }

        ManifestJson? json;
        try
        {
            using var stream = File.OpenRead(fullManifestPath);
            json = JsonSerializer.Deserialize<ManifestJson>(stream, JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return RuntimeManifestReadResult.Failure(Diagnostic("KESR1001", $"Runtime manifest could not be read: {ex.Message}"));
        }

        if (json is null)
        {
            return RuntimeManifestReadResult.Failure(Diagnostic("KESR1001", "Runtime manifest is empty."));
        }

        var diagnostics = Validate(json);
        if (diagnostics.Count > 0)
        {
            return RuntimeManifestReadResult.Failure(diagnostics);
        }

        var manifestDirectory = Path.GetDirectoryName(fullManifestPath)!;
        var scripts = json.Scripts!
            .Select(script => new RuntimeScriptEntry(
                script.ScriptId!,
                script.Locale!,
                script.KlibPath!,
                ResolvePath(manifestDirectory, script.KlibPath!),
                script.IsEntry,
                script.StartLabel))
            .ToArray();
        var assets = (json.Assets ?? [])
            .Select(asset => new RuntimeAssetEntry(
                asset.AssetId!,
                asset.Kind!,
                asset.Path!,
                ResolvePath(manifestDirectory, asset.Path!),
                asset.Locale))
            .ToArray();

        var document = new RuntimeManifestDocument(
            json.SchemaVersion!,
            json.GameId!,
            json.Title!,
            json.DefaultLocale!,
            scripts,
            assets,
            new RuntimeSettings(json.Defaults?.Width, json.Defaults?.Height, json.Defaults?.Fullscreen),
            new RuntimeBuildInfo(json.Build?.BuildId, json.Build?.CliVersion),
            fullManifestPath,
            manifestDirectory);

        return RuntimeManifestReadResult.Success(document);
    }

    private static List<RuntimeDiagnostic> Validate(ManifestJson json)
    {
        var diagnostics = new List<RuntimeDiagnostic>();

        Require(json.SchemaVersion, "schemaVersion", diagnostics);
        Require(json.GameId, "gameId", diagnostics);
        Require(json.Title, "title", diagnostics);
        Require(json.DefaultLocale, "defaultLocale", diagnostics);

        if (json.Scripts is null || json.Scripts.Count == 0)
        {
            diagnostics.Add(Diagnostic("KESR1002", "Runtime manifest must include at least one script entry."));
        }
        else
        {
            foreach (var script in json.Scripts)
            {
                Require(script.ScriptId, "scripts[].scriptId", diagnostics);
                Require(script.Locale, "scripts[].locale", diagnostics);
                Require(script.KlibPath, "scripts[].klibPath", diagnostics);
            }
        }

        foreach (var asset in json.Assets ?? [])
        {
            Require(asset.AssetId, "assets[].assetId", diagnostics);
            Require(asset.Kind, "assets[].kind", diagnostics);
            Require(asset.Path, "assets[].path", diagnostics);
        }

        return diagnostics;
    }

    private static void Require(string? value, string field, List<RuntimeDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            diagnostics.Add(Diagnostic("KESR1002", $"Runtime manifest is missing required field '{field}'."));
        }
    }

    private static RuntimeDiagnostic Diagnostic(string code, string message)
    {
        return RuntimeDiagnostic.Error(code, message, RuntimeFailureKind.Startup);
    }

    private static string ResolvePath(string manifestDirectory, string path)
    {
        return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(manifestDirectory, path));
    }

    private sealed record ManifestJson
    {
        public string? SchemaVersion { get; init; }

        public string? GameId { get; init; }

        public string? Title { get; init; }

        public string? DefaultLocale { get; init; }

        public List<ScriptJson>? Scripts { get; init; }

        public List<AssetJson>? Assets { get; init; }

        public SettingsJson? Defaults { get; init; }

        public BuildJson? Build { get; init; }
    }

    private sealed record ScriptJson
    {
        public string? ScriptId { get; init; }

        public string? Locale { get; init; }

        public string? KlibPath { get; init; }

        public bool IsEntry { get; init; }

        public string? StartLabel { get; init; }
    }

    private sealed record AssetJson
    {
        public string? AssetId { get; init; }

        public string? Kind { get; init; }

        public string? Path { get; init; }

        public string? Locale { get; init; }
    }

    private sealed record SettingsJson
    {
        public int? Width { get; init; }

        public int? Height { get; init; }

        public bool? Fullscreen { get; init; }
    }

    private sealed record BuildJson
    {
        public string? BuildId { get; init; }

        public string? CliVersion { get; init; }
    }
}

public sealed record RuntimeManifestReadResult(
    RuntimeManifestDocument? Document,
    IReadOnlyList<RuntimeDiagnostic> Diagnostics,
    RuntimeFailureKind FailureKind)
{
    public bool Succeeded => Document is not null && Diagnostics.Count == 0;

    public static RuntimeManifestReadResult Success(RuntimeManifestDocument document)
    {
        return new RuntimeManifestReadResult(document, [], RuntimeFailureKind.None);
    }

    public static RuntimeManifestReadResult Failure(IReadOnlyList<RuntimeDiagnostic> diagnostics)
    {
        return new RuntimeManifestReadResult(null, diagnostics.ToArray(), RuntimeFailureKind.Startup);
    }

    public static RuntimeManifestReadResult Failure(RuntimeDiagnostic diagnostic)
    {
        return Failure([diagnostic]);
    }
}
