using KoromoEventScript.Cli.Build;
using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.Commands.Build;
using KoromoEventScript.Cli.Compilation;
using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Tests.Compilation;

public class KlibCompilerTests
{
    [Test]
    public void BuildCommand_MatchesBroadSurfaceIrSnapshot()
    {
        using var fixture = CreateBroadSurfaceFixture();

        var result = ExecuteBuild(fixture);
        var actual = ReadGeneratedTextIr(fixture);
        var expected = ReadIrSnapshot("broad-surface.klibtxt");

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.Success));
            Assert.That(NormalizeLineEndings(actual), Is.EqualTo(NormalizeLineEndings(expected)));
        });
    }

    [Test]
    public void BuildCommand_NormalizesLineEndingsBeforeComparingBroadSurfaceSnapshot()
    {
        using var fixture = CreateBroadSurfaceFixture();

        var result = ExecuteBuild(fixture);
        var actual = ReadGeneratedTextIr(fixture);
        var expected = NormalizeLineEndings(ReadIrSnapshot("broad-surface.klibtxt"))
            .Replace("\n", "\r\n", StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.Success));
            Assert.That(NormalizeLineEndings(actual), Is.EqualTo(NormalizeLineEndings(expected)));
        });
    }

    private static TemporaryProject CreateBroadSurfaceFixture()
    {
        var fixture = TemporaryProject.Create();
        fixture.WriteConfig(entry: "events/main.kel");
        fixture.WriteFile("events/main.kel", """
entry = intro
intro = {
    chapter = "events/main.kc"
}
""");
        fixture.WriteFile("events/main.kc", """
actor Riku:
    cast Riku
var actors: Actor[] = [Riku]
var total: number = 1
cast:
    Riku
if true:
    total = total + 1
else:
    total = total - 1
while total < 3:
    total = total + 1
for actor in actors:
    show actor 0
label #start
say Riku:
    こんにちは
nar:
    つづく
select:
    case "続ける" #continue
    case "終わる" #end
label #continue
jump #end
label #end
""");
        return fixture;
    }

    private static BuildCommandResult ExecuteBuild(TemporaryProject fixture)
    {
        return new BuildCommand().Execute(
            new BuildCommandOptions(fixture.Root, DiagnosticOutputFormat.Text, EmitTextIr: true),
            TestContext.CurrentContext.WorkDirectory);
    }

    private static string ReadGeneratedTextIr(TemporaryProject fixture)
    {
        return File.ReadAllText(Path.Combine(fixture.Root, "build", "windows", "events", "main.klibtxt"));
    }

    private static string ReadIrSnapshot(string fileName)
    {
        return File.ReadAllText(GetIrSnapshotPath(fileName));
    }

    private static string NormalizeLineEndings(string text)
    {
        return text.Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static string GetIrSnapshotPath(string fileName)
    {
        return Path.GetFullPath(Path.Combine(GetRepositoryRoot(), "testdata", "snapshots", "ir", fileName));
    }

    private static string GetRepositoryRoot()
    {
        return Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", ".."));
    }
}
