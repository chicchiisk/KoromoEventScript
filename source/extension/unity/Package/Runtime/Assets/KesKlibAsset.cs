using System;
using KoromoEventScript.Runtime.Core.Klib;
using UnityEngine;

namespace KoromoEventScript.Unity
{

public sealed class KesKlibAsset : ScriptableObject
{
    [SerializeField]
    private byte[] data = Array.Empty<byte>();

    public ReadOnlyMemory<byte> Data => data;

    public void SetImportedData(byte[] importedData)
    {
        data = importedData ?? throw new ArgumentNullException(nameof(importedData));
    }

    public KlibModuleLoadResult LoadModule(string sourceName = null)
    {
        return new KlibModuleLoader().Load(data, string.IsNullOrEmpty(sourceName) ? name : sourceName);
    }
}
}
