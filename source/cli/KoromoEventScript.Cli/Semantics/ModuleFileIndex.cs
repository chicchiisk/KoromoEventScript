using System.Collections.ObjectModel;
using KoromoEventScript.Cli.ProjectSystem;

namespace KoromoEventScript.Cli.Semantics;

public sealed class ModuleFileIndex
{
    private static readonly string[] SupportedExtensions = [".ke", ".kc"];
    private readonly IReadOnlyDictionary<string, IReadOnlyList<ModuleFileEntry>> filesByModule;

    public ModuleFileIndex()
        : this(new Dictionary<string, IReadOnlyList<ModuleFileEntry>>(StringComparer.Ordinal))
    {
    }

    private ModuleFileIndex(IReadOnlyDictionary<string, IReadOnlyList<ModuleFileEntry>> filesByModule)
    {
        this.filesByModule = filesByModule;
    }

    public ModuleFileIndexResult Build(ProjectConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var eventsPath = ResolveProjectPath(config.ProjectRoot, config.EventsPath);

        if (!Directory.Exists(eventsPath))
        {
            return new ModuleFileIndexResult(new ModuleFileIndex());
        }

        var entries = Directory
            .EnumerateFiles(eventsPath, "*.*", SearchOption.AllDirectories)
            .Where(static path => SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.Ordinal))
            .Select(path => ModuleFileEntry.FromPath(config.ProjectRoot, path))
            .OrderBy(static entry => entry.ProjectRelativePath, StringComparer.Ordinal)
            .ToArray();

        var filesByModule = entries
            .GroupBy(static entry => entry.ModuleName, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => (IReadOnlyList<ModuleFileEntry>)group.ToArray(),
                StringComparer.Ordinal);

        return new ModuleFileIndexResult(new ModuleFileIndex(
            new ReadOnlyDictionary<string, IReadOnlyList<ModuleFileEntry>>(filesByModule)));
    }

    public ModuleFileMatch FindModule(string moduleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

        if (!filesByModule.TryGetValue(moduleName, out var candidates))
        {
            return ModuleFileMatch.Missing(moduleName);
        }

        return candidates.Count == 1
            ? ModuleFileMatch.Found(moduleName, candidates[0])
            : ModuleFileMatch.Ambiguous(moduleName, candidates);
    }

    private static string ResolveProjectPath(string projectRoot, string path)
    {
        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(projectRoot, path));
    }
}
