using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.Commands.Build;
using KoromoEventScript.Cli.Commands.Correct;
using KoromoEventScript.Cli.Commands.Init;
using KoromoEventScript.Cli.Commands.Loc;
using KoromoEventScript.Cli.Commands.Publish;
using KoromoEventScript.Cli.Commands.Run;
using KoromoEventScript.Cli.Diagnostics;
using KoromoEventScript.Runtime.Core.Klib;
using KoromoEventScript.Runtime.Core.Manifests;
using KoromoEventScript.Runtime.Core.Packages;

namespace KoromoEventScript.Cli.Tests.Commands;

public sealed class PublishCommandTests
{
    [Test]
    public void Publish_WindowsCreatesRuntimeFolderLayoutWithoutSourceFiles()
    {
        using var fixture = TemporaryProject.Create();
        CopyProject(GetTestDataPath("projects", "minimal"), fixture.Root);
        fixture.WriteFile("assets/bg/school.txt", "asset");
        var runtimeBundle = Path.Combine(fixture.Root, "runtime-bundle");
        Directory.CreateDirectory(runtimeBundle);
        File.WriteAllText(Path.Combine(runtimeBundle, "KoromoEventScript.Runtime.Windows.exe"), "runtime");
        File.WriteAllText(Path.Combine(runtimeBundle, "runtime-support.dll"), "support");
        using var output = new StringWriter();
        using var error = new StringWriter();
        var publishCommand = new WindowsPublishCommand(
            new KoromoEventScript.Cli.Build.BuildPipelineService(),
            new KoromoEventScript.Cli.ProjectSystem.ProjectRootResolver(),
            new KoromoEventScript.Cli.ProjectSystem.ProjectConfigLoader(),
            () => runtimeBundle);
        var app = new CliApplication(
            new BuildCheckOnlyCommand(),
            new BuildCommand(),
            new CorrectCommand(),
            new InitCommand(),
            new LocCommand(),
            publishCommand,
            new RunCommand(),
            new DiagnosticSink());

        var exitCode = app.Run(
            ["publish", fixture.Root, "--target", "windows", "--archive", "none"],
            output,
            error,
            TestContext.CurrentContext.WorkDirectory);

        var packageRoot = Path.Combine(fixture.Root, "dist", "windows", "MinimalProject");
        var dataManifest = Path.Combine(packageRoot, "data", "manifest.json");
        var manifestResult = new RuntimeManifestReader().Read(dataManifest);
        var packageResult = manifestResult.Succeeded
            ? new RuntimePackageResolver(new KlibModuleLoader()).Resolve(manifestResult.Document!)
            : null;
        var packageFiles = Directory.Exists(packageRoot)
            ? Directory.EnumerateFiles(packageRoot, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(packageRoot, path).Replace('\\', '/'))
                .ToArray()
            : [];

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.Success));
            Assert.That(output.ToString(), Is.Empty);
            Assert.That(error.ToString(), Is.Empty);
            Assert.That(File.Exists(Path.Combine(packageRoot, "MinimalProject.exe")), Is.True);
            Assert.That(File.Exists(Path.Combine(packageRoot, "runtime-support.dll")), Is.True);
            Assert.That(File.Exists(dataManifest), Is.True);
            Assert.That(File.Exists(Path.Combine(packageRoot, "data", "events", "chapter001.klib")), Is.True);
            Assert.That(File.Exists(Path.Combine(packageRoot, "data", "assets", "bg", "school.txt")), Is.True);
            Assert.That(packageFiles.Any(static path => path.EndsWith(".kc", StringComparison.OrdinalIgnoreCase)), Is.False);
            Assert.That(packageFiles.Any(static path => path.EndsWith(".kel", StringComparison.OrdinalIgnoreCase)), Is.False);
            Assert.That(manifestResult.Succeeded, Is.True);
            Assert.That(manifestResult.Document!.ManifestDirectory, Is.EqualTo(Path.Combine(packageRoot, "data")));
            Assert.That(manifestResult.Document.Assets.Select(static asset => asset.Path), Does.Contain("assets/bg/school.txt"));
            Assert.That(packageResult!.Succeeded, Is.True);
        });
    }

    private static string GetTestDataPath(params string[] segments)
    {
        return Path.GetFullPath(Path.Combine(GetRepositoryRoot(), "testdata", Path.Combine(segments)));
    }

    private static void CopyProject(string sourceRoot, string destinationRoot)
    {
        foreach (var directory in Directory.EnumerateDirectories(sourceRoot, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destinationRoot, Path.GetRelativePath(sourceRoot, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(destinationRoot, Path.GetRelativePath(sourceRoot, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }

    private static string GetRepositoryRoot()
    {
        return Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", ".."));
    }
}
