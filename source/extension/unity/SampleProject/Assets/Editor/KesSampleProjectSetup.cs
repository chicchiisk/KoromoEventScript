using System;
using System.Collections.Generic;
using System.IO;
using KoromoEventScript.Unity;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;

internal static class KesSampleProjectSetup
{
    private const int SetupRevision = 4;
    private const string PrefabPath = "Assets/KesSystem.prefab";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string ManifestPath = "Assets/Scenario/manifest.kson";
    private const string ChoiceIconPath = "Assets/SampleAssets/ui/choice_selected.png";
    private const string BasicSetupExportRoot = "Assets/__KesBasicSetupExport";
    private const string AutoConfigureRequestFileName = "KesSampleProjectAutoConfigure.request";

    [InitializeOnLoadMethod]
    private static void ConfigureWhenRequested()
    {
        var requestPath = Path.GetFullPath(Path.Combine(
            Application.dataPath,
            "..",
            "Temp",
            AutoConfigureRequestFileName));
        if (!File.Exists(requestPath))
        {
            return;
        }

        EditorApplication.delayCall += () =>
        {
            try
            {
                Configure();
                Debug.Log($"KoromoEventScript sample project configuration completed (revision {SetupRevision}).");
            }
            finally
            {
                File.Delete(requestPath);
            }
        };
    }

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

    public static void ExportBasicSetupSample()
    {
        Configure();

        var packageSampleRoot = Path.GetFullPath(Path.Combine(
            Application.dataPath,
            "../../Package/Samples~/BasicSetup"));
        var generatedPaths = new[]
        {
            "AddressableAssetsData",
            "Input",
            "Runtime",
            "UI",
            "KesSystem.prefab",
            "KesSystem.prefab.meta",
        };

        foreach (var generatedPath in generatedPaths)
        {
            FileUtil.DeleteFileOrDirectory(Path.Combine(packageSampleRoot, generatedPath));
        }

        Directory.CreateDirectory(packageSampleRoot);
        CopyAssetWithMeta(PrefabPath, Path.Combine(packageSampleRoot, "KesSystem.prefab"));
        CopyAssetWithMeta(
            "Assets/Settings/InputSystem_Actions.inputactions",
            Path.Combine(packageSampleRoot, "Input/InputSystem_Actions.inputactions"));
        CopyAssetWithMeta(
            "Assets/SampleRuntime/KesSampleSaveHost.cs",
            Path.Combine(packageSampleRoot, "Runtime/KesSampleSaveHost.cs"));
        CopyAssetWithMeta(
            ChoiceIconPath,
            Path.Combine(packageSampleRoot, "UI/choice_selected.png"));

        AssetDatabase.DeleteAsset(BasicSetupExportRoot);
        Directory.CreateDirectory(BasicSetupExportRoot);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        AddressableAssetSettings.Create(
            BasicSetupExportRoot + "/AddressableAssetsData",
            "AddressableAssetSettings",
            true,
            true);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        FileUtil.CopyFileOrDirectory(
            Path.GetFullPath(Path.Combine(Application.dataPath, "../" + BasicSetupExportRoot + "/AddressableAssetsData")),
            Path.Combine(packageSampleRoot, "AddressableAssetsData"));
        FileUtil.CopyFileOrDirectory(
            Path.GetFullPath(Path.Combine(Application.dataPath, "../" + BasicSetupExportRoot + "/AddressableAssetsData.meta")),
            Path.Combine(packageSampleRoot, "AddressableAssetsData.meta"));

        AssetDatabase.DeleteAsset(BasicSetupExportRoot);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("KoromoEventScript Basic Setup sample export completed: " + packageSampleRoot);
    }

