using KoromoEventScript.Runtime.Core.Klib;
using KoromoEventScript.Runtime.Core.Diagnostics;

namespace KoromoEventScript.Runtime.Core.Tests.Klib;

public sealed class KlibModuleLoaderTests
{
    [Test]
    public void LoadMemory_ReadsSameDocumentAsFileLoad()
    {
        var path = FindTestData("projects", "full-command-sample", "build", "windows", "events", "chapter001.klib");
        var loader = new KlibModuleLoader();

        var fileResult = loader.Load(path);
        var memoryResult = loader.Load(File.ReadAllBytes(path), path);

        Assert.Multiple(() =>
        {
            Assert.That(fileResult.Succeeded, Is.True);
            Assert.That(memoryResult.Succeeded, Is.True);
            Assert.That(memoryResult.Document!.Version, Is.EqualTo(fileResult.Document!.Version));
            Assert.That(memoryResult.Document.Module, Is.EqualTo(fileResult.Document.Module));
            Assert.That(memoryResult.Document.Constants, Has.Count.EqualTo(fileResult.Document.Constants.Count));
            Assert.That(memoryResult.Document.Instructions, Has.Count.EqualTo(fileResult.Document.Instructions.Count));
        });
    }

    [Test]
    public void LoadMemory_RejectsHeaderWithoutRequiredSections()
    {
        var data = new byte[24];
        data[0] = (byte)'K';
        data[1] = (byte)'L';
        data[2] = (byte)'I';
        data[3] = (byte)'B';

        var result = new KlibModuleLoader().Load(data, "header-only.klib");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureKind, Is.EqualTo(RuntimeFailureKind.Startup));
            Assert.That(result.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(result.Diagnostics[0].Code, Is.EqualTo("KESR2102"));
            Assert.That(result.Diagnostics[0].Message, Does.Contain("module info section"));
        });
    }

    private static string FindTestData(params string[] segments)
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName, "testdata" }.Concat(segments).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate Klib testdata.");
    }
}
