using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.Commands.Build;

namespace KoromoEventScript.Cli.Tests.Commands;

public class BuildCommandTests
{
    [Test]
    public void Run_EmitsKlibArtifactsForNonCheckOnlyBuild()
    {
        using var fixture = TemporaryProject.Create();
        using var output = new StringWriter();
        using var error = new StringWriter();
        CopyProject(GetTestDataPath("projects", "minimal"), fixture.Root);

        var exitCode = new CliApplication().Run(
            ["build", fixture.Root, "--txt-il"],
            output,
            error,
            TestContext.CurrentContext.WorkDirectory);

        var klibPath = Path.Combine(fixture.Root, "build", "windows", "events", "chapter001.klib");
        var klibtxtPath = Path.Combine(fixture.Root, "build", "windows", "events", "chapter001.klibtxt");
        var magic = File.ReadAllBytes(klibPath).Take(4).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.Success));
            Assert.That(output.ToString(), Is.Empty);
            Assert.That(error.ToString(), Is.Empty);
            Assert.That(File.Exists(klibPath), Is.True);
            Assert.That(File.Exists(klibtxtPath), Is.True);
            Assert.That(magic, Is.EqualTo(new byte[] { 0x4B, 0x4C, 0x49, 0x42 }));
            Assert.That(File.ReadAllText(klibtxtPath), Does.Contain("SYSCALLVOID"));
            Assert.That(File.ReadAllText(klibtxtPath), Does.Contain("SELECT"));
            Assert.That(File.ReadAllText(klibtxtPath), Does.Contain("JUMP"));
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
