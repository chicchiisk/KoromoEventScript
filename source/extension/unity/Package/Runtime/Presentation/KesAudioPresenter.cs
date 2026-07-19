using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using KoromoEventScript.Runtime.Core.Diagnostics;
using KoromoEventScript.Runtime.Core.Effects;
using UnityEngine;

namespace KoromoEventScript.Unity
{

[DisallowMultipleComponent]
[AddComponentMenu("KoromoEventScript/KES Audio Presenter")]
public sealed class KesAudioPresenter : MonoBehaviour
{
    [SerializeField]
    private MonoBehaviour assetResolverBehaviour;

    [SerializeField]
    private AudioSource bgmSource;

    [SerializeField]
    private AudioSource voiceSource;

    [SerializeField]
    private Transform seRoot;

    private readonly List<SePlayback> sePlaybacks = new();
    private IKesAssetResolver assetResolver;
    private string bgmAssetId = string.Empty;
    private string voiceAssetId = string.Empty;
    private int bgmRequestVersion;
    private int voiceRequestVersion;

    public string BgmAssetId => bgmAssetId;

    public string VoiceAssetId => voiceAssetId;

    public event Action<RuntimeDiagnostic> DiagnosticPublished;

    public void SetChannelVolume(string key, float value)
    {
        var volume = Mathf.Clamp01(value);
        switch (key)
        {
            case "masterVolume":
                if (bgmSource != null)
                {
                    bgmSource.volume = volume;
                }

                if (voiceSource != null)
                {
                    voiceSource.volume = volume;
                }

                for (var i = 0; i < sePlaybacks.Count; i++)
                {
                    if (sePlaybacks[i].Source != null)
                    {
                        sePlaybacks[i].Source.volume = volume;
                    }
                }

                break;

            case "bgmVolume":
                if (bgmSource != null)
                {
                    bgmSource.volume = volume;
                }

                break;

            case "voiceVolume":
                if (voiceSource != null)
                {
                    voiceSource.volume = volume;
                }

                break;

            case "seVolume":
                for (var i = 0; i < sePlaybacks.Count; i++)
                {
                    if (sePlaybacks[i].Source != null)
                    {
                        sePlaybacks[i].Source.volume = volume;
                    }
                }

                break;
        }
    }

    public void SetReferences(
        IKesAssetResolver resolver,
        AudioSource newBgmSource,
        AudioSource newVoiceSource,
        Transform newSeRoot)
    {
        assetResolver = resolver;
        assetResolverBehaviour = resolver as MonoBehaviour;
        bgmSource = newBgmSource;
        voiceSource = newVoiceSource;
        seRoot = newSeRoot;
    }

    public void Apply(RuntimeEffect effect)
    {
        Execute(effect, _ => { });
    }

    public void Execute(RuntimeEffect effect, Action<KesHostOperationResult> completed)
    {
        if (effect == null || effect.Kind != RuntimeEffectKind.Audio)
        {
            completed?.Invoke(KesHostOperationResult.Failed(RuntimeDiagnostic.Error(
                "KESU3103",
                "Audio presenter received an invalid audio effect.",
                RuntimeFailureKind.Runtime)));
            return;
        }

        EnsureResolver();
        switch (effect.Name)
        {
            case "audio.bgm":
                PlayBgm(effect.Payload, completed);
                break;

            case "audio.bgm_stop":
                StopBgm(ReadFloat(effect.Payload, "fade", 0f), completed);
                break;

            case "audio.se":
                PlaySe(Read(effect.Payload, "id", string.Empty), completed);
                break;

            case "audio.se_stop":
                StopSe(Read(effect.Payload, "id", string.Empty));
                completed?.Invoke(KesHostOperationResult.Succeeded());
                break;

            case "audio.se_stop_all":
                StopAllSe();
                completed?.Invoke(KesHostOperationResult.Succeeded());
                break;

            case "text.vo":
                PlayVoice(Read(effect.Payload, "id", string.Empty), completed);
                break;

            case "audio.vo_auto":
                var automaticVoiceId = Read(effect.Payload, "id", string.Empty);
                if (string.IsNullOrWhiteSpace(automaticVoiceId))
                {
                    PublishWarning(
                        "KESU4102",
                        "Automatic Voice could not be resolved because the current dialogue did not provide a voice id.");
                    completed?.Invoke(KesHostOperationResult.Succeeded());
                }
                else
                {
                    PlayVoice(automaticVoiceId, completed);
                }

                break;

            case "audio.voice_stop":
                StopVoice();
                completed?.Invoke(KesHostOperationResult.Succeeded());
                break;

            default:
                completed?.Invoke(KesHostOperationResult.Failed(RuntimeDiagnostic.Error(
                    "KESU3103",
                    "Unsupported audio effect: " + effect.Name,
                    RuntimeFailureKind.Runtime)));
                break;
        }
    }

