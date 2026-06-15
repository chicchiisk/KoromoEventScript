using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.ProjectSystem;

public sealed class ProjectScaffoldWriter
{
    public ProjectScaffoldWriteResult Write(ProjectScaffold scaffold, bool force)
    {
        ArgumentNullException.ThrowIfNull(scaffold);

        var diagnostics = new List<Diagnostic>();
        var projectRoot = scaffold.ProjectRoot;

        if (File.Exists(projectRoot))
        {
            diagnostics.Add(Error(projectRoot, $"Could not create project directory '{projectRoot}' because a file exists at that path."));
            return new ProjectScaffoldWriteResult(false, diagnostics);
        }

        Directory.CreateDirectory(projectRoot);

        foreach (var relativeDirectory in scaffold.Directories)
        {
            var absoluteDirectory = Path.Combine(projectRoot, relativeDirectory);
            if (File.Exists(absoluteDirectory))
            {
                diagnostics.Add(Error(relativeDirectory, $"Could not create directory '{relativeDirectory}' because a file exists at that path."));
                return new ProjectScaffoldWriteResult(false, diagnostics);
            }

            Directory.CreateDirectory(absoluteDirectory);
        }

        foreach (var file in scaffold.Files)
        {
            var absolutePath = Path.Combine(projectRoot, file.RelativePath);
            var parentDirectory = Path.GetDirectoryName(absolutePath)!;
            if (File.Exists(parentDirectory))
            {
                diagnostics.Add(Error(file.RelativePath, $"Could not create parent directory for '{file.RelativePath}'."));
                return new ProjectScaffoldWriteResult(false, diagnostics);
            }

            if (File.Exists(absolutePath) && !force)
            {
                diagnostics.Add(Error(file.RelativePath, $"File '{file.RelativePath}' already exists. Re-run with --force to overwrite managed scaffold files."));
                return new ProjectScaffoldWriteResult(false, diagnostics);
            }

            try
            {
                Directory.CreateDirectory(parentDirectory);
                File.WriteAllText(absolutePath, file.Contents.Replace("\r\n", "\n"));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                diagnostics.Add(Error(file.RelativePath, $"Could not write '{file.RelativePath}': {exception.Message}"));
                return new ProjectScaffoldWriteResult(false, diagnostics);
            }
        }

        return new ProjectScaffoldWriteResult(true, []);
    }

    private static Diagnostic Error(string path, string message)
    {
        return new Diagnostic(DiagnosticLevel.Error, "KES9002", path.Replace('\\', '/'), 1, 1, message);
    }
}
