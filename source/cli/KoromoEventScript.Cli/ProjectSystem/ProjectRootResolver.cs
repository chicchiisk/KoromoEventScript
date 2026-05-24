using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.ProjectSystem;

public sealed class ProjectRootResolver
{
    public ProjectRootResolveResult Resolve(string? explicitProjectDirectory, string currentDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentDirectory);

        if (!string.IsNullOrWhiteSpace(explicitProjectDirectory))
        {
            var root = Path.GetFullPath(Path.IsPathRooted(explicitProjectDirectory)
                ? explicitProjectDirectory
                : Path.Combine(currentDirectory, explicitProjectDirectory));

            if (Directory.Exists(root) && File.Exists(Path.Combine(root, "kes.xml")))
            {
                return new ProjectRootResolveResult(root, null, true);
            }

            return Failure($"Project directory '{explicitProjectDirectory}' does not contain kes.xml.");
        }

        var directory = new DirectoryInfo(Path.GetFullPath(currentDirectory));
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "kes.xml")))
            {
                return new ProjectRootResolveResult(directory.FullName, null, true);
            }

            directory = directory.Parent;
        }

        return Failure("Could not find kes.xml in the current directory or any parent directory.");
    }

    private static ProjectRootResolveResult Failure(string message)
    {
        return new ProjectRootResolveResult(
            null,
            new Diagnostic(DiagnosticLevel.Error, "KES9002", "kes.xml", 1, 1, message),
            false);
    }
}
