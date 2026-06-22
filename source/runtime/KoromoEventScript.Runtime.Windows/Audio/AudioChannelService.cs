using System.Globalization;
using KoromoEventScript.Runtime.Core.Diagnostics;
using KoromoEventScript.Runtime.Core.Effects;
using KoromoEventScript.Runtime.Core.Manifests;
using KoromoEventScript.Runtime.Core.Packages;

namespace KoromoEventScript.Runtime.Windows.Audio;

public enum AudioChannel
{
    Bgm = 1,
    Se = 2,
    Voice = 3,
}

public sealed record AudioPlaybackOptions(
    bool Loop = false,
    double FadeSeconds = 0d,
    double Volume = 1d);

public sealed record AudioPlaybackRequest(
    AudioChannel Channel,
    RuntimeAssetEntry Asset,
    AudioPlaybackOptions Options);

public sealed record AudioStopRequest(
    AudioChannel Channel,
    string? AssetId = null,
    double FadeSeconds = 0d);

public sealed record AudioVolumeChange(
    AudioChannel Channel,
    double ChannelVolume,
    double MasterVolume)
{
    public double EffectiveVolume => ChannelVolume * MasterVolume;
}

public sealed record AudioChannelItem(
    string AssetId,
    string ResolvedPath,
    bool Loop,
    double Volume);

public sealed record AudioVolumeState(
    double Master = 1d,
    double Bgm = 1d,
    double Se = 1d,
    double Voice = 1d);

public sealed record AudioServiceState(
    AudioChannelItem? Bgm,
    IReadOnlyList<AudioChannelItem> SoundEffects,
    AudioChannelItem? Voice,
    AudioVolumeState Volumes);

public sealed record AudioServiceResult(
    IReadOnlyList<RuntimeDiagnostic> Diagnostics,
    RuntimeFailureKind FailureKind)
{
    public bool Succeeded => FailureKind == RuntimeFailureKind.None;

    public static AudioServiceResult Success(params RuntimeDiagnostic[] diagnostics)
    {
        return new AudioServiceResult(diagnostics, RuntimeFailureKind.None);
    }

    public static AudioServiceResult Failure(RuntimeFailureKind failureKind, params RuntimeDiagnostic[] diagnostics)
    {
        return new AudioServiceResult(diagnostics, failureKind);
    }
}

public interface IAudioPlaybackBackend
{
    Task PlayAsync(AudioPlaybackRequest request, CancellationToken cancellationToken = default);

    Task StopAsync(AudioStopRequest request, CancellationToken cancellationToken = default);

    Task SetVolumeAsync(AudioVolumeChange change, CancellationToken cancellationToken = default);
}

public sealed class AudioChannelService
{
    private readonly RuntimeResourceCatalog resources;
    private readonly IAudioPlaybackBackend backend;
    private readonly List<AudioChannelItem> soundEffects = [];
    private AudioChannelItem? bgm;
    private AudioChannelItem? voice;
    private AudioVolumeState volumes = new();

    public AudioChannelService(RuntimeResourceCatalog resources, IAudioPlaybackBackend backend)
    {
        this.resources = resources;
        this.backend = backend;
    }

    public AudioServiceState State => new(bgm, soundEffects.ToArray(), voice, volumes);

    public async Task<AudioServiceResult> ApplyAsync(RuntimeEffect effect, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(effect);
        if (effect.Kind != RuntimeEffectKind.Audio)
        {
            return AudioServiceResult.Success();
        }

        return effect.Name switch
        {
            "audio.bgm" => await PlayBgmAsync(effect, cancellationToken),
            "audio.bgm_stop" => await StopBgmAsync(effect, cancellationToken),
            "audio.se" => await PlaySeAsync(effect, cancellationToken),
            "audio.se_stop" => await StopSeAsync(effect, cancellationToken),
            "audio.se_stop_all" => await StopAllSeAsync(cancellationToken),
            "audio.voice_stop" => await StopVoiceAsync(cancellationToken),
            "text.vo" => await PlayVoiceAsync(effect, cancellationToken),
            "audio.vo_auto" => await PlayAutoVoiceAsync(effect, cancellationToken),
            _ => AudioServiceResult.Success(),
        };
    }

    public async Task StopVoiceForTextSkipAsync(CancellationToken cancellationToken = default)
    {
        if (voice is null)
        {
            return;
        }

        await backend.StopAsync(new AudioStopRequest(AudioChannel.Voice, voice.AssetId), cancellationToken);
        voice = null;
    }

