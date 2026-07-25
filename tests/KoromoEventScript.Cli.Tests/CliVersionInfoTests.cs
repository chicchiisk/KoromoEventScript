using System.Diagnostics;
using KoromoEventScript.Cli;

namespace KoromoEventScript.Cli.Tests;

public sealed class CliVersionInfoTests
{
    [Test]
    public void Current_MatchesAssemblyAndFileVersion()
    {
        var assemblyPath = typeof(CliVersionInfo).Assembly.Location;
        var fileVersion = FileVersionInfo.GetVersionInfo(assemblyPath).FileVersion;

        Assert.Multiple(() =>
        {
            Assert.That(CliVersionInfo.Current, Is.EqualTo("0.1.0"));
            Assert.That(fileVersion, Is.EqualTo("0.1.0.0"));
        });
    }
}
