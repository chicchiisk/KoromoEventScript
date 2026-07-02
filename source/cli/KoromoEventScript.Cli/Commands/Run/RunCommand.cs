using KoromoEventScript.Cli.Build;
using KoromoEventScript.Cli.Commands.Build;
using KoromoEventScript.Cli.Diagnostics;
using KoromoEventScript.Cli.ProjectSystem;
using System.ComponentModel;

namespace KoromoEventScript.Cli.Commands.Run;

public sealed class RunCommand
{
    private readonly BuildPipelineService pipelineService;
    private readonly RunProjectInputResolver inputResolver;
    private readonly RunStalenessChecker stalenessChecker;
    private readonly RunArtifactValidator artifactValidator;
    private readonly RuntimeLaunchAdapter launchAdapter;
    private readonly IProcessLauncher processLauncher;
    private readonly Func<string> runtimeExecutablePathProvider;

    public RunCommand()
        : this(
            new BuildPipelineService(),
            new ProjectRootResolver(),
            new ProjectConfigLoader(),
            new ProcessLauncher(),
            () => new RuntimeCommandResolver().Resolve())
    {
    }

    public RunCommand(
        BuildPipelineService pipelineService,
        ProjectRootResolver projectRootResolver,
        ProjectConfigLoader projectConfigLoader,
        IProcessLauncher processLauncher,
        Func<string>? runtimeExecutablePathProvider = null)
        : this(
            pipelineService,
            new RunProjectInputResolver(projectRootResolver, projectConfigLoader),
            new RunStalenessChecker(),
            new RunArtifactValidator(),
            new RuntimeLaunchAdapter(),
            processLauncher,
            runtimeExecutablePathProvider)
    {
    }

    public RunCommand(
        BuildPipelineService pipelineService,
        RunProjectInputResolver inputResolver,
        RunStalenessChecker stalenessChecker,
        RunArtifactValidator artifactValidator,
        RuntimeLaunchAdapter launchAdapter,
        IProcessLauncher processLauncher,
        Func<string>? runtimeExecutablePathProvider = null)
    {
        this.pipelineService = pipelineService ?? throw new ArgumentNullException(nameof(pipelineService));
        this.inputResolver = inputResolver ?? throw new ArgumentNullException(nameof(inputResolver));
        this.stalenessChecker = stalenessChecker ?? throw new ArgumentNullException(nameof(stalenessChecker));
        this.artifactValidator = artifactValidator ?? throw new ArgumentNullException(nameof(artifactValidator));
        this.launchAdapter = launchAdapter ?? throw new ArgumentNullException(nameof(launchAdapter));
        this.processLauncher = processLauncher ?? throw new ArgumentNullException(nameof(processLauncher));
        this.runtimeExecutablePathProvider = runtimeExecutablePathProvider ?? (() => new RuntimeCommandResolver().Resolve());
    }

    public RunCommandResult Execute(RunCommandOptions options, string currentDirectory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentDirectory);

        var inputResult = inputResolver.Resolve(options.ProjectDirectory, currentDirectory);
        if (!inputResult.Succeeded)
        {
            return new RunCommandResult((int)inputResult.ExitCode, inputResult.Diagnostics);
        }

        var manifestResult = ResolveManifestPath(options, currentDirectory, inputResult.Input!);
        if (!manifestResult.Succeeded)
        {
            return new RunCommandResult((int)manifestResult.ExitCode, manifestResult.Diagnostics);
        }

        try
        {
            var request = launchAdapter.Create(
                runtimeExecutablePathProvider(),
                manifestResult.ManifestPath!,
                options,
                currentDirectory);
            var exitCode = processLauncher.Launch(request);

            return new RunCommandResult(exitCode, manifestResult.Diagnostics);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception)
        {
            return new RunCommandResult(
                (int)CliExitCode.RuntimeLaunchError,
                [new Diagnostic(DiagnosticLevel.Error, "KES9004", manifestResult.ManifestPath!, 1, 1, $"Could not launch Windows runtime: {exception.Message}")]);
        }
    }

    private RunManifestResolveResult ResolveManifestPath(
        RunCommandOptions options,
        string currentDirectory,
        RunProjectInput input)
    {
        return options.BuildMode switch
        {
            RunBuildMode.Always => BuildThenValidate(options, currentDirectory, input),
            RunBuildMode.Never => ValidateExistingArtifacts(input.ManifestPath),
            RunBuildMode.IfStale => ResolveIfStale(options, currentDirectory, input),
            _ => throw new InvalidOperationException($"Unsupported run build mode: {options.BuildMode}"),
        };
    }

    private RunManifestResolveResult ResolveIfStale(
        RunCommandOptions options,
        string currentDirectory,
        RunProjectInput input)
    {
        var stalenessResult = stalenessChecker.Check(input);
        if (!stalenessResult.Succeeded)
        {
            return RunManifestResolveResult.Failure(stalenessResult.ExitCode, stalenessResult.Diagnostics);
        }

        return stalenessResult.IsStale
            ? BuildThenValidate(options, currentDirectory, input)
            : ValidateExistingArtifacts(input.ManifestPath);
    }

    private RunManifestResolveResult BuildThenValidate(
        RunCommandOptions options,
        string currentDirectory,
        RunProjectInput input)
    {
        var buildOptions = new BuildCommandOptions(
            options.ProjectDirectory,
            options.OutputFormat,
            Target: options.Target);
        var buildResult = pipelineService.Run(new BuildPipelineRequest(buildOptions, currentDirectory, ValidateOnly: false));
        if (buildResult.ExitCode != CliExitCode.Success)
        {
            return RunManifestResolveResult.Failure(buildResult.ExitCode, buildResult.Diagnostics);
        }

        return ValidateExistingArtifacts(buildResult.ManifestPath ?? input.ManifestPath);
    }

    private RunManifestResolveResult ValidateExistingArtifacts(string manifestPath)
    {
        var validationResult = artifactValidator.Validate(manifestPath);
        return validationResult.Succeeded
            ? RunManifestResolveResult.Success(manifestPath)
            : RunManifestResolveResult.Failure(validationResult.ExitCode, validationResult.Diagnostics);
    }

    private sealed record RunManifestResolveResult(
        bool Succeeded,
        CliExitCode ExitCode,
        IReadOnlyList<Diagnostic> Diagnostics,
        string? ManifestPath)
    {
        public static RunManifestResolveResult Success(string manifestPath)
        {
            return new RunManifestResolveResult(true, CliExitCode.Success, [], manifestPath);
        }

        public static RunManifestResolveResult Failure(CliExitCode exitCode, IReadOnlyList<Diagnostic> diagnostics)
        {
            return new RunManifestResolveResult(false, exitCode, diagnostics, null);
        }
    }
}

public sealed record RunCommandResult(
    int ExitCode,
    IReadOnlyList<Diagnostic> Diagnostics);
