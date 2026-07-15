using System;
using System.Collections.Generic;
using KoromoEventScript.Unity;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

internal static class KesSampleProjectSetup
{
    private const string PrefabPath = "Assets/KesSystem.prefab";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string ManifestPath = "Assets/Scenario/manifest.kson";

    [MenuItem("Tools/KoromoEventScript/Configure Sample Project")]
    public static void Configure()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ConfigureSprites();
        ConfigureAddressables();
        var prefab = CreateKesSystemPrefab();
        ConfigureSampleScene(prefab);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }

    private static void ConfigureSprites()
    {
        var spriteGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/SampleAssets" });
        foreach (var guid in spriteGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                continue;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
        }
    }

    private static void ConfigureAddressables()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            settings = AddressableAssetSettings.Create(
                "Assets/AddressableAssetsData",
                "AddressableAssetSettings",
                true,
                true);
            AddressableAssetSettingsDefaultObject.Settings = settings;
        }

        var addresses = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["assets.bg.bg_morning"] = "Assets/SampleAssets/bg/bg_morning.png",
            ["assets.bg.bg_evening"] = "Assets/SampleAssets/bg/bg_evening.png",
            ["assets.actor.riku_normal"] = "Assets/SampleAssets/actor/riku_normal.png",
            ["assets.actor.riku_normal_dress"] = "Assets/SampleAssets/actor/riku_normal_dress.png",
            ["assets.actor.riku_pumpkin"] = "Assets/SampleAssets/actor/riku_pumpkin.png",
            ["assets.actor.riku_serious"] = "Assets/SampleAssets/actor/riku_serious.png",
            ["assets.actor.riku_sleep"] = "Assets/SampleAssets/actor/riku_sleep.png",
            ["assets.actor.riku_smile"] = "Assets/SampleAssets/actor/riku_smile.png",
            ["assets.actor.riku_trouble"] = "Assets/SampleAssets/actor/riku_trouble.png",
            ["assets.actor.amane_normal"] = "Assets/SampleAssets/actor/amane_normal.png",
            ["assets.actor.amane_smile"] = "Assets/SampleAssets/actor/amane_smile.png",
            ["assets.actor.noa_normal"] = "Assets/SampleAssets/actor/noa_normal.png",
            ["assets.actor.noa_smile"] = "Assets/SampleAssets/actor/noa_smile.png",
            ["assets.audio.bgm.bgm_001_alice2"] = "Assets/SampleAssets/audio/bgm/bgm_001_alice2.wav",
            ["assets.audio.se.se_001_door"] = "Assets/SampleAssets/audio/se/se_001_door.wav",
        };

        foreach (var pair in addresses)
        {
            var guid = AssetDatabase.AssetPathToGUID(pair.Value);
            if (string.IsNullOrEmpty(guid))
            {
                throw new InvalidOperationException("Sample asset was not imported: " + pair.Value);
            }

            var entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup);
            entry.address = pair.Key;
        }

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, null, true, true);
    }

    private static GameObject CreateKesSystemPrefab()
    {
        var root = new GameObject("KesSystem");
        try
        {
            var manager = root.AddComponent<KesManager>();
            manager.SetPlayOnStart(true);
            var resolver = root.AddComponent<KesAddressablesAssetResolver>();
            var presentation = root.AddComponent<KesPresentation>();
            var audioPresenter = root.AddComponent<KesAudioPresenter>();
            presentation.SetAssetResolver(resolver);
            presentation.SetAudioPresenter(audioPresenter);
            manager.SetPresentation(presentation);

            var spriteRoot = Child(root.transform, "SpriteRoot");
            var background = Child(spriteRoot.transform, "Background").AddComponent<SpriteRenderer>();
            var actorRoot = Child(spriteRoot.transform, "Actors").transform;
            presentation.SetSceneReferences(null, background, actorRoot);

            var canvasRoot = Child(root.transform, "CanvasRoot");
            var canvas = canvasRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPhysicalSize;
            scaler.physicalUnit = CanvasScaler.Unit.Points;
            scaler.fallbackScreenDPI = 96f;
            scaler.defaultSpriteDPI = 96f;
            canvasRoot.AddComponent<GraphicRaycaster>();

            var messageRoot = UiChild(canvasRoot.transform, "MessageRoot", new Vector2(160f, 60f), new Vector2(1760f, 340f));
            var panel = messageRoot.AddComponent<Image>();
            panel.color = new Color(0f, 0f, 0f, 0.75f);
            var speaker = TextChild(messageRoot.transform, "Speaker", new Vector2(40f, 190f), new Vector2(1560f, 260f), 30);
            var message = TextChild(messageRoot.transform, "Message", new Vector2(40f, 30f), new Vector2(1560f, 190f), 32);

            var choiceRoot = UiChild(canvasRoot.transform, "ChoiceRoot", new Vector2(460f, 360f), new Vector2(1460f, 760f));
            var choicePanel = choiceRoot.AddComponent<Image>();
            choicePanel.color = new Color(0f, 0f, 0f, 0.82f);
            var choices = TextChild(choiceRoot.transform, "Choices", new Vector2(40f, 40f), new Vector2(920f, 360f), 30);
            choiceRoot.SetActive(false);

            presentation.SetUiReferences(messageRoot, speaker, message, choiceRoot, choices);
            var audioRoot = Child(root.transform, "AudioRoot");
            var bgmSource = Child(audioRoot.transform, "BGM").AddComponent<AudioSource>();
            var voiceSource = Child(audioRoot.transform, "Voice").AddComponent<AudioSource>();
            var seRoot = Child(audioRoot.transform, "SE").transform;
            bgmSource.playOnAwake = false;
            voiceSource.playOnAwake = false;
            audioPresenter.SetReferences(resolver, bgmSource, voiceSource, seRoot);
            return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void ConfigureSampleScene(GameObject prefab)
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (var manager in UnityEngine.Object.FindObjectsByType<KesManager>())
        {
            UnityEngine.Object.DestroyImmediate(manager.gameObject);
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        var instanceManager = instance.GetComponent<KesManager>();
        instanceManager.SetBuildAsset(AssetDatabase.LoadAssetAtPath<KesBuildAsset>(ManifestPath));
        instanceManager.SetLocale(string.Empty);
        instanceManager.SetStartScriptId(string.Empty);
        EditorUtility.SetDirty(instanceManager);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    private static GameObject Child(Transform parent, string name)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child;
    }

    private static GameObject UiChild(Transform parent, string name, Vector2 min, Vector2 max)
    {
        var child = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)child.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(min.x / 1920f, min.y / 1080f);
        rect.anchorMax = new Vector2(max.x / 1920f, max.y / 1080f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return child;
    }

    private static Text TextChild(
        Transform parent,
        string name,
        Vector2 min,
        Vector2 max,
        int fontSize)
    {
        var child = UiChild(parent, name, min, max);
        var text = child.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = TextAnchor.UpperLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }
}
