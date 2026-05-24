using System.Xml.Linq;
using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.ProjectSystem;

public sealed class ProjectConfigLoader
{
    public ProjectConfigLoadResult Load(string projectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        var configPath = Path.Combine(projectRoot, "kes.xml");
        try
        {
            var document = XDocument.Load(configPath, LoadOptions.SetLineInfo);
            var root = document.Root;
            var project = root?.Element("Project");
            var paths = root?.Element("Paths");

            var entryPath = RequiredAttribute(project, "Entry");
            var eventsPath = RequiredAttribute(paths, "Events");
            var assetsPath = RequiredAttribute(paths, "Assets");
            var localePath = RequiredAttribute(paths, "Locale");
            var buildPath = RequiredAttribute(paths, "Build");
            var distPath = RequiredAttribute(paths, "Dist");

            var config = new ProjectConfig(
                Path.GetFullPath(projectRoot),
                entryPath,
                eventsPath,
                assetsPath,
                localePath,
                buildPath,
                distPath);
            return new ProjectConfigLoadResult(config, null, true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Failure("KES9002", $"Could not read kes.xml: {exception.Message}");
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.Xml.XmlException)
        {
            return Failure("KES9003", $"Invalid kes.xml: {exception.Message}");
        }
    }

    private static string RequiredAttribute(XElement? element, string attributeName)
    {
        var value = element?.Attribute(attributeName)?.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Missing required attribute '{attributeName}'.");
        }

        return value;
    }

    private static ProjectConfigLoadResult Failure(string code, string message)
    {
        return new ProjectConfigLoadResult(
            null,
            new Diagnostic(DiagnosticLevel.Error, code, "kes.xml", 1, 1, message),
            false);
    }
}
