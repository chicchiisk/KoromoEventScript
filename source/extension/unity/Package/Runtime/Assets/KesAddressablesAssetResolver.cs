using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace KoromoEventScript.Unity
{

[DisallowMultipleComponent]
[AddComponentMenu("KoromoEventScript/KES Addressables Asset Resolver")]
public sealed class KesAddressablesAssetResolver : MonoBehaviour, IKesAssetResolver
{
    private readonly Dictionary<string, AssetHandle<Sprite>> spriteHandles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AssetHandle<AudioClip>> audioHandles = new(StringComparer.Ordinal);
    private bool isShuttingDown;

    public int LoadedAssetCount => spriteHandles.Count + audioHandles.Count;

    public void LoadSprite(string assetId, Action<Sprite> onLoaded, Action<string> onFailed)
    {
        Load(assetId, spriteHandles, onLoaded, onFailed);
    }

    public void LoadAudioClip(string assetId, Action<AudioClip> onLoaded, Action<string> onFailed)
    {
        Load(assetId, audioHandles, onLoaded, onFailed);
    }

    public void Release(string assetId)
    {
        if (string.IsNullOrEmpty(assetId))
        {
            return;
        }

        if (Release(assetId, spriteHandles))
        {
            return;
        }

        Release(assetId, audioHandles);
    }

    public void ReleaseAll()
    {
        ReleaseAll(spriteHandles);
        ReleaseAll(audioHandles);
    }

    private void OnEnable()
    {
        isShuttingDown = false;
    }

    private void OnDestroy()
    {
        isShuttingDown = true;
        ReleaseAll();
    }

    private void Load<T>(
        string assetId,
        Dictionary<string, AssetHandle<T>> handles,
        Action<T> onLoaded,
        Action<string> onFailed)
        where T : UnityEngine.Object
    {
        if (isShuttingDown)
        {
            onFailed?.Invoke("The resolver is shutting down.");
            return;
        }

        if (string.IsNullOrWhiteSpace(assetId))
        {
            onFailed?.Invoke("Addressables asset id must not be empty.");
            return;
        }

        if (handles.TryGetValue(assetId, out var cached))
        {
            cached.ReferenceCount++;
            Observe(cached.Handle, assetId, onLoaded, onFailed);
            return;
        }

        var handle = Addressables.LoadAssetAsync<T>(assetId);
        handles.Add(assetId, new AssetHandle<T>(handle));
        Observe(
            handle,
            assetId,
            onLoaded,
            error =>
            {
                ReleaseFailed(assetId, handles);
                onFailed?.Invoke(error);
            });
    }

    private void Observe<T>(
        AsyncOperationHandle<T> handle,
        string assetId,
        Action<T> onLoaded,
        Action<string> onFailed)
        where T : UnityEngine.Object
    {
        if (handle.IsDone)
        {
            Complete(handle, assetId, onLoaded, onFailed);
            return;
        }

        handle.Completed += completed => Complete(completed, assetId, onLoaded, onFailed);
    }

    private void Complete<T>(
        AsyncOperationHandle<T> handle,
        string assetId,
        Action<T> onLoaded,
        Action<string> onFailed)
        where T : UnityEngine.Object
    {
        if (isShuttingDown)
        {
            return;
        }

        if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
        {
            onLoaded?.Invoke(handle.Result);
            return;
        }

        var message = handle.OperationException == null
            ? "Addressables did not return an asset."
            : handle.OperationException.Message;
        onFailed?.Invoke("Addressables key '" + assetId + "' failed: " + message);
    }

    private static bool Release<T>(string assetId, Dictionary<string, AssetHandle<T>> handles)
        where T : UnityEngine.Object
    {
        if (!handles.TryGetValue(assetId, out var cached))
        {
            return false;
        }

        cached.ReferenceCount--;
        if (cached.ReferenceCount > 0)
        {
            return true;
        }

        if (cached.Handle.IsValid())
        {
            Addressables.Release(cached.Handle);
        }

        handles.Remove(assetId);
        return true;
    }

    private static void ReleaseAll<T>(Dictionary<string, AssetHandle<T>> handles)
        where T : UnityEngine.Object
    {
        foreach (var cached in handles.Values)
        {
            if (cached.Handle.IsValid())
            {
                Addressables.Release(cached.Handle);
            }
        }

        handles.Clear();
    }

    private static void ReleaseFailed<T>(string assetId, Dictionary<string, AssetHandle<T>> handles)
        where T : UnityEngine.Object
    {
        if (!handles.TryGetValue(assetId, out var cached))
        {
            return;
        }

        if (cached.Handle.IsValid())
        {
            Addressables.Release(cached.Handle);
        }

        handles.Remove(assetId);
    }

    private sealed class AssetHandle<T> where T : UnityEngine.Object
    {
        public AssetHandle(AsyncOperationHandle<T> handle)
        {
            Handle = handle;
            ReferenceCount = 1;
        }

        public AsyncOperationHandle<T> Handle { get; }

        public int ReferenceCount { get; set; }
    }
}
}
