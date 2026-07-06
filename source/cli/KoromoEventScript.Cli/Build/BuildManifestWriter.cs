using System.Text.Json;
using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Build;

public sealed record BuildManifestWriteResult(
    bool Succeeded,
    CliExitCode ExitCode,
    IReadOnlyList<Diagnostic> Diagnostics);

public sealed class BuildManifestWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public BuildManifestWriteResult Write(string path, BuildManifestDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(document);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var payload = new
            {
                schemaVersion = "1.0",
                gameId = document.GameId,
                title = document.Title,
                defaultLocale = document.DefaultLocale,
                cliVersion = document.CliVersion,
                target = document.Target,
                entryEventListPath = document.EntryEventListPath,
                inputs = document.Inputs.Select(static input => new
                {
                    path = input.Path,
                    kind = input.Kind,
                }),
                scripts = document.Scripts.Select(static script => new
                {
                    scriptId = script.ScriptId,
                    locale = script.Locale,
                    isEntry = script.IsEntry,
                    startLabel = script.StartLabel,
                    sourcePath = script.SourcePath,
                    klibPath = script.KlibPath,
                    klibTextPath = script.KlibTextPath,
                }),
                events = document.Events.Select(static entry => new
                {
                    eventId = entry.EventId,
                    type = entry.Type,
                    chapter = entry.Chapter,
                    scriptId = entry.ScriptId,
                    isEntry = entry.IsEntry,
                    trigger = ToJson(entry.Trigger),
                }),
                assets = document.Assets.Select(static asset => new
                {
                    assetId = asset.AssetId,
                    kind = asset.Kind,
                    path = asset.Path,
                    locale = asset.Locale,
                }),
                defaults = new
                {
                    width = document.Defaults.Width,
                    height = document.Defaults.Height,
                    fullscreen = document.Defaults.Fullscreen,
                },
                build = new
                {
                    buildId = document.Build.BuildId,
                    cliVersion = document.Build.CliVersion,
                    target = document.Target,
                },
                localizations = document.Localizations.Select(localization => new
                {
                    locale = localization.Locale,
                    scripts = localization.Scripts.Select(static script => new
                    {
                        scriptId = script.ScriptId,
                        locale = script.Locale,
                        isEntry = script.IsEntry,
                        startLabel = script.StartLabel,
                        sourcePath = script.SourcePath,
                        klibPath = script.KlibPath,
                        klibTextPath = script.KlibTextPath,
                    }),
                }),
            };

            File.WriteAllText(path, JsonSerializer.Serialize(payload, JsonOptions));
            return new BuildManifestWriteResult(true, CliExitCode.Success, []);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new BuildManifestWriteResult(
                false,
                CliExitCode.FileOrDirectoryError,
                [new Diagnostic(DiagnosticLevel.Error, "KES9004", NormalizePath(path), 1, 1, $"Could not write manifest.json: {exception.Message}")]);
        }
    }

    private static object? ToJson(BuildManifestTrigger? trigger)
    {
        if (trigger is null)
        {
            return null;
        }

        return new
        {
            conditions = trigger.Conditions.Select(static condition => new
            {
                kind = condition.Kind,
                from = condition.From,
                param = condition.Param,
                value = condition.Value is null
                    ? null
                    : new
                    {
                        kind = condition.Value.Kind,
                        text = condition.Value.Text,
                    },
            }),
            or = trigger.Or.Select(ToJson),
        };
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }
}