    public async Task SetVolumesAsync(AudioVolumeState volumes, CancellationToken cancellationToken = default)
    {
        this.volumes = Clamp(volumes);
        await backend.SetVolumeAsync(new AudioVolumeChange(AudioChannel.Bgm, this.volumes.Bgm, this.volumes.Master), cancellationToken);
        await backend.SetVolumeAsync(new AudioVolumeChange(AudioChannel.Se, this.volumes.Se, this.volumes.Master), cancellationToken);
        await backend.SetVolumeAsync(new AudioVolumeChange(AudioChannel.Voice, this.volumes.Voice, this.volumes.Master), cancellationToken);
    }

    private async Task<AudioServiceResult> PlayBgmAsync(RuntimeEffect effect, CancellationToken cancellationToken)
    {
        if (!TryReadPayload(effect, "id", out var assetId))
        {
            return RuntimeError("KESR6001", "BGM playback requires an asset id.");
        }

        var asset = ResolveRequiredAsset(assetId, "bgm");
        if (asset.Result is not null)
        {
            return asset.Result;
        }

        if (bgm is not null)
        {
            await backend.StopAsync(new AudioStopRequest(AudioChannel.Bgm, bgm.AssetId, ReadDouble(effect, "fade")), cancellationToken);
        }

        var resolvedAsset = asset.Asset!;
        var options = new AudioPlaybackOptions(
            Loop: ReadBool(effect, "loop"),
            FadeSeconds: ReadDouble(effect, "fade"),
            Volume: volumes.Bgm * volumes.Master);
        await backend.PlayAsync(new AudioPlaybackRequest(AudioChannel.Bgm, resolvedAsset, options), cancellationToken);
        bgm = new AudioChannelItem(resolvedAsset.AssetId, resolvedAsset.ResolvedPath, options.Loop, options.Volume);
        return AudioServiceResult.Success();
    }

    private async Task<AudioServiceResult> StopBgmAsync(RuntimeEffect effect, CancellationToken cancellationToken)
    {
        if (bgm is not null)
        {
            await backend.StopAsync(new AudioStopRequest(AudioChannel.Bgm, bgm.AssetId, ReadDouble(effect, "fade")), cancellationToken);
            bgm = null;
        }

        return AudioServiceResult.Success();
    }

    private async Task<AudioServiceResult> PlaySeAsync(RuntimeEffect effect, CancellationToken cancellationToken)
    {
        if (!TryReadPayload(effect, "id", out var assetId))
        {
            return RuntimeError("KESR6001", "SE playback requires an asset id.");
        }

        var asset = ResolveRequiredAsset(assetId, "se");
        if (asset.Result is not null)
        {
            return asset.Result;
        }

        var resolvedAsset = asset.Asset!;
        var options = new AudioPlaybackOptions(Volume: volumes.Se * volumes.Master);
        await backend.PlayAsync(new AudioPlaybackRequest(AudioChannel.Se, resolvedAsset, options), cancellationToken);
        soundEffects.Add(new AudioChannelItem(resolvedAsset.AssetId, resolvedAsset.ResolvedPath, Loop: false, options.Volume));
        return AudioServiceResult.Success();
    }

    private async Task<AudioServiceResult> StopSeAsync(RuntimeEffect effect, CancellationToken cancellationToken)
    {
        if (!TryReadPayload(effect, "id", out var assetId))
        {
            return await StopAllSeAsync(cancellationToken);
        }

        await backend.StopAsync(new AudioStopRequest(AudioChannel.Se, assetId), cancellationToken);
        soundEffects.RemoveAll(effect => StringComparer.Ordinal.Equals(effect.AssetId, assetId));
        return AudioServiceResult.Success();
    }

    private async Task<AudioServiceResult> StopAllSeAsync(CancellationToken cancellationToken)
    {
        await backend.StopAsync(new AudioStopRequest(AudioChannel.Se), cancellationToken);
        soundEffects.Clear();
        return AudioServiceResult.Success();
    }

    private async Task<AudioServiceResult> StopVoiceAsync(CancellationToken cancellationToken)
    {
        await StopVoiceForTextSkipAsync(cancellationToken);
        return AudioServiceResult.Success();
    }

