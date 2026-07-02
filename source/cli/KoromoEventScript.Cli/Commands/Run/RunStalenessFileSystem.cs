namespace KoromoEventScript.Cli.Commands.Run;

public class RunStalenessFileSystem
{
    public virtual bool FileExists(string path)
    {
        return File.Exists(path);
    }

    public virtual bool DirectoryExists(string path)
    {
        return Directory.Exists(path);
    }

    public virtual IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption)
    {
        return Directory.EnumerateFiles(path, searchPattern, searchOption);
    }

    public virtual DateTimeOffset GetLastWriteTimeUtc(string path)
    {
        return new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
    }
}