    private static void CopyAssetWithMeta(string sourceAssetPath, string destinationPath)
    {
        var sourcePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", sourceAssetPath));
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
        FileUtil.CopyFileOrDirectory(sourcePath, destinationPath);
        FileUtil.CopyFileOrDirectory(sourcePath + ".meta", destinationPath + ".meta");
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
            ["assets.voice.voice_001_sample"] = "Assets/SampleAssets/audio/voice/voice_001_sample.wav",
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
            manager.SetLogExecutionSource(true);
            var resolver = root.AddComponent<KesAddressablesAssetResolver>();
            var presentation = root.AddComponent<KesPresentation>();
            var audioPresenter = root.AddComponent<KesAudioPresenter>();
            var inputSource = root.AddComponent<KesInputSystemSource>();
            var inputController = root.AddComponent<KesInputController>();
            var saveHost = root.AddComponent<KesSampleSaveHost>();
            presentation.SetAssetResolver(resolver);
            presentation.SetAudioPresenter(audioPresenter);
            manager.SetPresentation(presentation);
            manager.SetSaveHost(saveHost);

            var spriteRoot = Child(root.transform, "SpriteRoot");
            var background = Child(spriteRoot.transform, "Background").AddComponent<SpriteRenderer>();
            var actorRoot = Child(spriteRoot.transform, "Actors").transform;
            presentation.SetSceneReferences(null, background, actorRoot);

            var canvasRoot = Child(root.transform, "CanvasRoot");
            var canvas = canvasRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasRoot.AddComponent<GraphicRaycaster>();

            var messageRoot = UiChild(canvasRoot.transform, "MessageRoot", 160f, 740f, 160f, 60f);
            var panel = messageRoot.AddComponent<Image>();
            panel.color = new Color(0f, 0f, 0f, 0.75f);
            var speaker = TextChild(messageRoot.transform, "Speaker", 40f, 13f, 40f, 177f, 30);
            var message = TextChild(messageRoot.transform, "Message", 40f, 90f, 40f, 30f, 32);

            var choiceRoot = CenteredUiChild(canvasRoot.transform, "ChoiceRoot", 1000f, 80f);
            var choicePanel = choiceRoot.AddComponent<Image>();
            choicePanel.color = new Color(0f, 0f, 0f, 0.82f);
            var choiceLayout = choiceRoot.AddComponent<VerticalLayoutGroup>();
            choiceLayout.padding = new RectOffset(32, 32, 24, 24);
            choiceLayout.spacing = 12f;
            choiceLayout.childAlignment = TextAnchor.UpperCenter;
            choiceLayout.childControlWidth = true;
            choiceLayout.childControlHeight = true;
            choiceLayout.childForceExpandWidth = true;
            choiceLayout.childForceExpandHeight = false;
            var choiceSizeFitter = choiceRoot.AddComponent<ContentSizeFitter>();
            choiceSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            choiceSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var choiceItemTemplate = CreateChoiceItemTemplate(choiceRoot.transform);
            choiceRoot.SetActive(false);

            var menuRoot = UiChild(canvasRoot.transform, "MenuRoot", 560f, 260f, 560f, 260f);
            var menuPanel = menuRoot.AddComponent<Image>();
            menuPanel.color = new Color(0f, 0f, 0f, 0.9f);
            var menuTitle = TextChild(menuRoot.transform, "MenuTitle", 40f, 40f, 40f, 440f, 36);
            menuTitle.text = "Menu";
            menuTitle.alignment = TextAnchor.MiddleCenter;
            var menuHelp = TextChild(menuRoot.transform, "MenuHelp", 40f, 140f, 40f, 80f, 26);
            menuHelp.text = "Right Click / Esc: Close\nTab: Auto\nCtrl: Skip";
            menuHelp.alignment = TextAnchor.MiddleCenter;
            menuRoot.SetActive(false);

            var eventSystemRoot = Child(root.transform, "EventSystem");
            eventSystemRoot.AddComponent<EventSystem>();
            eventSystemRoot.AddComponent<InputSystemUIInputModule>();

            presentation.SetUiReferences(messageRoot, speaker, message, choiceRoot, choiceItemTemplate);
            inputController.SetReferences(manager, inputSource, presentation, menuRoot);
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
        ConfigureGlobalLights(scene);
        EditorUtility.SetDirty(instanceManager);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    private static void ConfigureGlobalLights(Scene scene)
    {
        var sortingLayers = Array.ConvertAll(SortingLayer.layers, layer => layer.id);
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var light in root.GetComponentsInChildren<Light2D>(true))
            {
                if (light.lightType != Light2D.LightType.Global)
                {
                    continue;
                }

                light.targetSortingLayers = sortingLayers;
                EditorUtility.SetDirty(light);
            }
        }
    }

    private static GameObject Child(Transform parent, string name)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child;
    }

    private static GameObject UiChild(Transform parent, string name, float left, float top, float right, float bottom)
    {
        var child = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)child.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        return child;
    }

    private static GameObject CenteredUiChild(Transform parent, string name, float width, float y)
    {
        var child = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)child.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(width, 0f);
        return child;
    }

    private static KesChoiceItemView CreateChoiceItemTemplate(Transform parent)
    {
        var item = new GameObject("ChoiceItemTemplate", typeof(RectTransform));
        item.transform.SetParent(parent, false);

        var itemBackground = item.AddComponent<Image>();
        itemBackground.color = new Color(1f, 1f, 1f, 0.06f);
        var itemLayout = item.AddComponent<HorizontalLayoutGroup>();
        itemLayout.padding = new RectOffset(18, 22, 10, 10);
        itemLayout.spacing = 18f;
        itemLayout.childAlignment = TextAnchor.MiddleLeft;
        itemLayout.childControlWidth = true;
        itemLayout.childControlHeight = true;
        itemLayout.childForceExpandWidth = false;
        itemLayout.childForceExpandHeight = true;
        var itemSize = item.AddComponent<LayoutElement>();
        itemSize.minHeight = 72f;

        var iconObject = new GameObject("SelectionIcon", typeof(RectTransform));
        iconObject.transform.SetParent(item.transform, false);
        var icon = iconObject.AddComponent<Image>();
        icon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ChoiceIconPath);
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        var iconSize = iconObject.AddComponent<LayoutElement>();
        iconSize.minWidth = 48f;
        iconSize.preferredWidth = 48f;
        iconSize.minHeight = 48f;
        iconSize.preferredHeight = 48f;
        iconSize.flexibleWidth = 0f;

        var labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.transform.SetParent(item.transform, false);
        var label = labelObject.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 30;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleLeft;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        var labelSize = labelObject.AddComponent<LayoutElement>();
        labelSize.flexibleWidth = 1f;

        var view = item.AddComponent<KesChoiceItemView>();
        view.SetReferences(icon, label);
        item.SetActive(false);
        return view;
    }

    private static Text TextChild(
        Transform parent,
        string name,
        float left,
        float top,
        float right,
        float bottom,
        int fontSize)
    {
        var child = UiChild(parent, name, left, top, right, bottom);
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
