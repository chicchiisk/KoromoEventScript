using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Commands.Run;

public sealed record BuildManifestReadResult(
    bool Succeeded,
    RunBuildManifestDocument? Document,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    public static BuildManifestReadResult Success(RunBuildManifestDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return new BuildManifestReadResult(true, document, []);
    }

    public static BuildManifestReadResult Failure(IReadOnlyList<Diagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        return new BuildManifestReadResult(false, null, diagnostics);
    }
}
