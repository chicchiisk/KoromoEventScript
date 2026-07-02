using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Commands.Run;

public sealed record RunStalenessResult(
    bool Succeeded,
    bool IsStale,
    CliExitCode ExitCode,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    public static RunStalenessResult Fresh()
    {
        return new RunStalenessResult(true, false, CliExitCode.Success, []);
    }

    public static RunStalenessResult Stale()
    {
        return new RunStalenessResult(true, true, CliExitCode.Success, []);
    }

    public static RunStalenessResult Failure(CliExitCode exitCode, IReadOnlyList<Diagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        return new RunStalenessResult(false, false, exitCode, diagnostics);
    }
}