    public void StopAll()
    {
        StopBgm(0f, null);
        StopVoice();
        StopAllSe();
    }

    private void OnDisable()
    {
        StopAll();
    }

    private void PlayBgm(
        IReadOnlyDictionary<string, string> payload,
        Action<KesHostOperationResult> completed)
    {
        var id = Read(payload, "id", string.Empty);
        if (!Validate(id, bgmSource, "BGM", completed))
        {
            return;
        }

        var requestVersion = ++bgmRequestVersion;
        var previousAssetId = bgmAssetId;
        var loop = ReadBool(payload, "loop", true);
        var fade = ReadFloat(payload, "fade", 0f);
        assetResolver.LoadAudioClip(
            id,
            clip =>
            {
                if (requestVersion != bgmRequestVersion)
                {
                    assetResolver.Release(id);
                    return;
                }

                StartCoroutine(SwitchBgm(
                    previousAssetId,
                    id,
                    clip,
                    loop,
                    fade,
                    requestVersion,
                    completed));
            },
            error => CompleteError("KESU3101", error, completed));
    }

    private void StopBgm(float fadeSeconds, Action<KesHostOperationResult> completed)
    {
        bgmRequestVersion++;
        if (bgmSource != null)
        {
            if (fadeSeconds > 0f && bgmSource.isPlaying)
            {
                StartCoroutine(FadeAndStop(
                    bgmSource,
                    fadeSeconds,
                    () =>
                    {
                        Release(ref bgmAssetId);
                        completed?.Invoke(KesHostOperationResult.Succeeded());
                    }));
                return;
            }

            bgmSource.Stop();
            bgmSource.clip = null;
        }

        Release(ref bgmAssetId);
        completed?.Invoke(KesHostOperationResult.Succeeded());
    }

    private void PlayVoice(string id, Action<KesHostOperationResult> completed)
    {
        if (!Validate(id, voiceSource, "Voice", completed))
        {
            return;
        }

        var requestVersion = ++voiceRequestVersion;
        Release(ref voiceAssetId);
        voiceAssetId = id;
        assetResolver.LoadAudioClip(
            id,
            clip =>
            {
                if (requestVersion != voiceRequestVersion || !StringComparer.Ordinal.Equals(voiceAssetId, id))
                {
                    assetResolver.Release(id);
                    return;
                }

                voiceSource.clip = clip;
                voiceSource.loop = false;
                voiceSource.Play();
                completed?.Invoke(KesHostOperationResult.Succeeded());
            },
            error =>
            {
                PublishWarning("KESU4101", error);
                completed?.Invoke(KesHostOperationResult.Succeeded());
            });
    }

    private void StopVoice()
    {
        voiceRequestVersion++;
        if (voiceSource != null)
        {
            voiceSource.Stop();
            voiceSource.clip = null;
        }

        Release(ref voiceAssetId);
    }

    private void PlaySe(string id, Action<KesHostOperationResult> completed)
    {
        if (assetResolver == null || string.IsNullOrWhiteSpace(id))
        {
            CompleteError("KESU3102", "SE asset id or resolver is missing.", completed);
            return;
        }

        assetResolver.LoadAudioClip(
            id,
            clip =>
            {
                var playbackObject = new GameObject("SE - " + id);
                playbackObject.transform.SetParent(seRoot == null ? transform : seRoot, false);
                var source = playbackObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.clip = clip;
                var playback = new SePlayback(id, source);
                sePlaybacks.Add(playback);
                source.Play();
                StartCoroutine(ReleaseSeAfterPlayback(playback, clip.length));
                completed?.Invoke(KesHostOperationResult.Succeeded());
            },
            error => CompleteError("KESU3102", error, completed));
    }

    private void StopSe(string id)
    {
        for (var i = sePlaybacks.Count - 1; i >= 0; i--)
        {
            if (StringComparer.Ordinal.Equals(sePlaybacks[i].AssetId, id))
            {
                ReleaseSe(sePlaybacks[i]);
            }
        }
    }

    private void StopAllSe()
    {
        for (var i = sePlaybacks.Count - 1; i >= 0; i--)
        {
            ReleaseSe(sePlaybacks[i]);
        }
    }

