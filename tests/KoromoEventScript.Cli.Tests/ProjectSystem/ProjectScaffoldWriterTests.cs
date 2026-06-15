using KoromoEventScript.Cli.ProjectSystem;

namespace KoromoEventScript.Cli.Tests.ProjectSystem;

public class ProjectScaffoldWriterTests
{
    [Test]
    public void Write_RejectsExistingManagedFileWithoutForce()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteFile("kes.xml", "existing");
        var scaffold = CreateScaffold(fixture.Root);

        var result = new ProjectScaffoldWriter().Write(scaffold, force: false);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Diagnostics, Is.Not.Empty);
            Assert.That(result.Diagnostics[0].Code, Is.EqualTo("KES9002"));
        });
    }

    [Test]
    public void Write_OverwritesManagedFilesWithForceAndPreservesUnknownFiles()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteFile("kes.xml", "old");
        fixture.WriteFile("notes.txt", "keep");
        var scaffold = CreateScaffold(fixture.Root);

        var result = new ProjectScaffoldWriter().Write(scaffold, force: true);
        var files = fixture.SnapshotFiles();

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(files["kes.xml"], Does.Contain("Entry=\"events/main.kel\""));
            Assert.That(files["notes.txt"], Is.EqualTo("keep"));
        });
    }

    [Test]
    public void Write_RejectsFileWhereDirectoryIsExpected()
    {
        using var fixture = TemporaryProject.Create();
        fixture.WriteFile("assets", "not a directory");
        var scaffold = CreateScaffold(fixture.Root);

        var result = new ProjectScaffoldWriter().Write(scaffold, force: false);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Diagnostics, Is.Not.Empty);
            Assert.That(result.Diagnostics[0].Message, Does.Contain("assets"));
        });
    }

    private static ProjectScaffold CreateScaffold(string root)
    {
        var options = new KoromoEventScript.Cli.Commands.Init.InitCommandOptions(
            ProjectDirectory: null,
            ProjectName: "Game",
            Template: KoromoEventScript.Cli.Commands.Init.InitTemplate.Basic,
            Force: false,
            NoSample: false,
            OutputFormat: KoromoEventScript.Cli.Diagnostics.DiagnosticOutputFormat.Text);
        return new ProjectScaffoldFactory().Create(options, root);
    }
}
