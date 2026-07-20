using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Runtime.Core.Klib;
using KoromoEventScript.Runtime.Core.Execution;

namespace KoromoEventScript.Cli.Tests.Execution;

public sealed class PrimeSievePerformanceSampleTests
{
    [Test]
    public void BuiltPrimeSieveSample_Reports104707AsPrime()
    {
        using var fixture = TemporaryProject.Create();
        CopyProject(GetTestDataPath("projects", "function-performance-sample"), fixture.Root);

        var exitCode = new CliApplication().Run(
            ["build", fixture.Root, "--target", "unity"],
            TextWriter.Null,
            TextWriter.Null,
            TestContext.CurrentContext.WorkDirectory);
        Assert.That(exitCode, Is.EqualTo((int)CliExitCode.Success));

        var loadResult = new KlibModuleLoader().Load(
            Path.Combine(fixture.Root, "build", "unity", "events", "prime_sieve.klib"));
        Assert.That(
            loadResult.Succeeded,
            Is.True,
            string.Join(Environment.NewLine, loadResult.Diagnostics.Select(static diagnostic => diagnostic.Message)));

        var document = loadResult.Document!;
        var resultSlot = FindVariableSlot(document, "is_prime");
        var session = new KesVmSession(document);
        var executionResult = new KesVmExecutor().Run(session, 20_000_000);

        Assert.Multiple(() =>
        {
            Assert.That(
                executionResult.Succeeded,
                Is.True,
                string.Join(Environment.NewLine, executionResult.Diagnostics.Select(static diagnostic => diagnostic.Message)));
            Assert.That(session.Variables[resultSlot].BoolValue, Is.True);
        });
    }

    private static int FindVariableSlot(KlibDocument document, string variableName)
    {
        for (var index = 0; index < document.Variables.Count; index++)
        {
            var nameIndex = document.Variables[index].NameIndex;
            if (string.Equals(document.Constants[nameIndex].StringValue, variableName, StringComparison.Ordinal))
            {
                return index;
            }
        }

        Assert.Fail($"Variable '{variableName}' was not found.");
        return -1;
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
