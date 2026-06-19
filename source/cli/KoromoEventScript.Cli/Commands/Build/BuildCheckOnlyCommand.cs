using KoromoEventScript.Cli.Build;
using KoromoEventScript.Cli.Diagnostics;
using KoromoEventScript.Cli.ProjectSystem;
using KoromoEventScript.Cli.Semantics;

namespace KoromoEventScript.Cli.Commands.Build;

public sealed class BuildCheckOnlyCommand
{
    private readonly BuildPipelineService pipelineService;

    public BuildCheckOnlyCommand()
        : this(new BuildPipelineService())
    {
    }

    public BuildCheckOnlyCommand(BuildPipelineService pipelineService)
    {
        this.pipelineService = pipelineService;
    }

    public BuildCheckOnlyResult Execute(BuildCommandOptions options, string currentDirectory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentDirectory);

        var result = pipelineService.Run(new BuildPipelineRequest(options with { CheckOnly = true }, currentDirectory, ValidateOnly: true));
        return new BuildCheckOnlyResult(result.ExitCode, result.Diagnostics);
    }
}
