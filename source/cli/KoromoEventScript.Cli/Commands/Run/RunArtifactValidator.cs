using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Commands.Run;

public sealed class RunArtifactValidator
{
    private readonly BuildManifestReader manifestReader;

    public RunArtifactValidator()
        : this(new BuildManifestReader())
    {
    }

    public RunArtifactValidator(BuildManifestReader manifestReader)
    {
        this.manifestReader = manifestReader ?? throw new ArgumentNullException(nameof(manifestReader));
    }

    public RunArtifactValidationResult Validate(string manifestPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);

        var fullManifestPath = Path.GetFullPath(manifestPath);
        var readResult = manifestReader.Read(fullManifestPath);
        if (!readResult.Succeeded)
        {
            return RunArtifactValidationResult.Failure(
                CliExitCode.FileOrDirectoryError,
                readResult.Diagnostics,
                null,
                []);
        }

        var manifest = readResult.Document!;
        if (!string.Equals(manifest.Target, "windows", StringComparison.OrdinalIgnoreCase))
        {
            return RunArtifactValidationResult.Failure(
                CliExitCode.FileOrDirectoryError,
                [Diagnostic("KES9003", fullManifestPath, $"manifest.json target must be 'windows' for 'kes run', but was '{manifest.Target}'.")],
                manifest,
                []);
        }

        var manifestDirectory = Path.GetDirectoryName(fullManifestPath)!;
        var resolvedKlibPaths = manifest.Scripts
            .Select(script => ResolvePath(manifestDirectory, script.KlibPath))
            .ToArray();
        var missingDiagnostics = resolvedKlibPaths
            .Where(static path => !File.Exists(path))
            .Select(static path => Diagnostic("KES9002", path, $"Required .klib artifact was not found: {path}"))
            .ToArray();

        if (missingDiagnostics.Length > 0)
        {
            return RunArtifactValidationResult.Failure(
                CliExitCode.FileOrDirectoryError,
                missingDiagnostics,
                manifest,
                resolvedKlibPaths);
        }

        return RunArtifactValidationResult.Success(manifest, resolvedKlibPaths);
    }

    private static string ResolvePath(string manifestDirectory, string path)
    {
        return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(manifestDirectory, path));
    }

    private static Diagnostic Diagnostic(string code, string path, string message)
    {
        return new Diagnostic(DiagnosticLevel.Error, code, path.Replace('\\', '/'), 1, 1, message);
    }
}
