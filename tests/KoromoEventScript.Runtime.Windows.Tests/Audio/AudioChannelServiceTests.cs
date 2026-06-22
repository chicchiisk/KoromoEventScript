using KoromoEventScript.Runtime.Core.Diagnostics;
using KoromoEventScript.Runtime.Core.Effects;
using KoromoEventScript.Runtime.Core.Manifests;
using KoromoEventScript.Runtime.Core.Packages;
using KoromoEventScript.Runtime.Windows.Audio;

namespace KoromoEventScript.Runtime.Windows.Tests.Audio;

public sealed class AudioChannelServiceTests
{
    [Test]
    public async Task ApplyAsync_WithBgmSeAndVoiceEffects_UpdatesIndependentChannels()
    {
        var backend = new RecordingAudioBackend();
        var service = new AudioChannelService(CreateCatalog(), backend);

        await service.ApplyAsync(AudioEffect("audio.bgm", ["id", "bgm.daily", "loop", "true", "fade", "0.5"]));
        await service.ApplyAsync(AudioEffect("audio.se", ["id", "se.door"]));
        await service.ApplyAsync(AudioEffect("text.vo", ["id", "voice.noa.001"]));

        Assert.Multiple(() =>
        {
            Assert.That(service.State.Bgm?.AssetId, Is.EqualTo("bgm.daily"));
            Assert.That(service.State.SoundEffects.Select(static item => item.AssetId), Is.EqualTo(["se.door"]));
            Assert.That(service.State.Voice?.AssetId, Is.EqualTo("voice.noa.001"));
            Assert.That(backend.Plays.Select(static play => play.Channel), Is.EqualTo([AudioChannel.Bgm, AudioChannel.Se, AudioChannel.Voice]));
            Assert.That(backend.Plays[0].Options.Loop, Is.True);
            Assert.That(backend.Plays[0].Options.FadeSeconds, Is.EqualTo(0.5d));
        });
    }

    [Test]
    public async Task ApplyAsync_WithNewBgm_ReplacesCurrentBgm()
    {
        var backend = new RecordingAudioBackend();
        var service = new AudioChannelService(CreateCatalog(), backend);

        await service.ApplyAsync(AudioEffect("audio.bgm", ["id", "bgm.daily", "loop", "true", "fade", "0"]));
        await service.ApplyAsync(AudioEffect("audio.bgm", ["id", "bgm.night", "loop", "false", "fade", "1"]));

        Assert.Multiple(() =>
        {
            Assert.That(service.State.Bgm?.AssetId, Is.EqualTo("bgm.night"));
            Assert.That(backend.Stops.Single().Channel, Is.EqualTo(AudioChannel.Bgm));
            Assert.That(backend.Plays.Select(static play => play.Asset.AssetId), Is.EqualTo(["bgm.daily", "bgm.night"]));
        });
    }

    [Test]
    public async Task ApplyAsync_WithMissingVoice_ReturnsWarningAndContinues()
    {
        var service = new AudioChannelService(CreateCatalog(), new RecordingAudioBackend());

        var result = await service.ApplyAsync(AudioEffect("text.vo", ["id", "voice.missing"]));

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Diagnostics.Single().Severity, Is.EqualTo(RuntimeDiagnosticSeverity.Warning));
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("KESW6001"));
            Assert.That(service.State.Voice, Is.Null);
        });
    }

    [Test]
    public async Task StopVoiceForTextSkipAsync_StopsVoiceOnlyAndKeepsBgm()
    {
        var backend = new RecordingAudioBackend();
        var service = new AudioChannelService(CreateCatalog(), backend);
        await service.ApplyAsync(AudioEffect("audio.bgm", ["id", "bgm.daily", "loop", "true", "fade", "0"]));
        await service.ApplyAsync(AudioEffect("text.vo", ["id", "voice.noa.001"]));

        await service.StopVoiceForTextSkipAsync();

        Assert.Multiple(() =>
        {
            Assert.That(service.State.Bgm?.AssetId, Is.EqualTo("bgm.daily"));
            Assert.That(service.State.Voice, Is.Null);
            Assert.That(backend.Stops.Select(static stop => stop.Channel), Is.EqualTo([AudioChannel.Voice]));
        });
    }

    [Test]
    public async Task SetVolumesAsync_UpdatesMasterAndChannelVolumes()
    {
        var backend = new RecordingAudioBackend();
        var service = new AudioChannelService(CreateCatalog(), backend);

        await service.SetVolumesAsync(new AudioVolumeState(Master: 0.8d, Bgm: 0.5d, Se: 0.4d, Voice: 0.9d));

        Assert.Multiple(() =>
        {
            Assert.That(service.State.Volumes.Master, Is.EqualTo(0.8d));
            Assert.That(backend.VolumeChanges.Select(static change => change.Channel), Is.EqualTo([AudioChannel.Bgm, AudioChannel.Se, AudioChannel.Voice]));
            Assert.That(backend.VolumeChanges.Select(static change => change.EffectiveVolume), Is.EqualTo(new[] { 0.4d, 0.32d, 0.72d }).Within(0.000_001d));
        });
    }

    private static RuntimeEffect AudioEffect(string name, params string[] payload)
    {
        return new RuntimeEffect(RuntimeEffectKind.Audio, name, ToPayload(payload));
    }

    private static IReadOnlyDictionary<string, string?> ToPayload(params string[] values)
    {
        var payload = new Dictionary<string, string?>(StringComparer.Ordinal);
        for (var index = 0; index < values.Length; index += 2)
        {
            payload[values[index]] = values[index + 1];
        }

        return payload;
    }

    private static RuntimeResourceCatalog CreateCatalog()
    {
        return RuntimeResourceCatalog.Create(
            "ja-JP",
            [
                Asset("bgm.daily", "bgm", "daily.ogg"),
                Asset("bgm.night", "bgm", "night.ogg"),
                Asset("se.door", "se", "door.wav"),
                Asset("voice.noa.001", "voice", "noa001.ogg"),
            ]);
    }

    private static RuntimeAssetEntry Asset(string id, string kind, string fileName)
    {
        return new RuntimeAssetEntry(id, kind, fileName, Path.GetFullPath(fileName), Locale: null);
    }

    private sealed class RecordingAudioBackend : IAudioPlaybackBackend
    {
        public List<AudioPlaybackRequest> Plays { get; } = [];

        public List<AudioStopRequest> Stops { get; } = [];

        public List<AudioVolumeChange> VolumeChanges { get; } = [];

        public Task PlayAsync(AudioPlaybackRequest request, CancellationToken cancellationToken = default)
        {
            Plays.Add(request);
            return Task.CompletedTask;
        }

        public Task StopAsync(AudioStopRequest request, CancellationToken cancellationToken = default)
        {
            Stops.Add(request);
            return Task.CompletedTask;
        }

        public Task SetVolumeAsync(AudioVolumeChange change, CancellationToken cancellationToken = default)
        {
            VolumeChanges.Add(change);
            return Task.CompletedTask;
        }
    }
}
