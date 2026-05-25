namespace KoromoEventScript.Cli.Semantics;

public enum ModuleFileMatchKind
{
    Missing,
    Found,
    Ambiguous,
}

public sealed record ModuleFileEntry
{
    private ModuleFileEntry(string moduleName, string projectRelativePath, string fullPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRelativePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        ModuleName = moduleName;
        ProjectRelativePath = projectRelativePath.Replace('\\', '/');
        FullPath = fullPath;
    }

    public string ModuleName { get; }

    public string ProjectRelativePath { get; }

    public string FullPath { get; }

    public static ModuleFileEntry FromPath(string projectRoot, string fullPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        var moduleName = Path.GetFileNameWithoutExtension(fullPath);
        var projectRelativePath = Path.GetRelativePath(projectRoot, fullPath);

        return new ModuleFileEntry(moduleName, projectRelativePath, Path.GetFullPath(fullPath));
    }
}

public sealed record ModuleFileMatch
{
    private ModuleFileMatch(
        ModuleFileMatchKind kind,
        string moduleName,
        ModuleFileEntry? file,
        IReadOnlyList<ModuleFileEntry> candidates)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        ArgumentNullException.ThrowIfNull(candidates);

        Kind = kind;
        ModuleName = moduleName;
        File = file;
        Candidates = candidates.ToArray();
    }

    public ModuleFileMatchKind Kind { get; }

    public string ModuleName { get; }

    public ModuleFileEntry? File { get; }

    public IReadOnlyList<ModuleFileEntry> Candidates { get; }

    public static ModuleFileMatch Missing(string moduleName)
    {
        return new ModuleFileMatch(ModuleFileMatchKind.Missing, moduleName, null, []);
    }

    public static ModuleFileMatch Found(string moduleName, ModuleFileEntry file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return new ModuleFileMatch(ModuleFileMatchKind.Found, moduleName, file, [file]);
    }

    public static ModuleFileMatch Ambiguous(string moduleName, IReadOnlyList<ModuleFileEntry> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (candidates.Count < 2)
        {
            throw new ArgumentException("Ambiguous module matches require at least two candidates.", nameof(candidates));
        }

        return new ModuleFileMatch(ModuleFileMatchKind.Ambiguous, moduleName, null, candidates);
    }
}

public sealed record ModuleFileIndexResult(ModuleFileIndex Index);
