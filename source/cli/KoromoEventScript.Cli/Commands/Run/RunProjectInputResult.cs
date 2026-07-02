using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Commands.Run;

public sealed record RunProjectInputResult(
    bool Succeeded,
    RunProjectInput? Input,
    CliExitCode ExitCode,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    public static RunProjectInputResult Success(RunProjectInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return new RunProjectInputResult(true, input, CliExitCode.Success, []);
    }

    public static RunProjectInputResult Failure(CliExitCode exitCode, IReadOnlyList<Diagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        return new RunProjectInputResult(false, null, exitCode, diagnostics);
    }
}
