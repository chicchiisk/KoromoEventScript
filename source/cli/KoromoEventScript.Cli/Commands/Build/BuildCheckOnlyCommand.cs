using KoromoEventScript.Cli.Build;
using KoromoEventScript.Cli.Diagnostics;
using KoromoEventScript.Cli.ProjectSystem;
using KoromoEventScript.Cli.Semantics;

namespace KoromoEventScript.Cli.Commands.Build;

public sealed class BuildCheckOnlyCommand
{
    private readonly BuildPreparationService preparationService;

    public BuildCheckOnlyCommand()
        : this(new BuildPreparationService())
    {
    }

    public BuildCheckOnlyCommand(BuildPreparationService preparationService)
    {
        this.preparationService = preparationService;
    }

    public BuildCheckOnlyResult Execute(BuildCommandOptions options, string currentDirectory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentDirectory);

        var preparation = preparationService.Prepare(options with { CheckOnly = true }, currentDirectory);
        if (!preparation.Succeeded)
        {
            return new BuildCheckOnlyResult(preparation.ExitCode, preparation.Diagnostics);
        }

        return new BuildCheckOnlyResult(preparation.ExitCode, preparation.Diagnostics);
    }
}
