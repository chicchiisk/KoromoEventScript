using KoromoEventScript.Cli.Commands.Run;
using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Tests.Commands;

public sealed class RuntimeLaunchAdapterTests
{
    [Test]
    public void Create_ForExeBuildsRuntimeArgumentsWithoutShellString()
    {
        var runtimePath = Path.Combine("C:", "tools", "runtime", "KoromoEventScript.Runtime.Windows.exe");
        var manifestPath = Path.Combine("C:", "project", "build", "windows", "manifest.json");
        var options = new RunCommandOptions(
            ProjectDirectory: null,
            OutputFormat: DiagnosticOutputFormat.Text,
            Locale: "ja-JP",
            Start: "chapter002:start",
            Fullscreen: true,
            Width: 1600,
            Height: 900,
            Debug: true,
            Profile: true,
            RuntimeArguments: ["--trace-frame", "--seed", "42"]);

        var request = new RuntimeLaunchAdapter().Create(
            runtimePath,
            manifestPath,
            options,
            Path.Combine("C:", "project"));

        Assert.Multiple(() =>
        {
            Assert.That(request.FileName, Is.EqualTo(runtimePath));
            Assert.That(request.WorkingDirectory, Is.EqualTo(Path.GetDirectoryName(runtimePath)));
            Assert.That(request.Arguments, Is.EqualTo(new[]
            {
                "--manifest",
                manifestPath,
                "--locale",
                "ja-JP",
                "--start",
                "chapter002:start",
                "--fullscreen",
                "--width",
                "1600",
                "--height",
                "900",
                "--debug",
                "--profile",
                "--trace-frame",
                "--seed",
                "42",
            }));
        });
    }

    [Test]
    public void Create_ForExeOmitsUnspecifiedOptionalArgumentsAndUsesCurrentDirectoryWhenRuntimeHasNoDirectory()
    {
        var options = new RunCommandOptions(null, DiagnosticOutputFormat.Text);
        var currentDirectory = Path.Combine("C:", "project");

        var request = new RuntimeLaunchAdapter().Create(
            "KoromoEventScript.Runtime.Windows.exe",
            "manifest.json",
            options,
            currentDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(request.FileName, Is.EqualTo("KoromoEventScript.Runtime.Windows.exe"));
            Assert.That(request.WorkingDirectory, Is.EqualTo(currentDirectory));
            Assert.That(request.Arguments, Is.EqualTo(new[] { "--manifest", "manifest.json" }));
        });
    }

    [Test]
    public void Create_ForCsprojBuildsDotnetRunRequestWithSerializedRuntimeArguments()
    {
        var projectPath = Path.Combine("C:", "repo", "source", "runtime", "KoromoEventScript.Runtime.Windows", "KoromoEventScript.Runtime.Windows.csproj");
        var manifestPath = Path.Combine("C:", "project", "build", "windows", "manifest with space.json");
        var options = new RunCommandOptions(
            ProjectDirectory: null,
            OutputFormat: DiagnosticOutputFormat.Text,
            Locale: string.Empty,
            Start: "tag with space",
            RuntimeArguments: ["", "plain", "with space", """quote"inside""", @"C:\assets\"]);

        var request = new RuntimeLaunchAdapter().Create(
            projectPath,
            manifestPath,
            options,
            Path.Combine("C:", "project"));

        Assert.Multiple(() =>
        {
            Assert.That(request.FileName, Is.EqualTo("dotnet"));
            Assert.That(request.WorkingDirectory, Is.EqualTo(Path.GetDirectoryName(projectPath)));
            Assert.That(request.Arguments, Is.EqualTo(new[]
            {
                "run",
                "--project",
                projectPath,
                "--no-launch-profile",
                "--",
                "--args",
                @"""--manifest"" ""C:\project\build\windows\manifest with space.json"" ""--start"" ""tag with space"" """" ""plain"" ""with space"" ""quote\""inside"" ""C:\assets\\""",
            }));
        });
    }
}
