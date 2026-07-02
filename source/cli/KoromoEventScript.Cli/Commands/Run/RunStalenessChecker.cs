using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Commands.Run;

public sealed class RunStalenessChecker
{
    private readonly BuildManifestReader manifestReader;
    private readonly RunStalenessFileSystem fileSystem;

    public RunStalenessChecker()
        : this(new BuildManifestReader(), new RunStalenessFileSystem())
    {
    }

    public RunStalenessChecker(
        BuildManifestReader? manifestReader = null,
        RunStalenessFileSystem? fileSystem = null)
    {
        this.manifestReader = manifestReader ?? new BuildManifestReader();
        this.fileSystem = fileSystem ?? new RunStalenessFileSystem();
    }

    public RunStalenessResult Check(RunProjectInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var manifestPath = Path.GetFullPath(input.ManifestPath);
        if (!FileExists(manifestPath))
        {
            return RunStalenessResult.Stale();
        }

        var readResult = manifestReader.Read(manifestPath);
        if (!readResult.Succeeded)
        {
            return RunStalenessResult.Failure(CliExitCode.FileOrDirectoryError, readResult.Diagnostics);
        }

        var manifestDirectory = Path.GetDirectoryName(manifestPath)!;
        var artifactPaths = new List<string> { manifestPath };
        foreach (var script in readResult.Document!.Scripts)
        {
            var klibPath = ResolvePath(manifestDirectory, script.KlibPath);
            if (!FileExists(klibPath))
            {
                return RunStalenessResult.Stale();
            }

            artifactPaths.Add(klibPath);
        }

        var inputCandidates = EnumerateInputCandidates(input);
        if (inputCandidates.Diagnostics.Count > 0)
        {
            return RunStalenessResult.Failure(CliExitCode.FileOrDirectoryError, inputCandidates.Diagnostics);
        }

        var inputTimestamps = GetTimestamps(inputCandidates.Paths, inputCandidate: true);
        if (inputTimestamps.Diagnostics.Count > 0)
        {
            return RunStalenessResult.Failure(CliExitCode.FileOrDirectoryError, inputTimestamps.Diagnostics);
        }

        var artifactTimestamps = GetTimestamps(artifactPaths, inputCandidate: false);
        if (artifactTimestamps.Diagnostics.Count > 0)
        {
            return RunStalenessResult.Failure(CliExitCode.FileOrDirectoryError, artifactTimestamps.Diagnostics);
        }

        if (inputTimestamps.Values.Count == 0 || artifactTimestamps.Values.Count == 0)
        {
            return RunStalenessResult.Fresh();
        }

        var latestInput = inputTimestamps.Values.Max();
        var oldestArtifact = artifactTimestamps.Values.Min();
        return latestInput > oldestArtifact
            ? RunStalenessResult.Stale()
            : RunStalenessResult.Fresh();
    }

    private CandidateEnumerationResult EnumerateInputCandidates(RunProjectInput input)
    {
        var projectRoot = Path.GetFullPath(input.ProjectRoot);
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine(projectRoot, "kes.xml"),
            Path.GetFullPath(input.EntryFullPath),
        };
        var diagnostics = new List<Diagnostic>();

        AddDirectoryFiles(paths, diagnostics, ResolveProjectPath(projectRoot, input.Config.EventsPath), "*.kc");
        AddDirectoryFiles(paths, diagnostics, ResolveProjectPath(projectRoot, input.Config.AssetsPath), "*");
        AddDirectoryFiles(paths, diagnostics, ResolveProjectPath(projectRoot, input.Config.LocalePath), "*");

        return new CandidateEnumerationResult(paths.ToArray(), diagnostics);
    }

    private void AddDirectoryFiles(HashSet<string> paths, List<Diagnostic> diagnostics, string directory, string searchPattern)
    {
        try
        {
            if (!fileSystem.DirectoryExists(directory))
            {
                return;
            }

            foreach (var path in fileSystem.EnumerateFiles(directory, searchPattern, SearchOption.AllDirectories))
            {
                paths.Add(Path.GetFullPath(path));
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            diagnostics.Add(FileDiagnostic(
                "KES9002",
                directory,
                $"Could not enumerate input files for stale checking: {exception.Message}"));
        }
    }

    private TimestampResult GetTimestamps(IReadOnlyList<string> paths, bool inputCandidate)
    {
        var values = new List<DateTimeOffset>();
        var diagnostics = new List<Diagnostic>();

        foreach (var path in paths)
        {
            if (!FileExists(path))
            {
                if (inputCandidate)
                {
                    diagnostics.Add(FileDiagnostic(
                        "KES9002",
                        path,
                        $"Input file for stale checking was not found: {path}"));
                }

                continue;
            }

            try
            {
                values.Add(fileSystem.GetLastWriteTimeUtc(path));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                diagnostics.Add(FileDiagnostic(
                    "KES9002",
                    path,
                    $"Could not read input file timestamp for stale checking: {exception.Message}"));
            }
        }

        return new TimestampResult(values, diagnostics);
    }

    private bool FileExists(string path)
    {
        try
        {
            return fileSystem.FileExists(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string ResolveProjectPath(string projectRoot, string path)
    {
        return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(projectRoot, path));
    }

    private static string ResolvePath(string baseDirectory, string path)
    {
        return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(baseDirectory, path));
    }

    private static Diagnostic FileDiagnostic(string code, string path, string message)
    {
        return new Diagnostic(DiagnosticLevel.Error, code, NormalizePath(path), 1, 1, message);
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private sealed record CandidateEnumerationResult(
        IReadOnlyList<string> Paths,
        IReadOnlyList<Diagnostic> Diagnostics);

    private sealed record TimestampResult(
        IReadOnlyList<DateTimeOffset> Values,
        IReadOnlyList<Diagnostic> Diagnostics);
}
