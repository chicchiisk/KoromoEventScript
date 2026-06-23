using System.Text.Json;
using System.Text.Json.Nodes;
using KoromoEventScript.Cli.Build;
using KoromoEventScript.Cli.Commands.Build;
using KoromoEventScript.Cli.Diagnostics;
using KoromoEventScript.Cli.ProjectSystem;

namespace KoromoEventScript.Cli.Commands.Publish;

public sealed class WindowsPublishCommand
{
    private readonly BuildPipelineService pipelineService;
    private readonly ProjectRootResolver projectRootResolver;
    private readonly ProjectConfigLoader projectConfigLoader;
    private readonly Func<string> runtimeBundlePathProvider;

    public WindowsPublishCommand()
        : this(
            new BuildPipelineService(),
            new ProjectRootResolver(),
            new ProjectConfigLoader(),
            DefaultRuntimeBundlePath)
    {
    }

    public WindowsPublishCommand(
        BuildPipelineService pipelineService,
        ProjectRootResolver projectRootResolver,
        ProjectConfigLoader projectConfigLoader,
        Func<string>? runtimeBundlePathProvider = null)
    {
        this.pipelineService = pipelineService;
        this.projectRootResolver = projectRootResolver;
        this.projectConfigLoader = projectConfigLoader;
        this.runtimeBundlePathProvider = runtimeBundlePathProvider ?? DefaultRuntimeBundlePath;
    }

    public PublishCommandResult Execute(PublishCommandOptions options, string currentDirectory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentDirectory);

        if (!string.Equals(options.Target, "windows", StringComparison.OrdinalIgnoreCase))
        {
            return Failure(CliExitCode.CommandLineError, "KES9001", string.Empty, $"Unsupported publish target '{options.Target}'. Expected 'windows'.");
        }

        if (!string.Equals(options.Archive, "none", StringComparison.OrdinalIgnoreCase))
        {
            return Failure(CliExitCode.CommandLineError, "KES9001", string.Empty, "Only '--archive none' is supported until zip packaging is enabled.");
        }

        var rootResult = projectRootResolver.Resolve(options.ProjectDirectory, currentDirectory);
        if (!rootResult.Succeeded)
        {
            return new PublishCommandResult(CliExitCode.FileOrDirectoryError, [rootResult.Diagnostic!], null);
        }

        var configResult = projectConfigLoader.Load(rootResult.ProjectRoot!);
        if (!configResult.Succeeded)
        {
            return new PublishCommandResult(CliExitCode.FileOrDirectoryError, [configResult.Diagnostic!], null);
        }

        var config = configResult.Config!;
        var buildResult = pipelineService.Run(new BuildPipelineRequest(
            new BuildCommandOptions(
                options.ProjectDirectory,
                options.OutputFormat,
                Target: "windows",
                Locale: options.Locale),
            currentDirectory,
            ValidateOnly: false));
        if (buildResult.ExitCode != CliExitCode.Success)
        {
            return new PublishCommandResult(buildResult.ExitCode, buildResult.Diagnostics, null);
        }

