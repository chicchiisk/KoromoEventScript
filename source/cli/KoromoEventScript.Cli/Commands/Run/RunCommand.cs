using KoromoEventScript.Cli.Build;
using KoromoEventScript.Cli.Commands.Build;
using KoromoEventScript.Cli.Diagnostics;
using KoromoEventScript.Cli.ProjectSystem;
using System.ComponentModel;

namespace KoromoEventScript.Cli.Commands.Run;

public sealed class RunCommand
{
    private readonly BuildPipelineService pipelineService;
    private readonly ProjectRootResolver projectRootResolver;
    private readonly ProjectConfigLoader projectConfigLoader;
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
    {
        this.pipelineService = pipelineService;
        this.projectRootResolver = projectRootResolver;
        this.projectConfigLoader = projectConfigLoader;
        this.processLauncher = processLauncher;
        this.runtimeExecutablePathProvider = runtimeExecutablePathProvider ?? (() => new RuntimeCommandResolver().Resolve());
    }

    public RunCommandResult Execute(RunCommandOptions options, string currentDirectory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentDirectory);

        var manifestResult = ResolveManifestPath(options, currentDirectory);
        if (!manifestResult.Succeeded)
        {
            return new RunCommandResult((int)manifestResult.ExitCode, manifestResult.Diagnostics);
        }

        try
        {
            var request = new RuntimeLaunchAdapter().Create(
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

    private RunManifestResolveResult ResolveManifestPath(RunCommandOptions options, string currentDirectory)
    {
        if (options.BuildMode != RunBuildMode.Never)
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

            return RunManifestResolveResult.Success(buildResult.ManifestPath!);
        }

        var rootResult = projectRootResolver.Resolve(options.ProjectDirectory, currentDirectory);
        if (!rootResult.Succeeded)
        {
            return RunManifestResolveResult.Failure(CliExitCode.FileOrDirectoryError, [rootResult.Diagnostic!]);
        }

        var configResult = projectConfigLoader.Load(rootResult.ProjectRoot!);
        if (!configResult.Succeeded)
        {
            return RunManifestResolveResult.Failure(CliExitCode.FileOrDirectoryError, [configResult.Diagnostic!]);
        }

        return RunManifestResolveResult.Success(Path.Combine(configResult.Config!.ProjectRoot, configResult.Config.BuildPath, "windows", "manifest.json"));
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
