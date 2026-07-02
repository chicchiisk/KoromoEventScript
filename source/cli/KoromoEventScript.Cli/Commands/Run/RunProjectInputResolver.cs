using KoromoEventScript.Cli.Diagnostics;
using KoromoEventScript.Cli.ProjectSystem;

namespace KoromoEventScript.Cli.Commands.Run;

public sealed class RunProjectInputResolver
{
    private readonly ProjectRootResolver projectRootResolver;
    private readonly ProjectConfigLoader projectConfigLoader;

    public RunProjectInputResolver()
        : this(new ProjectRootResolver(), new ProjectConfigLoader())
    {
    }

    public RunProjectInputResolver(ProjectRootResolver projectRootResolver, ProjectConfigLoader projectConfigLoader)
    {
        this.projectRootResolver = projectRootResolver ?? throw new ArgumentNullException(nameof(projectRootResolver));
        this.projectConfigLoader = projectConfigLoader ?? throw new ArgumentNullException(nameof(projectConfigLoader));
    }

    public RunProjectInputResult Resolve(string? projectDirectory, string currentDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentDirectory);

        if (!string.IsNullOrWhiteSpace(projectDirectory))
        {
            var fileDiagnostic = DiagnoseExplicitFileInput(projectDirectory, currentDirectory);
            if (fileDiagnostic is not null)
            {
                return RunProjectInputResult.Failure(CliExitCode.CommandLineError, [fileDiagnostic]);
            }
        }

        var rootResult = projectRootResolver.Resolve(projectDirectory, currentDirectory);
        if (!rootResult.Succeeded)
        {
            return RunProjectInputResult.Failure(CliExitCode.FileOrDirectoryError, [rootResult.Diagnostic!]);
        }

        var configResult = projectConfigLoader.Load(rootResult.ProjectRoot!);
        if (!configResult.Succeeded)
        {
            return RunProjectInputResult.Failure(CliExitCode.FileOrDirectoryError, [configResult.Diagnostic!]);
        }

        var config = configResult.Config!;
        var projectRoot = Path.GetFullPath(rootResult.ProjectRoot!);
        var entryPath = config.EntryPath;
        var entryFullPath = Path.GetFullPath(Path.Combine(projectRoot, entryPath));
        if (!File.Exists(entryFullPath))
        {
            return RunProjectInputResult.Failure(
                CliExitCode.FileOrDirectoryError,
                [new Diagnostic(
                    DiagnosticLevel.Error,
                    "KES9002",
                    entryPath,
                    1,
                    1,
                    $"Project.Entry '{entryPath}' does not exist under the project root.")]);
        }

        var manifestPath = Path.GetFullPath(Path.Combine(projectRoot, config.BuildPath, "windows", "manifest.json"));
        return RunProjectInputResult.Success(new RunProjectInput(
            projectRoot,
            config,
            entryPath,
            entryFullPath,
            manifestPath));
    }

    private static Diagnostic? DiagnoseExplicitFileInput(string projectDirectory, string currentDirectory)
    {
        var fullPath = Path.GetFullPath(Path.IsPathRooted(projectDirectory)
            ? projectDirectory
            : Path.Combine(currentDirectory, projectDirectory));
        if (!File.Exists(fullPath))
        {
            return null;
        }

        var extension = Path.GetExtension(fullPath);
        var message = extension.ToLowerInvariant() switch
        {
            ".kc" => ".kc file input is no longer supported by 'kes run'. Specify a project directory containing kes.xml.",
            ".kel" => ".kel file input is no longer supported by 'kes run'. Specify a project directory containing kes.xml.",
            _ => "Specify a project directory containing kes.xml; file input is not supported by 'kes run'.",
        };

        return new Diagnostic(DiagnosticLevel.Error, "KES9001", fullPath, 1, 1, message);
    }
}
