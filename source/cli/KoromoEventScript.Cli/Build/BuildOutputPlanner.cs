using KoromoEventScript.Cli.Commands.Build;
using KoromoEventScript.Cli.ProjectSystem;

namespace KoromoEventScript.Cli.Build;

public sealed record BuildArtifactPaths(
    string KlibPath,
    string? KlibTextPath,
    string ManifestPath,
    string DiagnosticsPath);

public sealed class BuildOutputPlanner
{
    public BuildArtifactPaths Resolve(ProjectConfig config, BuildCommandOptions options, string projectRelativeScriptPath)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRelativeScriptPath);

        var outputRoot = ResolveOutputRoot(config, options);
        var scriptRelativePath = Path.GetRelativePath(config.EventsPath, projectRelativeScriptPath);
        var scriptOutputPath = Path.ChangeExtension(scriptRelativePath, ".klib")!;

        var eventsRoot = Path.Combine(outputRoot, options.Target, config.EventsPath);
        if (!string.IsNullOrWhiteSpace(options.Locale))
        {
            eventsRoot = Path.Combine(eventsRoot, "loc", options.Locale);
        }

        var klibPath = Path.Combine(eventsRoot, scriptOutputPath);
        var klibTextPath = options.EmitTextIr
            ? Path.ChangeExtension(klibPath, ".klibtxt")
            : null;
        var manifestFileName = string.Equals(options.Target, "unity", StringComparison.OrdinalIgnoreCase)
            ? "manifest.kson"
            : "manifest.json";

        return new BuildArtifactPaths(
            klibPath,
            klibTextPath,
            Path.Combine(outputRoot, options.Target, manifestFileName),
            Path.Combine(outputRoot, options.Target, "diagnostics.json"));
    }

    private static string ResolveOutputRoot(ProjectConfig config, BuildCommandOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.OutputDirectory))
        {
            return Path.Combine(config.ProjectRoot, config.BuildPath);
        }

        return Path.IsPathRooted(options.OutputDirectory)
            ? options.OutputDirectory
            : Path.Combine(config.ProjectRoot, options.OutputDirectory);
    }
}
