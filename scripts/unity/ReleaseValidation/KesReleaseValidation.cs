using System;
using System.IO;
using System.Linq;
using KoromoEventScript.Unity;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Reporting;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.UI;
using UnityEditor.SceneManagement;
using UnityEngine;

internal static class KesReleaseValidation
{
    private const string PackageName = "com.koromosoft.koromo-event-script";
    private const string SampleName = "Basic Setup";

    public static void ImportSample()
    {
        var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(
            "Packages/" + PackageName + "/package.json");
        if (packageInfo == null)
        {
            throw new InvalidOperationException("KES package was not resolved in the clean project.");
        }

        var sample = Sample.FindByPackage(PackageName, packageInfo.version)
            .SingleOrDefault(candidate => string.Equals(candidate.displayName, SampleName, StringComparison.Ordinal));
        if (string.IsNullOrWhiteSpace(sample.displayName))
        {
            throw new InvalidOperationException("Basic Setup sample was not found in the KES package.");
        }

        if (!sample.Import(Sample.ImportOptions.OverridePreviousImports | Sample.ImportOptions.HideImportWindow))
        {
            throw new InvalidOperationException("Basic Setup sample import failed.");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("KES release validation sample import completed: " + sample.importPath);
    }

    public static void ValidateAndBuild()
    {
        var prefabGuid = AssetDatabase.FindAssets("KesSystem t:Prefab", new[] { "Assets/Samples" }).SingleOrDefault();
        if (string.IsNullOrEmpty(prefabGuid))
        {
            throw new InvalidOperationException("Imported KesSystem prefab was not found.");
        }

        var prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null || prefab.GetComponent<KesManager>() == null)
        {
            throw new InvalidOperationException("Imported KesSystem prefab has no KesManager.");
        }

        var missingScripts = prefab
            .GetComponentsInChildren<Transform>(true)
            .Sum(transform => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject));
        if (missingScripts != 0)
        {
            throw new InvalidOperationException("Imported KesSystem prefab has missing scripts: " + missingScripts);
        }

        if (AssetDatabase.FindAssets("t:InputActionAsset", new[] { "Assets/Samples" }).Length == 0)
        {
            throw new InvalidOperationException("Basic Setup Input Actions were not imported.");
        }

        var addressableSettingsGuid = AssetDatabase
            .FindAssets("t:AddressableAssetSettings", new[] { "Assets/Samples" })
            .SingleOrDefault();
        var addressableSettings = string.IsNullOrEmpty(addressableSettingsGuid)
            ? null
            : AssetDatabase.LoadAssetAtPath<AddressableAssetSettings>(
                AssetDatabase.GUIDToAssetPath(addressableSettingsGuid));
        if (addressableSettings == null)
        {
            throw new InvalidOperationException("Basic Setup Addressables settings were not imported.");
        }

        Directory.CreateDirectory(Path.Combine(Application.dataPath, "AddressableAssetsData"));
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        AddressableAssetSettingsDefaultObject.Settings = addressableSettings;
        AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult addressablesResult);
        if (!string.IsNullOrEmpty(addressablesResult.Error))
        {
            throw new InvalidOperationException("Addressables build failed: " + addressablesResult.Error);
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.GetComponent<KesManager>().SetPlayOnStart(false);
        var scenePath = "Assets/KesReleaseValidation.unity";
        EditorSceneManager.SaveScene(scene, scenePath);

        var buildOutput = Path.GetFullPath(Path.Combine(Application.dataPath, "../Build/KesReleaseValidation.exe"));
        Directory.CreateDirectory(Path.GetDirectoryName(buildOutput));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { scenePath },
            locationPathName = buildOutput,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        });
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new InvalidOperationException("Windows Player build failed: " + report.summary.result);
        }

        var resultPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../release-validation.json"));
        File.WriteAllText(
            resultPath,
            "{\"packageResolved\":true,\"sampleImported\":true,\"prefabValid\":true," +
            "\"inputActionsFound\":true,\"addressablesSettingsFound\":true,\"windowsPlayerBuilt\":true}");
        Debug.Log("KES clean project release validation completed: " + resultPath);
    }
}
