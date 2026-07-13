using System;
using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace KoromoEventScript.Unity.Editor
{

[ScriptedImporter(1, "klib")]
public sealed class KesKlibImporter : ScriptedImporter
{
    public override void OnImportAsset(AssetImportContext context)
    {
        var data = File.ReadAllBytes(context.assetPath);
        if (data.Length < 24 ||
            data[0] != (byte)'K' ||
            data[1] != (byte)'L' ||
            data[2] != (byte)'I' ||
            data[3] != (byte)'B')
        {
            throw new InvalidDataException("KESU1001: Klib file has an invalid or truncated header.");
        }

        var asset = ScriptableObject.CreateInstance<KesKlibAsset>();
        asset.name = Path.GetFileNameWithoutExtension(context.assetPath);
        asset.SetImportedData(data);

        context.AddObjectToAsset("main", asset);
        context.SetMainObject(asset);
    }
}
}
