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
        if (effect == null || effect.Kind != RuntimeEffectKind.Audio)
        {
            return;
        }

        EnsureResolver();
        switch (effect.Name)
        {
            case "audio.bgm":
                PlayBgm(effect.Payload);
                break;

            case "audio.bgm_stop":
                StopBgm(ReadFloat(effect.Payload, "fade", 0f));
                break;

            case "audio.se":
                PlaySe(Read(effect.Payload, "id", string.Empty));
                break;

            case "audio.se_stop":
                StopSe(Read(effect.Payload, "id", string.Empty));
                break;

            case "audio.se_stop_all":
                StopAllSe();
                break;

            case "text.vo":
                PlayVoice(Read(effect.Payload, "id", string.Empty));
                break;

            case "audio.voice_stop":
                StopVoice();
                break;
        }
    }

    public void StopAll()
    {
        StopBgm(0f);
        StopVoice();
        StopAllSe();
    }

    private void OnDisable()
    {
        StopAll();
    }

    private void PlayBgm(IReadOnlyDictionary<string, string> payload)
    {
        var id = Read(payload, "id", string.Empty);
        if (!Validate(id, bgmSource, "BGM"))
        {
            return;
        }

        var requestVersion = ++bgmRequestVersion;
        Release(ref bgmAssetId);
        bgmAssetId = id;
        var loop = ReadBool(payload, "loop", true);
        assetResolver.LoadAudioClip(
            id,
            clip =>
            {
                if (requestVersion != bgmRequestVersion || !StringComparer.Ordinal.Equals(bgmAssetId, id))
                {
                    assetResolver.Release(id);
                    return;
                }

                bgmSource.clip = clip;
                bgmSource.loop = loop;
                bgmSource.volume = 1f;
                bgmSource.Play();
            },
            error => PublishError("KESU3101", error));
    }

    private void StopBgm(float fadeSeconds)
    {
        bgmRequestVersion++;
        if (bgmSource != null)
        {
            if (fadeSeconds > 0f && bgmSource.isPlaying)
            {
                StartCoroutine(FadeAndStop(bgmSource, fadeSeconds, () => Release(ref bgmAssetId)));
                return;
            }

            bgmSource.Stop();
            bgmSource.clip = null;
        }

        Release(ref bgmAssetId);
    }

    private void PlayVoice(string id)
    {
        if (!Validate(id, voiceSource, "Voice"))
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
            },
            error => PublishWarning("KESU4101", error));
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

    private void PlaySe(string id)
    {
        if (assetResolver == null || string.IsNullOrWhiteSpace(id))
        {
            PublishError("KESU3102", "SE asset id or resolver is missing.");
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
            },
            error => PublishError("KESU3102", error));
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

    private bool Validate(string id, AudioSource source, string channel)
    {
        if (assetResolver != null && !string.IsNullOrWhiteSpace(id) && source != null)
        {
            return true;
        }

        PublishError("KESU3100", channel + " asset id, AudioSource, or resolver is missing.");
        return false;
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
