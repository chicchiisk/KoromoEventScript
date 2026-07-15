using System;
using UnityEngine;

namespace KoromoEventScript.Unity
{

public interface IKesAssetResolver
{
    void LoadSprite(string assetId, Action<Sprite> onLoaded, Action<string> onFailed);

    void LoadAudioClip(string assetId, Action<AudioClip> onLoaded, Action<string> onFailed);

    void Release(string assetId);

    void ReleaseAll();
}
}
