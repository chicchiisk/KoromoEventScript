using KoromoEventScript.Cli.Diagnostics;
using KoromoEventScript.Cli.ProjectSystem;

namespace KoromoEventScript.Cli.Commands.Clean;

public sealed class CleanService
{
    public CleanCommandResult Execute(ProjectConfig config, CleanCommandOptions options)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            var targets = ResolveTargets(config, options).ToArray();
            if (!options.DryRun)
            {
                foreach (var target in targets)
                {
                    DeleteIfExists(target);
                }
            }

            return new CleanCommandResult(CliExitCode.Success, [], targets);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new CleanCommandResult(
                CliExitCode.FileOrDirectoryError,
                [new Diagnostic(DiagnosticLevel.Error, "KES9004", config.ProjectRoot, 1, 1, $"Could not clean project artifacts: {exception.Message}")],
                []);
        }
    }

    private static IEnumerable<string> ResolveTargets(ProjectConfig config, CleanCommandOptions options)
    {
        yield return ResolveArtifactPath(config.ProjectRoot, config.BuildPath, options.Target);

        if (options.IncludeDist)
        {
            yield return ResolveArtifactPath(config.ProjectRoot, config.DistPath, options.Target);
        }
    }

    private static string ResolveArtifactPath(string projectRoot, string configuredPath, string? target)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new InvalidOperationException("Clean target path is empty.");
        }

        var root = Path.GetFullPath(projectRoot);
        var basePath = Path.GetFullPath(Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(root, configuredPath));
        var path = string.IsNullOrWhiteSpace(target)
            ? basePath
            : Path.GetFullPath(Path.Combine(basePath, target));

        if (IsSamePath(path, root))
        {
            throw new InvalidOperationException("Clean target path resolves to the project root.");
        }

        return path;
    }

    private static void DeleteIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
            return;
        }

        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static bool IsSamePath(string left, string right)
    {
        return string.Equals(
            left.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            right.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }
}
