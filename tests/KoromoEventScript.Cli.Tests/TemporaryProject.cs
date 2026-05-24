namespace KoromoEventScript.Cli.Tests;

internal sealed class TemporaryProject : IDisposable
{
    private TemporaryProject(string root)
    {
        Root = root;
    }

    public string Root { get; }

    public static TemporaryProject Create()
    {
        var root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "tmp", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return new TemporaryProject(root);
    }

    public void WriteConfig(string entry = "events/main.kel")
    {
        WriteFile("kes.xml", $$"""
<?xml version="1.0" encoding="utf-8"?>
<KoromoEventScript>
    <Project Name="Temp" Version="0.1.0" Entry="{{entry}}" />
    <Paths Events="events" Assets="assets" Locale="locale" Build="build" Dist="dist" />
</KoromoEventScript>
""");
    }

    public void WriteFile(string relativePath, string contents)
    {
        var path = Path.Combine(Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents.Replace("\r\n", "\n"));
    }

    public IReadOnlyDictionary<string, string> SnapshotFiles()
    {
        return Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(Root, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToDictionary(path => path, path => File.ReadAllText(Path.Combine(Root, path)), StringComparer.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
