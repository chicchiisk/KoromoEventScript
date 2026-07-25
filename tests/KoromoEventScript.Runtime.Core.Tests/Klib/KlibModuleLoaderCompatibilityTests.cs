using System.Text;
using KoromoEventScript.Runtime.Core.Klib;

namespace KoromoEventScript.Runtime.Core.Tests.Klib;

public sealed class KlibModuleLoaderCompatibilityTests
{
    [Test]
    public void Load_UnsupportedMajorVersion_ReturnsCompatibilityDiagnostic()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Encoding.ASCII.GetBytes("KLIB"));
            writer.Write(99);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
        }

        var result = new KlibModuleLoader().Load(stream.ToArray(), "unsupported.klib");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(result.Diagnostics[0].Message, Does.Contain("unsupported format version 99.0.0"));
        });
    }
}
