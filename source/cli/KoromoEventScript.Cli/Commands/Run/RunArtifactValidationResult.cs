using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Commands.Run;

public sealed record RunArtifactValidationResult(
    bool Succeeded,
    RunBuildManifestDocument? Manifest,
    IReadOnlyList<string> ResolvedKlibPaths,
    CliExitCode ExitCode,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    public static RunArtifactValidationResult Success(
        RunBuildManifestDocument manifest,
        IReadOnlyList<string> resolvedKlibPaths)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(resolvedKlibPaths);

        return new RunArtifactValidationResult(true, manifest, resolvedKlibPaths, CliExitCode.Success, []);
    }

    public static RunArtifactValidationResult Failure(
        CliExitCode exitCode,
        IReadOnlyList<Diagnostic> diagnostics,
        RunBuildManifestDocument? manifest,
        IReadOnlyList<string> resolvedKlibPaths)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(resolvedKlibPaths);

        return new RunArtifactValidationResult(false, manifest, resolvedKlibPaths, exitCode, diagnostics);
    }
}
