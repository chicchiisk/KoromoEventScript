using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace KoromoEventScript.Unity.Editor
{

[ScriptedImporter(1, "kson")]
public sealed class KesKsonImporter : ScriptedImporter
{
    public override void OnImportAsset(AssetImportContext context)
    {
        var json = File.ReadAllText(context.assetPath);
        var manifest = ParseManifest(json);
        var scriptReferences = ResolveScripts(context, manifest);

        var asset = ScriptableObject.CreateInstance<KesBuildAsset>();
        asset.name = Path.GetFileNameWithoutExtension(context.assetPath);
        asset.SetImportedData(
            json,
            manifest.schemaVersion,
            manifest.gameId,
            manifest.build.buildId,
            manifest.defaultLocale,
            scriptReferences);

        context.AddObjectToAsset("main", asset);
        context.SetMainObject(asset);
    }

    internal static KesManifestData ParseManifest(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException("KESU1101: manifest.kson is empty.");
        }

        KesManifestData manifest;
        try
        {
            manifest = JsonUtility.FromJson<KesManifestData>(json);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("KESU1101: manifest.kson is not valid JSON.", exception);
        }

        if (manifest == null || manifest.schemaVersion != "1.0")
        {
            throw new InvalidDataException("KESU1102: manifest.kson must use schemaVersion '1.0'.");
        }

        if (!string.Equals(manifest.target, "unity", StringComparison.Ordinal))
        {
            throw new InvalidDataException("KESU1103: manifest.kson target must be 'unity'.");
        }

        if (string.IsNullOrWhiteSpace(manifest.gameId) ||
            string.IsNullOrWhiteSpace(manifest.defaultLocale) ||
            manifest.build == null ||
            string.IsNullOrWhiteSpace(manifest.build.buildId))
        {
            throw new InvalidDataException("KESU1104: manifest.kson is missing required metadata.");
        }

        if (manifest.scripts == null || manifest.scripts.Length == 0)
        {
            throw new InvalidDataException("KESU1105: manifest.kson must contain at least one script.");
        }

        return manifest;
    }

    private static List<KesScriptAssetReference> ResolveScripts(
        AssetImportContext context,
        KesManifestData manifest)
    {
        var result = new List<KesScriptAssetReference>();
        var keys = new HashSet<string>(StringComparer.Ordinal);

        AddScripts(context, manifest.scripts, manifest.defaultLocale, keys, result);
        if (manifest.localizations != null)
        {
            foreach (var localization in manifest.localizations)
            {
                if (localization == null || string.IsNullOrWhiteSpace(localization.locale))
                {
                    throw new InvalidDataException("KESU1106: localization entry has no locale.");
                }

                AddScripts(context, localization.scripts, localization.locale, keys, result);
            }
        }

        return result;
    }

    private static void AddScripts(
        AssetImportContext context,
        KesManifestScriptData[] scripts,
        string fallbackLocale,
        HashSet<string> keys,
        List<KesScriptAssetReference> result)
    {
        if (scripts == null)
        {
            throw new InvalidDataException("KESU1105: script list is missing.");
        }

        foreach (var script in scripts)
        {
            if (script == null ||
                string.IsNullOrWhiteSpace(script.scriptId) ||
                string.IsNullOrWhiteSpace(script.klibPath))
            {
                throw new InvalidDataException("KESU1105: script entry is incomplete.");
            }

            var locale = string.IsNullOrWhiteSpace(script.locale) ? fallbackLocale : script.locale;
            var key = locale + "\n" + script.scriptId;
            if (!keys.Add(key))
            {
                throw new InvalidDataException(
                    $"KESU1107: duplicate scriptId '{script.scriptId}' for locale '{locale}'.");
            }

            var assetPath = ResolveAssetPath(context.assetPath, script.klibPath);
            context.DependsOnSourceAsset(assetPath);
            var klib = AssetDatabase.LoadAssetAtPath<KesKlibAsset>(assetPath);
            if (klib == null)
            {
                throw new FileNotFoundException(
                    $"KESU1108: referenced Klib asset was not found: {assetPath}",
                    assetPath);
            }

            result.Add(new KesScriptAssetReference(script.scriptId, locale, klib));
        }
    }

    internal static string ResolveAssetPath(string manifestAssetPath, string relativePath)
    {
        if (Path.IsPathRooted(relativePath) || relativePath.IndexOf('\\') >= 0)
        {
            throw new InvalidDataException("KESU1109: Klib path must be a forward-slash relative path.");
        }

        var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrEmpty(projectRoot))
        {
            throw new InvalidOperationException("KESU1110: Unity project root could not be resolved.");
        }

        var manifestDirectory = Path.GetDirectoryName(Path.Combine(projectRoot, manifestAssetPath));
        var fullPath = Path.GetFullPath(Path.Combine(manifestDirectory ?? projectRoot, relativePath));
        var assetsRoot = Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("KESU1109: Klib path escapes the Unity Assets directory.");
        }

        return Path.GetRelativePath(projectRoot, fullPath).Replace('\\', '/');
    }
}
}
