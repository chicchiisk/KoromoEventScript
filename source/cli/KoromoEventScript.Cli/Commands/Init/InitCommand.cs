using KoromoEventScript.Cli.ProjectSystem;

namespace KoromoEventScript.Cli.Commands.Init;

public class InitCommand
{
    private readonly ProjectScaffoldFactory scaffoldFactory;
    private readonly ProjectScaffoldWriter scaffoldWriter;

    public InitCommand()
        : this(new ProjectScaffoldFactory(), new ProjectScaffoldWriter())
    {
    }

    public InitCommand(ProjectScaffoldFactory scaffoldFactory, ProjectScaffoldWriter scaffoldWriter)
    {
        this.scaffoldFactory = scaffoldFactory;
        this.scaffoldWriter = scaffoldWriter;
    }

    public virtual InitCommandResult Execute(InitCommandOptions options, string currentDirectory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentDirectory);

        var projectRoot = ResolveProjectRoot(options.ProjectDirectory, currentDirectory);
        var scaffold = scaffoldFactory.Create(options, projectRoot);
        var writeResult = scaffoldWriter.Write(scaffold, options.Force);
        if (!writeResult.Succeeded)
        {
            return new InitCommandResult(CliExitCode.FileOrDirectoryError, writeResult.Diagnostics, null, null);
        }

        return new InitCommandResult(
            CliExitCode.Success,
            [],
            $"Initialized KES project at '{projectRoot}'.",
            projectRoot);
    }

    private static string ResolveProjectRoot(string? projectDirectory, string currentDirectory)
    {
        if (string.IsNullOrWhiteSpace(projectDirectory))
        {
            return Path.GetFullPath(currentDirectory);
        }

        return Path.GetFullPath(
            Path.IsPathRooted(projectDirectory)
                ? projectDirectory
                : Path.Combine(currentDirectory, projectDirectory));
    }
}