    private async Task<AudioServiceResult> PlayVoiceAsync(RuntimeEffect effect, CancellationToken cancellationToken)
    {
        if (!TryReadPayload(effect, "id", out var assetId))
        {
            return VoiceWarning("Voice playback was requested without an asset id.");
        }

        return await PlayVoiceByAssetIdAsync(assetId, cancellationToken);
    }

    private async Task<AudioServiceResult> PlayAutoVoiceAsync(RuntimeEffect effect, CancellationToken cancellationToken)
    {
        if (!TryReadPayload(effect, "id", out var assetId))
        {
            return VoiceWarning("Auto voice playback could not resolve an asset id.");
        }

        return await PlayVoiceByAssetIdAsync(assetId, cancellationToken);
    }

    private async Task<AudioServiceResult> PlayVoiceByAssetIdAsync(string assetId, CancellationToken cancellationToken)
    {
        var asset = ResolveOptionalVoiceAsset(assetId);
        if (asset.Result is not null)
        {
            return asset.Result;
        }

        if (voice is not null)
        {
            await backend.StopAsync(new AudioStopRequest(AudioChannel.Voice, voice.AssetId), cancellationToken);
        }

        var resolvedAsset = asset.Asset!;
        var options = new AudioPlaybackOptions(Volume: volumes.Voice * volumes.Master);
        await backend.PlayAsync(new AudioPlaybackRequest(AudioChannel.Voice, resolvedAsset, options), cancellationToken);
        voice = new AudioChannelItem(resolvedAsset.AssetId, resolvedAsset.ResolvedPath, Loop: false, options.Volume);
        return AudioServiceResult.Success();
    }

    private AssetResolveResult ResolveRequiredAsset(string assetId, string kind)
    {
        var asset = resources.ResolveAsset(assetId);
        if (asset is null)
        {
            return new AssetResolveResult(null, RuntimeError("KESR6002", $"Audio asset '{assetId}' was not found."));
        }

        if (!StringComparer.OrdinalIgnoreCase.Equals(asset.Kind, kind))
        {
            return new AssetResolveResult(null, RuntimeError("KESR6003", $"Audio asset '{assetId}' is not a {kind} asset."));
        }

        return new AssetResolveResult(asset, null);
    }

    private AssetResolveResult ResolveOptionalVoiceAsset(string assetId)
    {
        var asset = resources.ResolveAsset(assetId);
        if (asset is null)
        {
            return new AssetResolveResult(null, VoiceWarning($"Voice asset '{assetId}' was not found."));
        }

        if (!StringComparer.OrdinalIgnoreCase.Equals(asset.Kind, "voice"))
        {
            return new AssetResolveResult(null, RuntimeError("KESR6003", $"Audio asset '{assetId}' is not a voice asset."));
        }

        return new AssetResolveResult(asset, null);
    }

    private static bool TryReadPayload(RuntimeEffect effect, string key, out string value)
    {
        if (effect.Payload.TryGetValue(key, out var payloadValue) && !string.IsNullOrWhiteSpace(payloadValue))
        {
            value = payloadValue;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool ReadBool(RuntimeEffect effect, string key)
    {
        return effect.Payload.TryGetValue(key, out var value) &&
            bool.TryParse(value, out var parsed) &&
            parsed;
    }

    private static double ReadDouble(RuntimeEffect effect, string key)
    {
        if (effect.Payload.TryGetValue(key, out var value) &&
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return Math.Max(0d, parsed);
        }

        return 0d;
    }

    private static AudioVolumeState Clamp(AudioVolumeState state)
    {
        return new AudioVolumeState(
            Clamp01(state.Master),
            Clamp01(state.Bgm),
            Clamp01(state.Se),
            Clamp01(state.Voice));
    }

    private static double Clamp01(double value)
    {
        return Math.Clamp(value, 0d, 1d);
    }

    private static AudioServiceResult RuntimeError(string code, string message)
    {
        return AudioServiceResult.Failure(
            RuntimeFailureKind.Runtime,
            RuntimeDiagnostic.Error(code, message, RuntimeFailureKind.Runtime));
    }

    private static AudioServiceResult VoiceWarning(string message)
    {
        return AudioServiceResult.Success(RuntimeDiagnostic.Warning("KESW6001", message));
    }

    private sealed record AssetResolveResult(RuntimeAssetEntry? Asset, AudioServiceResult? Result);
}
