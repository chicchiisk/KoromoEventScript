using KoromoEventScript.Cli.Build;
using KoromoEventScript.Cli.Localization;

namespace KoromoEventScript.Cli.Commands.Loc;

public sealed class LocCommand
{
    private const string DefaultDictionaryFileName = "localization.csv";

    private readonly ScriptPreparationService scriptPreparationService;
    private readonly TagAssignmentPlanner planner;
    private readonly ScriptRewriteService rewriteService;
    private readonly LocalizationDictionaryExportService exportService;

    public LocCommand()
        : this(
            new ScriptPreparationService(),
            new TagAssignmentPlanner(),
            new ScriptRewriteService(),
            new LocalizationDictionaryExportService())
    {
    }

    public LocCommand(
        ScriptPreparationService scriptPreparationService,
        TagAssignmentPlanner planner,
        ScriptRewriteService rewriteService,
        LocalizationDictionaryExportService exportService)
    {
        this.scriptPreparationService = scriptPreparationService;
        this.planner = planner;
        this.rewriteService = rewriteService;
        this.exportService = exportService;
    }

    public LocCommandResult Execute(LocCommandOptions options, string currentDirectory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentDirectory);

        var preparation = scriptPreparationService.Prepare(
            new ScriptPreparationRequest(
                options.ProjectDirectory,
                EntryPath: null,
                WarningsAsErrors: false),
            currentDirectory);
        if (!preparation.Succeeded)
        {
            return new LocCommandResult(preparation.ExitCode, preparation.Diagnostics);
        }

        var config = preparation.Config!;
        var documents = preparation.SemanticResult!.ImportGraph?.OrderedDocuments ?? [];
        var plan = planner.BuildPlan(config, documents);
        if (plan.HasChanges)
        {
            var rewriteResult = rewriteService.Apply(config, plan);
            if (!rewriteResult.Succeeded)
            {
                return new LocCommandResult(rewriteResult.ExitCode, rewriteResult.Diagnostics);
            }
        }

        var outputPath = ResolveOutputPath(config.ProjectRoot, options.OutputPath);
        var exportResult = exportService.Export(new LocalizationExportRequest(
            config,
            documents,
            plan,
            options.RequestedLocales,
            outputPath));
        if (!exportResult.Succeeded)
        {
            return new LocCommandResult(exportResult.ExitCode, exportResult.Diagnostics);
        }

        var displayPath = Path.GetRelativePath(config.ProjectRoot, exportResult.OutputPath).Replace('\\', '/');
        if (!displayPath.Contains('/'))
        {
            displayPath = $"./{displayPath}";
        }

        return new LocCommandResult(CliExitCode.Success, [], $"Exported localization dictionary: {displayPath}");
    }

    private static string ResolveOutputPath(string projectRoot, string? outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return Path.Combine(projectRoot, DefaultDictionaryFileName);
        }

        return Path.IsPathRooted(outputPath)
            ? Path.GetFullPath(outputPath)
            : Path.GetFullPath(Path.Combine(projectRoot, outputPath));
    }
}
