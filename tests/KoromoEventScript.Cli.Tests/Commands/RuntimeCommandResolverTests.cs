using System.Runtime.InteropServices;
using KoromoEventScript.Cli.Commands.Run;

namespace KoromoEventScript.Cli.Tests.Commands;

public sealed class RuntimeCommandResolverTests
{
    [Test]
    public void Resolve_PrefersBundledExeInAppBaseDirectory()
    {
        var fileSystem = new StubRuntimeCommandFileSystem(
            existingFiles: [Path.Combine("C:", "app", "KoromoEventScript.Runtime.Windows.exe")],
            existingDirectories: []);
        var resolver = new RuntimeCommandResolver(fileSystem, () => Path.Combine("C:", "app"), () => Path.Combine("C:", "repo"), RuntimeArchitecture.X64);

        var result = resolver.Resolve();

        Assert.That(result, Is.EqualTo(Path.Combine("C:", "app", "KoromoEventScript.Runtime.Windows.exe")));
    }

    [Test]
    public void Resolve_PrefersRepoCsprojAfterBundledExe()
    {
        var csproj = Path.Combine("C:", "repo", "source", "runtime", "KoromoEventScript.Runtime.Windows", "KoromoEventScript.Runtime.Windows.csproj");
        var binExe = Path.Combine("C:", "repo", "source", "runtime", "KoromoEventScript.Runtime.Windows", "bin", "Debug", "net10.0-windows10.0.19041.0", "win-x64", "KoromoEventScript.Runtime.Windows.exe");
        var fileSystem = new StubRuntimeCommandFileSystem([csproj, binExe], []);
        var resolver = new RuntimeCommandResolver(fileSystem, () => Path.Combine("C:", "app"), () => Path.Combine("C:", "repo"), RuntimeArchitecture.X64);

        var result = resolver.Resolve();

        Assert.That(result, Is.EqualTo(csproj));
    }

    [Test]
    public void Resolve_PrefersCurrentArchitectureDebugBinExeBeforeRelease()
    {
        var runtimeRoot = Path.Combine("C:", "repo", "source", "runtime", "KoromoEventScript.Runtime.Windows");
        var releaseExe = Path.Combine(runtimeRoot, "bin", "Release", "net10.0-windows10.0.19041.0", "win-x64", "KoromoEventScript.Runtime.Windows.exe");
        var debugExe = Path.Combine(runtimeRoot, "bin", "Debug", "net10.0-windows10.0.19041.0", "win-x64", "KoromoEventScript.Runtime.Windows.exe");
        var armDebugExe = Path.Combine(runtimeRoot, "bin", "Debug", "net10.0-windows10.0.19041.0", "win-arm64", "KoromoEventScript.Runtime.Windows.exe");
        var fileSystem = new StubRuntimeCommandFileSystem([releaseExe, debugExe, armDebugExe], []);
        var resolver = new RuntimeCommandResolver(fileSystem, () => Path.Combine("C:", "app"), () => Path.Combine("C:", "repo"), RuntimeArchitecture.X64);

        var result = resolver.Resolve();

        Assert.That(result, Is.EqualTo(debugExe));
    }

    [Test]
    public void Resolve_FallsBackToRuntimeExeNameWhenNoCandidateExists()
    {
        var resolver = new RuntimeCommandResolver(
            new StubRuntimeCommandFileSystem([], []),
            () => Path.Combine("C:", "app"),
            () => Path.Combine("C:", "repo"),
            RuntimeArchitecture.X64);

        var result = resolver.Resolve();

        Assert.That(result, Is.EqualTo("KoromoEventScript.Runtime.Windows.exe"));
    }

    private sealed class StubRuntimeCommandFileSystem : RuntimeCommandFileSystem
    {
        private readonly HashSet<string> existingFiles;
        private readonly HashSet<string> existingDirectories;

        public StubRuntimeCommandFileSystem(IEnumerable<string> existingFiles, IEnumerable<string> existingDirectories)
        {
            this.existingFiles = existingFiles.Select(Path.GetFullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            this.existingDirectories = existingDirectories.Select(Path.GetFullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        public override bool FileExists(string path)
        {
            return existingFiles.Contains(Path.GetFullPath(path));
        }

        public override bool DirectoryExists(string path)
        {
            return existingDirectories.Contains(Path.GetFullPath(path));
        }

        public override IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption)
        {
            var fullPath = Path.GetFullPath(path);
            return existingFiles
                .Where(file => file.StartsWith(fullPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                .Where(file => string.Equals(Path.GetFileName(file), searchPattern, StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.OrdinalIgnoreCase);
        }
    }
}
