using KoromoEventScript.Runtime.Core.Diagnostics;
using KoromoEventScript.Runtime.Core.Execution;
using KoromoEventScript.Runtime.Windows.Audio;
using KoromoEventScript.Runtime.Windows.Diagnostics;
using KoromoEventScript.Runtime.Windows.Input;

namespace KoromoEventScript.Runtime.Windows.Tests.Diagnostics;

public sealed class RuntimeDiagnosticsTests
{
    [Test]
    public void FormatOverlay_WithDebugEnabled_IncludesRuntimeState()
    {
        var formatter = new RuntimeDebugOverlayFormatter();

        var lines = formatter.Format(CreateSnapshot(), RuntimeDiagnosticsMode.Debug);

        Assert.Multiple(() =>
        {
            Assert.That(lines, Does.Contain("FPS: 59.94"));
            Assert.That(lines, Does.Contain("VM: chapter001#42"));
            Assert.That(lines, Does.Contain("Resources: loaded=3 unresolved=missing.bg"));
            Assert.That(lines, Does.Contain("Audio: BGM=bgm.daily Voice=voice.noa.001 SE=2"));
            Assert.That(lines, Does.Contain("Input: StartSkip/Keyboard"));
            Assert.That(lines, Has.Some.Contains("KESW6001"));
            Assert.That(lines, Has.Some.Contains("KESR3002"));
        });
    }

    [Test]
    public void FormatOverlay_WithNormalMode_RedactsInternalRuntimeState()
    {
        var formatter = new RuntimeDebugOverlayFormatter();

        var lines = formatter.Format(CreateSnapshot(), RuntimeDiagnosticsMode.Normal);

        Assert.Multiple(() =>
        {
            Assert.That(lines, Does.Contain("Warnings: 1 Errors: 1"));
            Assert.That(lines.Any(static line => line.Contains("chapter001", StringComparison.Ordinal)), Is.False);
            Assert.That(lines.Any(static line => line.Contains("missing.bg", StringComparison.Ordinal)), Is.False);
            Assert.That(lines.Any(static line => line.Contains("bgm.daily", StringComparison.Ordinal)), Is.False);
            Assert.That(lines.Any(static line => line.Contains("StartSkip", StringComparison.Ordinal)), Is.False);
        });
    }

    [Test]
    public async Task WriteAsync_WithDebugEnabled_WritesDetailedRuntimeLog()
    {
        var logPath = Path.Combine(Path.GetTempPath(), "kes-runtime-log", Guid.NewGuid().ToString("N"), "runtime.log");
        var writer = new RuntimeLogWriter(logPath);

        await writer.WriteAsync(CreateSnapshot(), RuntimeDiagnosticsMode.Debug);

        var log = await File.ReadAllTextAsync(logPath);
        Assert.Multiple(() =>
        {
            Assert.That(log, Does.Contain("FPS: 59.94"));
            Assert.That(log, Does.Contain("VM: chapter001#42"));
            Assert.That(log, Does.Contain("KESW6001"));
            Assert.That(log, Does.Contain("KESR3002"));
        });
    }

    [Test]
    public async Task WriteAsync_WithNormalMode_OnlyWritesPlayerVisibleDiagnosticSummary()
    {
        var logPath = Path.Combine(Path.GetTempPath(), "kes-runtime-log", Guid.NewGuid().ToString("N"), "runtime.log");
        var writer = new RuntimeLogWriter(logPath);

        await writer.WriteAsync(CreateSnapshot(), RuntimeDiagnosticsMode.Normal);

        var log = await File.ReadAllTextAsync(logPath);
        Assert.Multiple(() =>
        {
            Assert.That(log, Does.Contain("Warnings: 1 Errors: 1"));
            Assert.That(log, Does.Not.Contain("chapter001#42"));
            Assert.That(log, Does.Not.Contain("missing.bg"));
            Assert.That(log, Does.Not.Contain("bgm.daily"));
        });
    }

    private static RuntimeDiagnosticsSnapshot CreateSnapshot()
    {
        return new RuntimeDiagnosticsSnapshot(
            Fps: 59.94d,
            VmPosition: new RuntimeExecutionPosition("chapter001", 42, "events/chapter001.kc"),
            Resources: new RuntimeResourceDiagnostics(LoadedAssetCount: 3, UnresolvedAssetIds: ["missing.bg"]),
            Audio: new AudioServiceState(
                Bgm: new AudioChannelItem("bgm.daily", "assets/bgm/daily.ogg", Loop: true, Volume: 0.8d),
                SoundEffects: [
                    new AudioChannelItem("se.door", "assets/se/door.wav", Loop: false, Volume: 0.5d),
                    new AudioChannelItem("se.bell", "assets/se/bell.wav", Loop: false, Volume: 0.5d),
                ],
                Voice: new AudioChannelItem("voice.noa.001", "assets/voice/noa001.ogg", Loop: false, Volume: 0.9d),
                Volumes: new AudioVolumeState(Master: 0.8d, Bgm: 1d, Se: 0.7d, Voice: 0.9d)),
            LastInput: new RuntimeInputEvent(
                RuntimeInputAction.StartSkip,
                RuntimeInputSource.Keyboard,
                new RuntimeInputState(SkipActive: true)),
            Diagnostics: [
                RuntimeDiagnostic.Warning("KESW6001", "Voice asset is missing."),
                RuntimeDiagnostic.Error("KESR3002", "Instruction index does not exist.", RuntimeFailureKind.Runtime),
            ]);
    }
}
