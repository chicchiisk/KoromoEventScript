using KoromoEventScript.Runtime.Core.Klib;

namespace KoromoEventScript.Cli.Compilation;

public sealed record KlibCompilationResult(
    KlibDocument? Document,
    IReadOnlyList<KoromoEventScript.Cli.Diagnostics.Diagnostic> Diagnostics)
{
    public bool Succeeded => Document is not null && Diagnostics.Count == 0;

    public static KlibCompilationResult Success(KlibDocument document)
    {
        return new KlibCompilationResult(document, []);
    }

    public static KlibCompilationResult Failure(IReadOnlyList<KoromoEventScript.Cli.Diagnostics.Diagnostic> diagnostics)
    {
        return new KlibCompilationResult(null, diagnostics.ToArray());
    }
}