        try
        {
            var packageRoot = ResolvePackageRoot(config, options);
            if (options.Clean && Directory.Exists(packageRoot))
            {
                Directory.Delete(packageRoot, recursive: true);
            }

            Directory.CreateDirectory(packageRoot);
            CopyRuntimeBundle(runtimeBundlePathProvider(), packageRoot, $"{config.ProjectName}.exe");
            var dataRoot = Path.Combine(packageRoot, "data");
            Directory.CreateDirectory(dataRoot);

            var buildRoot = Path.GetDirectoryName(buildResult.ManifestPath!)!;
            CopyKlibArtifacts(Path.Combine(buildRoot, config.EventsPath), Path.Combine(dataRoot, config.EventsPath));
            CopyAssets(Path.Combine(config.ProjectRoot, config.AssetsPath), Path.Combine(dataRoot, config.AssetsPath));
            WriteRuntimeManifest(buildResult.ManifestPath!, Path.Combine(dataRoot, "manifest.json"), config.AssetsPath);

            if (options.IncludeSource)
            {
                CopySources(config, Path.Combine(packageRoot, "source"));
            }

            return new PublishCommandResult(CliExitCode.Success, buildResult.Diagnostics, packageRoot);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            return Failure(CliExitCode.FileOrDirectoryError, "KES9004", rootResult.ProjectRoot!, $"Could not publish Windows package: {exception.Message}");
        }
    }

    private static string ResolvePackageRoot(ProjectConfig config, PublishCommandOptions options)
    {
        var outputRoot = string.IsNullOrWhiteSpace(options.OutputDirectory)
            ? Path.Combine(config.ProjectRoot, config.DistPath)
            : Path.IsPathRooted(options.OutputDirectory)
                ? options.OutputDirectory
                : Path.Combine(config.ProjectRoot, options.OutputDirectory);

        return Path.Combine(outputRoot, "windows", config.ProjectName);
    }

    private static void CopyRuntimeBundle(string runtimeBundlePath, string packageRoot, string executableName)
    {
        var source = Path.GetFullPath(runtimeBundlePath);
        if (File.Exists(source))
        {
            var runtimeDirectory = Path.GetDirectoryName(source)!;
            CopyDirectory(runtimeDirectory, packageRoot, includePredicate: path => !IsRuntimeDataPath(runtimeDirectory, path) && !StringComparer.OrdinalIgnoreCase.Equals(path, source));
            File.Copy(source, Path.Combine(packageRoot, executableName), overwrite: true);
            return;
        }

        if (!Directory.Exists(source))
        {
            throw new FileNotFoundException($"Windows runtime bundle was not found: {source}");
        }

        CopyDirectory(source, packageRoot, includePredicate: path => !IsRuntimeDataPath(source, path) && !StringComparer.OrdinalIgnoreCase.Equals(Path.GetExtension(path), ".exe"));
        var executable = Directory.EnumerateFiles(source, "*.exe", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (executable is null)
        {
            throw new FileNotFoundException($"Windows runtime executable was not found in bundle: {source}");
        }

        File.Copy(executable, Path.Combine(packageRoot, executableName), overwrite: true);
    }

    private static bool IsRuntimeDataPath(string runtimeDirectory, string path)
    {
        var relative = Path.GetRelativePath(runtimeDirectory, path).Replace('\\', '/');
        return relative.StartsWith("data/", StringComparison.OrdinalIgnoreCase);
    }

    private static void CopyKlibArtifacts(string sourceEventsRoot, string destinationEventsRoot)
    {
        if (!Directory.Exists(sourceEventsRoot))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(sourceEventsRoot, "*.klib", SearchOption.AllDirectories))
        {
            CopyFile(file, Path.Combine(destinationEventsRoot, Path.GetRelativePath(sourceEventsRoot, file)));
        }
    }

    private static void CopyAssets(string sourceAssetsRoot, string destinationAssetsRoot)
    {
        if (!Directory.Exists(sourceAssetsRoot))
        {
            return;
        }

        CopyDirectory(sourceAssetsRoot, destinationAssetsRoot);
    }

    private static void CopySources(ProjectConfig config, string destinationRoot)
    {
        CopyFile(Path.Combine(config.ProjectRoot, "kes.xml"), Path.Combine(destinationRoot, "kes.xml"));
        CopyFile(Path.Combine(config.ProjectRoot, config.EntryPath), Path.Combine(destinationRoot, config.EntryPath));
        var eventsRoot = Path.Combine(config.ProjectRoot, config.EventsPath);
        if (Directory.Exists(eventsRoot))
        {
            CopyDirectory(eventsRoot, Path.Combine(destinationRoot, config.EventsPath), includePredicate: path => StringComparer.OrdinalIgnoreCase.Equals(Path.GetExtension(path), ".kc"));
        }
    }

    private static void WriteRuntimeManifest(string sourceManifestPath, string destinationManifestPath, string assetsPath)
    {
        var node = JsonNode.Parse(File.ReadAllText(sourceManifestPath))?.AsObject()
            ?? throw new InvalidOperationException("Build manifest is empty.");

        if (node["assets"] is JsonArray assets)
        {
            foreach (var assetNode in assets.OfType<JsonObject>())
            {
                var originalPath = assetNode["path"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(originalPath))
                {
                    continue;
                }

                var fileName = Path.GetFileName(originalPath.Replace('\\', '/'));
                var assetId = assetNode["assetId"]?.GetValue<string>() ?? Path.GetFileNameWithoutExtension(fileName);
                var relativeAssetPath = ResolvePublishedAssetPath(assetsPath, assetId, fileName);
                assetNode["path"] = relativeAssetPath;
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destinationManifestPath)!);
        File.WriteAllText(destinationManifestPath, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string ResolvePublishedAssetPath(string assetsPath, string assetId, string fileName)
    {
        var assetSegments = assetId.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (assetSegments.Length <= 1)
        {
            return $"{assetsPath.Replace('\\', '/')}/{fileName}";
        }

        var relativeDirectory = string.Join('/', assetSegments.Skip(1).SkipLast(1));
        return string.IsNullOrWhiteSpace(relativeDirectory)
            ? $"{assetsPath.Replace('\\', '/')}/{fileName}"
            : $"{assetsPath.Replace('\\', '/')}/{relativeDirectory}/{fileName}";
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory, Func<string, bool>? includePredicate = null)
    {
        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destinationDirectory, Path.GetRelativePath(sourceDirectory, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            if (includePredicate is not null && !includePredicate(file))
            {
                continue;
            }

            CopyFile(file, Path.Combine(destinationDirectory, Path.GetRelativePath(sourceDirectory, file)));
        }
    }

    private static void CopyFile(string sourcePath, string destinationPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        File.Copy(sourcePath, destinationPath, overwrite: true);
    }

    private static PublishCommandResult Failure(CliExitCode exitCode, string code, string file, string message)
    {
        return new PublishCommandResult(exitCode, [new Diagnostic(DiagnosticLevel.Error, code, file, 1, 1, message)], null);
    }

    private static string DefaultRuntimeBundlePath()
    {
        var candidate = Path.Combine(AppContext.BaseDirectory, "KoromoEventScript.Runtime.Windows.exe");
        return File.Exists(candidate) ? candidate : "KoromoEventScript.Runtime.Windows.exe";
    }
}

public sealed record PublishCommandResult(
    CliExitCode ExitCode,
    IReadOnlyList<Diagnostic> Diagnostics,
    string? PackageRoot);