    private IEnumerator ReleaseSeAfterPlayback(SePlayback playback, float seconds)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, seconds));
        ReleaseSe(playback);
    }

    private IEnumerator FadeAndStop(AudioSource source, float seconds, Action completed)
    {
        var startVolume = source.volume;
        var elapsed = 0f;
        while (elapsed < seconds && source != null)
        {
            elapsed += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(startVolume, 0f, Mathf.Clamp01(elapsed / seconds));
            yield return null;
        }

        if (source != null)
        {
            source.Stop();
            source.clip = null;
            source.volume = startVolume;
        }

        completed?.Invoke();
    }

    private void ReleaseSe(SePlayback playback)
    {
        if (!sePlaybacks.Remove(playback))
        {
            return;
        }

        if (playback.Source != null)
        {
            playback.Source.Stop();
            Destroy(playback.Source.gameObject);
        }

        assetResolver?.Release(playback.AssetId);
    }

    private bool Validate(
        string id,
        AudioSource source,
        string channel,
        Action<KesHostOperationResult> completed)
    {
        if (assetResolver != null && !string.IsNullOrWhiteSpace(id) && source != null)
        {
            return true;
        }

        CompleteError(
            "KESU3100",
            channel + " asset id, AudioSource, or resolver is missing.",
            completed);
        return false;
    }

    private IEnumerator SwitchBgm(
        string previousAssetId,
        string nextAssetId,
        AudioClip nextClip,
        bool loop,
        float fadeSeconds,
        int requestVersion,
        Action<KesHostOperationResult> completed)
    {
        var startVolume = bgmSource == null ? 1f : bgmSource.volume;
        if (fadeSeconds > 0f && bgmSource != null && bgmSource.isPlaying)
        {
            var elapsed = 0f;
            while (elapsed < fadeSeconds && requestVersion == bgmRequestVersion)
            {
                elapsed += Time.unscaledDeltaTime;
                bgmSource.volume = Mathf.Lerp(startVolume, 0f, Mathf.Clamp01(elapsed / fadeSeconds));
                yield return null;
            }
        }

        if (requestVersion != bgmRequestVersion || bgmSource == null)
        {
            assetResolver?.Release(nextAssetId);
            yield break;
        }

        bgmSource.Stop();
        bgmSource.clip = nextClip;
        bgmSource.loop = loop;
        bgmSource.volume = fadeSeconds > 0f ? 0f : startVolume;
        bgmSource.Play();
        if (!string.IsNullOrEmpty(previousAssetId))
        {
            assetResolver?.Release(previousAssetId);
        }

        bgmAssetId = nextAssetId;
        if (fadeSeconds > 0f)
        {
            var elapsed = 0f;
            while (elapsed < fadeSeconds && requestVersion == bgmRequestVersion)
            {
                elapsed += Time.unscaledDeltaTime;
                bgmSource.volume = Mathf.Lerp(0f, startVolume, Mathf.Clamp01(elapsed / fadeSeconds));
                yield return null;
            }
        }

        if (requestVersion == bgmRequestVersion)
        {
            bgmSource.volume = startVolume;
            completed?.Invoke(KesHostOperationResult.Succeeded());
        }
    }

    private void Release(ref string assetId)
    {
        if (assetResolver != null && !string.IsNullOrEmpty(assetId))
        {
            assetResolver.Release(assetId);
        }

        assetId = string.Empty;
    }

    private void EnsureResolver()
    {
        if (assetResolver == null && assetResolverBehaviour != null)
        {
            assetResolver = assetResolverBehaviour as IKesAssetResolver;
        }
    }

    private void PublishError(string code, string message)
    {
        var diagnostic = RuntimeDiagnostic.Error(code, message, RuntimeFailureKind.Runtime);
        Debug.LogError(code + ": " + message, this);
        DiagnosticPublished?.Invoke(diagnostic);
    }

    private void CompleteError(
        string code,
        string message,
        Action<KesHostOperationResult> completed)
    {
        var diagnostic = RuntimeDiagnostic.Error(code, message, RuntimeFailureKind.Runtime);
        Debug.LogError(code + ": " + message, this);
        DiagnosticPublished?.Invoke(diagnostic);
        completed?.Invoke(KesHostOperationResult.Failed(diagnostic));
    }

    private void PublishWarning(string code, string message)
    {
        var diagnostic = RuntimeDiagnostic.Warning(code, message);
        Debug.LogWarning(code + ": " + message, this);
        DiagnosticPublished?.Invoke(diagnostic);
    }

    private static string Read(IReadOnlyDictionary<string, string> payload, string key, string fallback)
    {
        return payload != null && payload.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;
    }

    private static float ReadFloat(IReadOnlyDictionary<string, string> payload, string key, float fallback)
    {
        return payload != null && payload.TryGetValue(key, out var value) &&
            float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : fallback;
    }

    private static bool ReadBool(IReadOnlyDictionary<string, string> payload, string key, bool fallback)
    {
        return payload != null && payload.TryGetValue(key, out var value) && bool.TryParse(value, out var result)
            ? result
            : fallback;
    }

    private sealed class SePlayback
    {
        public SePlayback(string assetId, AudioSource source)
        {
            AssetId = assetId;
            Source = source;
        }

        public string AssetId { get; }

        public AudioSource Source { get; }
    }
}
}
