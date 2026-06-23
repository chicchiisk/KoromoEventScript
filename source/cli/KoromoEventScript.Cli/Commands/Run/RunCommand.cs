using KoromoEventScript.Cli.Build;
using KoromoEventScript.Cli.Commands.Build;
using KoromoEventScript.Cli.Diagnostics;
using KoromoEventScript.Cli.ProjectSystem;

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
            DefaultRuntimeExecutablePath)
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
        this.runtimeExecutablePathProvider = runtimeExecutablePathProvider ?? DefaultRuntimeExecutablePath;
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

        var arguments = BuildRuntimeArguments(options, manifestResult.ManifestPath!);
        try
        {
            var exitCode = processLauncher.Launch(new ProcessLaunchRequest(
                runtimeExecutablePathProvider(),
                arguments,
                Path.GetDirectoryName(manifestResult.ManifestPath!)!));

            return new RunCommandResult(exitCode, manifestResult.Diagnostics);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new RunCommandResult(
                (int)CliExitCode.FileOrDirectoryError,
                [new Diagnostic(DiagnosticLevel.Error, "KES9004", manifestResult.ManifestPath!, 1, 1, $"Could not launch Windows runtime: {exception.Message}")]);
        }
    }

    private RunManifestResolveResult ResolveManifestPath(RunCommandOptions options, string currentDirectory)
    {
        if (!string.IsNullOrWhiteSpace(options.ManifestPath))
        {
            return RunManifestResolveResult.Success(Path.GetFullPath(Path.IsPathRooted(options.ManifestPath)
                ? options.ManifestPath
                : Path.Combine(currentDirectory, options.ManifestPath)));
        }

        if (!options.NoBuild)
        {
            var buildOptions = new BuildCommandOptions(
                options.ProjectDirectory,
                options.OutputFormat,
                Target: "windows");
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

    private static IReadOnlyList<string> BuildRuntimeArguments(RunCommandOptions options, string manifestPath)
    {
        var arguments = new List<string>
        {
            "--manifest",
            manifestPath,
        };

        AddValue(arguments, "--locale", options.Locale);
        AddValue(arguments, "--start", options.Start);
        if (options.Fullscreen)
        {
            arguments.Add("--fullscreen");
        }

        AddValue(arguments, "--width", options.Width?.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AddValue(arguments, "--height", options.Height?.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (options.Debug)
        {
            arguments.Add("--debug");
        }

        if (options.Profile)
        {
            arguments.Add("--profile");
        }

        arguments.AddRange(options.RuntimeArguments ?? []);
        return arguments;
    }

    private static void AddValue(List<string> arguments, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        arguments.Add(name);
        arguments.Add(value);
    }

    private static string DefaultRuntimeExecutablePath()
    {
        var candidate = Path.Combine(AppContext.BaseDirectory, "KoromoEventScript.Runtime.Windows.exe");
        return File.Exists(candidate) ? candidate : "KoromoEventScript.Runtime.Windows.exe";
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
