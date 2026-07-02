using System.Runtime.InteropServices;

namespace KoromoEventScript.Cli.Commands.Run;

public enum RuntimeArchitecture
{
    X64,
    Arm64,
}

public class RuntimeCommandFileSystem
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
        return Directory.Exists(path)
            ? Directory.EnumerateFiles(path, searchPattern, searchOption)
            : [];
    }
}

public sealed class RuntimeCommandResolver
{
    private const string RuntimeExecutableName = "KoromoEventScript.Runtime.Windows.exe";
    private const string RuntimeProjectName = "KoromoEventScript.Runtime.Windows.csproj";

    private readonly RuntimeCommandFileSystem fileSystem;
    private readonly Func<string> appBaseDirectoryProvider;
    private readonly Func<string> repositoryRootProvider;
    private readonly RuntimeArchitecture architecture;

    public RuntimeCommandResolver()
        : this(
            new RuntimeCommandFileSystem(),
            () => AppContext.BaseDirectory,
            ResolveDefaultRepositoryRoot,
            GetCurrentArchitecture())
    {
    }

    public RuntimeCommandResolver(
        RuntimeCommandFileSystem fileSystem,
        Func<string> appBaseDirectoryProvider,
        Func<string> repositoryRootProvider,
        RuntimeArchitecture architecture)
    {
        this.fileSystem = fileSystem;
        this.appBaseDirectoryProvider = appBaseDirectoryProvider;
        this.repositoryRootProvider = repositoryRootProvider;
        this.architecture = architecture;
    }

    public string Resolve()
    {
        var bundledExecutablePath = Path.Combine(appBaseDirectoryProvider(), RuntimeExecutableName);
        if (fileSystem.FileExists(bundledExecutablePath))
        {
            return bundledExecutablePath;
        }

        var runtimeProjectRoot = Path.Combine(repositoryRootProvider(), "source", "runtime", "KoromoEventScript.Runtime.Windows");
        var runtimeProjectPath = Path.Combine(runtimeProjectRoot, RuntimeProjectName);
        if (fileSystem.FileExists(runtimeProjectPath))
        {
            return runtimeProjectPath;
        }

        var runtimeIdentifier = architecture == RuntimeArchitecture.Arm64 ? "win-arm64" : "win-x64";
        foreach (var configuration in new[] { "Debug", "Release" })
        {
            var configurationDirectory = Path.Combine(runtimeProjectRoot, "bin", configuration);
            var executable = fileSystem
                .EnumerateFiles(configurationDirectory, RuntimeExecutableName, SearchOption.AllDirectories)
                .Where(path => ContainsPathSegment(path, runtimeIdentifier))
                .Order(StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (executable is not null)
            {
                return executable;
            }
        }

        return RuntimeExecutableName;
    }

    private static bool ContainsPathSegment(string path, string segment)
    {
        return path
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(part => string.Equals(part, segment, StringComparison.OrdinalIgnoreCase));
    }

    private static RuntimeArchitecture GetCurrentArchitecture()
    {
        return RuntimeInformation.ProcessArchitecture == Architecture.Arm64
            ? RuntimeArchitecture.Arm64
            : RuntimeArchitecture.X64;
    }

    private static string ResolveDefaultRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var runtimeProjectPath = Path.Combine(
                directory.FullName,
                "source",
                "runtime",
                "KoromoEventScript.Runtime.Windows",
                RuntimeProjectName);
            if (File.Exists(runtimeProjectPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return AppContext.BaseDirectory;
    }
}
