using KoromoEventScript.Runtime.Core.Diagnostics;
using KoromoEventScript.Runtime.Windows.ErrorHandling;

namespace KoromoEventScript.Runtime.Windows.Tests.ErrorHandling;

public sealed class WindowsRuntimeFatalErrorPresenterTests
{
    [TestCase(RuntimeFailureKind.Argument, RuntimeExitCode.CommandLineError)]
    [TestCase(RuntimeFailureKind.Runtime, RuntimeExitCode.RuntimeError)]
    [TestCase(RuntimeFailureKind.Io, RuntimeExitCode.FileOrDirectoryError)]
    [TestCase(RuntimeFailureKind.Startup, RuntimeExitCode.RuntimeStartupError)]
    [TestCase(RuntimeFailureKind.General, RuntimeExitCode.GeneralError)]
    public void Create_MapsFailureKindToCliCompatibleExitCode(RuntimeFailureKind failureKind, RuntimeExitCode exitCode)
    {
        var presenter = new WindowsRuntimeFatalErrorPresenter();

        var result = presenter.Create([Diagnostic(failureKind)], failureKind, debug: false);

        Assert.That(result.ExitCode, Is.EqualTo(exitCode));
    }

    [Test]
    public void Create_WithManifestStartupError_ShowsPlayerSafeMessageInNormalMode()
    {
        var presenter = new WindowsRuntimeFatalErrorPresenter();
        var diagnostic = RuntimeDiagnostic.Error(
            "KESR1001",
            "Runtime manifest was not found: C:/internal/build/data/manifest.json",
            RuntimeFailureKind.Startup);

        var result = presenter.Create([diagnostic], RuntimeFailureKind.Startup, debug: false);

        Assert.Multiple(() =>
        {
            Assert.That(result.Title, Is.EqualTo("Runtime startup error"));
            Assert.That(result.PlayerMessage, Does.Contain("The game could not be started."));
            Assert.That(result.PlayerMessage, Does.Contain("KESR1001"));
            Assert.That(result.PlayerMessage, Does.Not.Contain("C:/internal/build"));
            Assert.That(result.ExitCode, Is.EqualTo(RuntimeExitCode.RuntimeStartupError));
        });
    }

    [Test]
    public void Create_WithDebugMode_IncludesDiagnosticDetails()
    {
        var presenter = new WindowsRuntimeFatalErrorPresenter();
        var diagnostic = RuntimeDiagnostic.Error(
            "KESR1001",
            "Runtime manifest was not found: C:/internal/build/data/manifest.json",
            RuntimeFailureKind.Startup);

        var result = presenter.Create([diagnostic], RuntimeFailureKind.Startup, debug: true);

        Assert.That(result.PlayerMessage, Does.Contain("C:/internal/build/data/manifest.json"));
    }

    [TestCase(RuntimeFailureKind.Io, "A required game file could not be loaded.")]
    [TestCase(RuntimeFailureKind.Runtime, "The game stopped because a runtime error occurred.")]
    [TestCase(RuntimeFailureKind.Argument, "The runtime command line is invalid.")]
    public void Create_UsesRepresentativeFatalErrorMessages(RuntimeFailureKind failureKind, string message)
    {
        var presenter = new WindowsRuntimeFatalErrorPresenter();

        var result = presenter.Create([Diagnostic(failureKind)], failureKind, debug: false);

        Assert.That(result.PlayerMessage, Does.Contain(message));
    }

    private static RuntimeDiagnostic Diagnostic(RuntimeFailureKind failureKind)
    {
        return RuntimeDiagnostic.Error("KESR9999", "Internal detail", failureKind);
    }
}
