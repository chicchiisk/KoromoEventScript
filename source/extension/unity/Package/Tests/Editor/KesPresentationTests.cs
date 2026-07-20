using System;
using System.Collections.Generic;
using System.Linq;
using KoromoEventScript.Runtime.Core.Diagnostics;
using KoromoEventScript.Runtime.Core.Effects;
using KoromoEventScript.Runtime.Core.Execution;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace KoromoEventScript.Unity.Editor.Tests
{

public sealed class KesPresentationTests
{
    [Test]
    public void AddressablesResolver_EmptyAssetIdFailsWithoutCreatingHandle()
    {
        var gameObject = new GameObject("AddressablesResolverTest");

        try
        {
            var resolver = gameObject.AddComponent<KesAddressablesAssetResolver>();
            string error = null;

            resolver.LoadSprite(string.Empty, _ => Assert.Fail("Empty id must not resolve."), value => error = value);

            Assert.That(error, Is.EqualTo("Addressables asset id must not be empty."));
            Assert.That(resolver.LoadedAssetCount, Is.EqualTo(0));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void ApplySceneEffects_UpdatesBackgroundAndActorRenderers()
    {
        var fixture = new PresentationFixture();

        try
        {
            var background = fixture.CreateSprite("background");
            var actorNormal = fixture.CreateSprite("actor-normal");
            var actorSmile = fixture.CreateSprite("actor-smile");
            fixture.Resolver.Add("assets.bg.bg_morning", background);
            fixture.Resolver.Add("assets.actor.riku_normal", actorNormal);
            fixture.Resolver.Add("assets.actor.riku_smile", actorSmile);

            fixture.Presentation.Apply(Batch(
                SceneEffect("scene.bg", ("id", "bg_morning")),
                SceneEffect(
                    "actor.show",
                    ("actor", "actors.Riku"),
                    ("assetBaseName", "riku"),
                    ("face", "normal"),
                    ("pos", "0"),
                    ("layer", "1"),
                    ("z", "2"))));

            Assert.That(fixture.Presentation.BackgroundAssetId, Is.EqualTo("assets.bg.bg_morning"));
            Assert.That(fixture.BackgroundRenderer.sprite, Is.SameAs(background));
            Assert.That(fixture.Presentation.ActorCount, Is.EqualTo(1));
            Assert.That(fixture.Presentation.TryGetActorRenderer("actors.Riku", out var actorRenderer), Is.True);
            Assert.That(actorRenderer.sprite, Is.SameAs(actorNormal));
            Assert.That(actorRenderer.sortingOrder, Is.EqualTo(102));
            AssertVector(
                actorRenderer.transform.position,
                KesCoordinateMapper.DesignToWorld(fixture.Camera, new Vector2(960f, 600f)));

            fixture.Presentation.Apply(Batch(
                SceneEffect(
                    "actor.face",
                    ("actor", "actors.Riku"),
                    ("assetBaseName", "riku"),
                    ("exp", "smile")),
                SceneEffect("actor.move", ("actor", "actors.Riku"), ("pos", "1"))));

            Assert.That(fixture.Presentation.GetActorAssetId("actors.Riku"), Is.EqualTo("assets.actor.riku_smile"));
            Assert.That(actorRenderer.sprite, Is.SameAs(actorSmile));
            AssertVector(
                actorRenderer.transform.position,
                KesCoordinateMapper.DesignToWorld(fixture.Camera, new Vector2(1380f, 600f)));

            fixture.Presentation.Apply(Batch(SceneEffect("actor.hide", ("actor", "actors.Riku"))));

            Assert.That(actorRenderer.gameObject.activeSelf, Is.False);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void ApplyUiEffectsAndSelectionContinuation_UpdatesStandardUiState()
    {
        var fixture = new PresentationFixture();

        try
        {
            fixture.Presentation.Apply(Batch(UiEffect(
                "scenario.say",
                ("actor", "actors.Riku"),
                ("text", "こんにちは"))));

            Assert.That(fixture.MessageRoot.activeSelf, Is.True);
            Assert.That(fixture.SpeakerText.text, Is.EqualTo("Riku"));
            Assert.That(fixture.MessageText.text, Is.EqualTo("こんにちは"));

            fixture.Presentation.ApplyContinuation(new RuntimeContinuation(
                RuntimeContinuationKind.WaitingForSelection,
                null,
                new[] { 10, 20 },
                "選択してください",
                new[]
                {
                    new RuntimeSelectionChoice("はい", 10),
                    new RuntimeSelectionChoice("いいえ", 20),
                }));

            Assert.That(fixture.ChoiceRoot.activeSelf, Is.True);
            Assert.That(fixture.Presentation.ChoiceCount, Is.EqualTo(2));
            Assert.That(fixture.Presentation.SelectedChoiceIndex, Is.EqualTo(0));
            Assert.That(fixture.Presentation.TryGetChoiceItem(0, out var firstChoice), Is.True);
            Assert.That(firstChoice.Label, Is.EqualTo("はい"));
            Assert.That(firstChoice.IsSelected, Is.True);
            Assert.That(fixture.Presentation.TryGetChoiceItem(1, out var secondChoice), Is.True);
            Assert.That(secondChoice.Label, Is.EqualTo("いいえ"));
            Assert.That(secondChoice.IsSelected, Is.False);

            fixture.Presentation.SetSelectedChoiceIndex(1);
            Assert.That(firstChoice.IsSelected, Is.False);
            Assert.That(secondChoice.IsSelected, Is.True);

            fixture.Presentation.Apply(Batch(UiEffect("text.r")));
            Assert.That(fixture.MessageText.text, Is.EqualTo("こんにちは\n"));

            fixture.Presentation.Apply(Batch(UiEffect("text.cm")));
            fixture.Presentation.ApplyContinuation(RuntimeContinuation.Completed);

            Assert.That(fixture.MessageRoot.activeSelf, Is.False);
            Assert.That(fixture.MessageText.text, Is.Empty);
            Assert.That(fixture.ChoiceRoot.activeSelf, Is.False);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void ActorFaceChangedWhileSpriteIsLoading_ReleasesSupersededRequest()
    {
        var fixture = new PresentationFixture();

        try
        {
            var resolver = new DeferredSpriteResolver();
            var normal = fixture.CreateSprite("actor-normal");
            var smile = fixture.CreateSprite("actor-smile");
            fixture.Presentation.SetAssetResolver(resolver);

            fixture.Presentation.Apply(Batch(SceneEffect(
                "actor.show",
                ("actor", "actors.Riku"),
                ("assetBaseName", "riku"),
                ("face", "normal"))));
            fixture.Presentation.Apply(Batch(SceneEffect(
                "actor.face",
                ("actor", "actors.Riku"),
                ("exp", "smile"))));

            Assert.That(resolver.ReleasedAssetIds, Is.EqualTo(new[] { "assets.actor.riku_normal" }));

            resolver.Complete("assets.actor.riku_normal", normal);
            resolver.Complete("assets.actor.riku_smile", smile);

            Assert.That(fixture.Presentation.TryGetActorRenderer("actors.Riku", out var renderer), Is.True);
            Assert.That(renderer.sprite, Is.SameAs(smile));
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void Execute_HostSceneEffects_ReportCompletionAndRetainHiddenActorAsset()
    {
        var fixture = new PresentationFixture();

        try
        {
            fixture.Resolver.Add("assets.actor.riku_normal", fixture.CreateSprite("actor-normal"));
            var results = new List<KesHostOperationResult>();

            fixture.Presentation.Execute(
                SceneEffect(
                    "actor.show",
                    ("actor", "actors.Riku"),
                    ("assetBaseName", "riku"),
                    ("face", "normal")),
                results.Add);
            fixture.Presentation.Execute(
                SceneEffect("actor.hide", ("actor", "actors.Riku")),
                results.Add);

            Assert.That(results.Select(static result => result.Status), Is.All.EqualTo(KesHostOperationStatus.Succeeded));
            Assert.That(fixture.Presentation.TryGetActorRenderer("actors.Riku", out var renderer), Is.True);
            Assert.That(renderer.gameObject.activeSelf, Is.False);
            Assert.That(renderer.sprite, Is.Not.Null);
            Assert.That(fixture.Resolver.ReleasedAssetIds, Is.Empty);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void Execute_UnknownSceneEffectFailsWithStableDiagnostic()
    {
        var fixture = new PresentationFixture();

        try
        {
            KesHostOperationResult result = null;

            LogAssert.Expect(LogType.Error, "KESU3000: Unsupported presentation effect: scene.unknown");
            fixture.Presentation.Execute(SceneEffect("scene.unknown"), value => result = value);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Status, Is.EqualTo(KesHostOperationStatus.Failed));
            Assert.That(result.Diagnostic.Code, Is.EqualTo("KESU3000"));
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void Execute_AutomaticVoiceWithoutResolvedIdWarnsAndContinues()
    {
        var gameObject = new GameObject("KesAudioPresenterTest");
        try
        {
            var presenter = gameObject.AddComponent<KesAudioPresenter>();
            RuntimeDiagnostic warning = null;
            KesHostOperationResult result = null;
            presenter.DiagnosticPublished += value => warning = value;

            presenter.Execute(
                new RuntimeEffect(
                    RuntimeEffectKind.Audio,
                    "audio.vo_auto",
                    new Dictionary<string, string>()),
                value => result = value);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Status, Is.EqualTo(KesHostOperationStatus.Succeeded));
            Assert.That(warning, Is.Not.Null);
            Assert.That(warning.Code, Is.EqualTo("KESU4102"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    private static RuntimeEffectBatch Batch(params RuntimeEffect[] effects)
    {
        return new RuntimeEffectBatch(effects, Array.Empty<RuntimeDiagnostic>());
    }

    private static void AssertVector(Vector3 actual, Vector3 expected)
    {
        Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f));
        Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f));
        Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.0001f));
    }

    private static RuntimeEffect SceneEffect(string name, params (string Key, string Value)[] payload)
    {
        return Effect(RuntimeEffectKind.Scene, name, payload);
    }

    private static RuntimeEffect UiEffect(string name, params (string Key, string Value)[] payload)
    {
        return Effect(RuntimeEffectKind.Ui, name, payload);
    }

    private static RuntimeEffect Effect(
        RuntimeEffectKind kind,
        string name,
        IReadOnlyList<(string Key, string Value)> payload)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < payload.Count; i++)
        {
            values.Add(payload[i].Key, payload[i].Value);
        }

        return new RuntimeEffect(kind, name, values);
    }

    private sealed class PresentationFixture : IDisposable
    {
        private readonly List<UnityEngine.Object> createdObjects = new();

        public PresentationFixture()
        {
            Root = CreateGameObject("PresentationFixture");
            Camera = CreateGameObject("Camera").AddComponent<Camera>();
            Camera.orthographic = true;
            Camera.orthographicSize = 5.4f;
            Camera.transform.position = new Vector3(0f, 0f, -10f);
            BackgroundRenderer = CreateGameObject("Background").AddComponent<SpriteRenderer>();
            var actorRoot = CreateGameObject("Actors").transform;
            MessageRoot = CreateGameObject("MessageRoot");
            SpeakerText = CreateText("SpeakerText");
            MessageText = CreateText("MessageText");
            ChoiceRoot = CreateRectGameObject("ChoiceRoot");
            ChoiceRoot.AddComponent<VerticalLayoutGroup>();
            var sizeFitter = ChoiceRoot.AddComponent<ContentSizeFitter>();
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ChoiceItemTemplate = CreateChoiceItemTemplate();
            Presentation = Root.AddComponent<KesPresentation>();
            Presentation.SetSceneReferences(Camera, BackgroundRenderer, actorRoot);
            Presentation.SetUiReferences(MessageRoot, SpeakerText, MessageText, ChoiceRoot, ChoiceItemTemplate);
            Resolver = new ImmediateAssetResolver();
            Presentation.SetAssetResolver(Resolver);
        }

        public GameObject Root { get; }

        public Camera Camera { get; }

        public SpriteRenderer BackgroundRenderer { get; }

        public GameObject MessageRoot { get; }

        public Text SpeakerText { get; }

        public Text MessageText { get; }

        public GameObject ChoiceRoot { get; }

        public KesChoiceItemView ChoiceItemTemplate { get; }

        public KesPresentation Presentation { get; }

        public ImmediateAssetResolver Resolver { get; }

        public Sprite CreateSprite(string name)
        {
            var texture = new Texture2D(2, 2);
            texture.name = name + "-texture";
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 1f);
            sprite.name = name;
            createdObjects.Add(texture);
            createdObjects.Add(sprite);
            return sprite;
        }

        public void Dispose()
        {
            for (var i = createdObjects.Count - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(createdObjects[i]);
            }

            UnityEngine.Object.DestroyImmediate(Root);
        }

        private GameObject CreateGameObject(string name)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(Root == null ? null : Root.transform);
            createdObjects.Add(gameObject);
            return gameObject;
        }

        private GameObject CreateRectGameObject(string name)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(Root.transform);
            createdObjects.Add(gameObject);
            return gameObject;
        }

        private KesChoiceItemView CreateChoiceItemTemplate()
        {
            var item = new GameObject("ChoiceItemTemplate", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            item.transform.SetParent(ChoiceRoot.transform);
            createdObjects.Add(item);

            var iconObject = new GameObject("SelectionIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObject.transform.SetParent(item.transform);
            createdObjects.Add(iconObject);

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(item.transform);
            createdObjects.Add(labelObject);

            var view = item.AddComponent<KesChoiceItemView>();
            view.SetReferences(iconObject.GetComponent<Image>(), labelObject.GetComponent<Text>());
            item.SetActive(false);
            return view;
        }

        private Text CreateText(string name)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            gameObject.transform.SetParent(Root.transform);
            createdObjects.Add(gameObject);
            return gameObject.GetComponent<Text>();
        }
    }

    private sealed class ImmediateAssetResolver : IKesAssetResolver
    {
        private readonly Dictionary<string, Sprite> sprites = new(StringComparer.Ordinal);
        private readonly List<string> releasedAssetIds = new();

        public IReadOnlyList<string> ReleasedAssetIds => releasedAssetIds;

        public void Add(string assetId, Sprite sprite)
        {
            sprites.Add(assetId, sprite);
        }

        public void LoadSprite(string assetId, Action<Sprite> onLoaded, Action<string> onFailed)
        {
            if (sprites.TryGetValue(assetId, out var sprite))
            {
                onLoaded(sprite);
                return;
            }

            onFailed("Asset was not registered by the test resolver.");
        }

        public void LoadAudioClip(string assetId, Action<AudioClip> onLoaded, Action<string> onFailed)
        {
            onFailed("Audio is not used by this presentation test.");
        }

        public void Release(string assetId)
        {
            releasedAssetIds.Add(assetId);
        }

        public void ReleaseAll()
        {
        }
    }

    private sealed class DeferredSpriteResolver : IKesAssetResolver
    {
        private readonly Dictionary<string, Action<Sprite>> completions = new(StringComparer.Ordinal);
        private readonly List<string> releasedAssetIds = new();

        public IReadOnlyList<string> ReleasedAssetIds => releasedAssetIds;

        public void LoadSprite(string assetId, Action<Sprite> onLoaded, Action<string> onFailed)
        {
            completions.Add(assetId, onLoaded);
        }

        public void LoadAudioClip(string assetId, Action<AudioClip> onLoaded, Action<string> onFailed)
        {
            onFailed("Audio is not used by this presentation test.");
        }

        public void Release(string assetId)
        {
            releasedAssetIds.Add(assetId);
        }

        public void ReleaseAll()
        {
        }

        public void Complete(string assetId, Sprite sprite)
        {
            completions[assetId](sprite);
        }
    }
}
}
