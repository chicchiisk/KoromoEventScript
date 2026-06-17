using KoromoEventScript.Cli.Build;
using KoromoEventScript.Cli.Diagnostics;
using KoromoEventScript.Cli.Localization;

namespace KoromoEventScript.Cli.Commands.Correct;

public class CorrectCommand
{
    private readonly ScriptPreparationService scriptPreparationService;
    private readonly TagAssignmentPlanner planner;
    private readonly ScriptRewriteService rewriteService;
    private readonly CorrectPreviewFormatter previewFormatter;

    public CorrectCommand()
        : this(
            new ScriptPreparationService(),
            new TagAssignmentPlanner(),
            new ScriptRewriteService(),
            new CorrectPreviewFormatter())
    {
    }

    public CorrectCommand(
        ScriptPreparationService scriptPreparationService,
        TagAssignmentPlanner planner,
        ScriptRewriteService rewriteService,
        CorrectPreviewFormatter previewFormatter)
    {
        this.scriptPreparationService = scriptPreparationService;
        this.planner = planner;
        this.rewriteService = rewriteService;
        this.previewFormatter = previewFormatter;
    }

    public virtual CorrectCommandResult Execute(CorrectCommandOptions options, string currentDirectory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentDirectory);

        var preparation = scriptPreparationService.Prepare(
            new ScriptPreparationRequest(
                options.ProjectDirectory,
                options.EntryPath,
                WarningsAsErrors: false),
            currentDirectory);
        if (!preparation.Succeeded)
        {
            return new CorrectCommandResult(preparation.ExitCode, preparation.Diagnostics);
        }

        var documents = preparation.SemanticResult!.ImportGraph?.OrderedDocuments ?? [];
        var plan = planner.BuildPlan(preparation.Config!, documents);
        if (options.CheckOnly)
        {
            return new CorrectCommandResult(CliExitCode.Success, preparation.Diagnostics, previewFormatter.Format(plan));
        }

        var rewriteResult = rewriteService.Apply(preparation.Config!, plan);
        if (!rewriteResult.Succeeded)
        {
            return new CorrectCommandResult(rewriteResult.ExitCode, rewriteResult.Diagnostics);
        }

        return new CorrectCommandResult(CliExitCode.Success, preparation.Diagnostics);
    }
}
