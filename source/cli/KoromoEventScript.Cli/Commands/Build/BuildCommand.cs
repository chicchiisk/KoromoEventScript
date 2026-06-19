using KoromoEventScript.Cli.Build;
using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Commands.Build;

public sealed class BuildCommand
{
    private readonly BuildPipelineService pipelineService;

    public BuildCommand()
        : this(new BuildPipelineService())
    {
    }

    public BuildCommand(BuildPipelineService pipelineService)
    {
        this.pipelineService = pipelineService;
    }

    public BuildCommandResult Execute(BuildCommandOptions options, string currentDirectory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentDirectory);

        var result = pipelineService.Run(new BuildPipelineRequest(options, currentDirectory, ValidateOnly: false));
        return new BuildCommandResult(result.ExitCode, result.Diagnostics);
    }
}

public sealed record BuildCommandResult(
    CliExitCode ExitCode,
    IReadOnlyList<Diagnostic> Diagnostics);
