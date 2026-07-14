using System;
using System.IO;
using KoromoEventScript.Runtime.Core.Klib;
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
        var loadResult = new KlibModuleLoader().Load(data, context.assetPath);
        if (!loadResult.Succeeded)
        {
            var detail = loadResult.Diagnostics.Count > 0
                ? loadResult.Diagnostics[0].Message
                : "Klib validation failed.";
            throw new InvalidDataException("KESU1001: " + detail);
        }

        var asset = ScriptableObject.CreateInstance<KesKlibAsset>();
        asset.name = Path.GetFileNameWithoutExtension(context.assetPath);
        asset.SetImportedData(data);

        context.AddObjectToAsset("main", asset);
        context.SetMainObject(asset);
    }
}
}
