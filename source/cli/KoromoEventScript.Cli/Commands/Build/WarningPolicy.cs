using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Commands.Build;

public static class WarningPolicy
{
    public static CliExitCode Apply(
        CliExitCode currentExitCode,
        IReadOnlyList<Diagnostic> diagnostics,
        bool warningsAsErrors)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        if (currentExitCode != CliExitCode.Success || !warningsAsErrors)
        {
            return currentExitCode;
        }

        return diagnostics.Any(static diagnostic => diagnostic.Level == DiagnosticLevel.Warning)
            ? CliExitCode.WarningsAsErrors
            : currentExitCode;
    }
}
