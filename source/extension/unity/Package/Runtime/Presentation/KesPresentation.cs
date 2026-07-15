using System;
using System.Collections.Generic;
using System.Globalization;
using KoromoEventScript.Runtime.Core.Diagnostics;
using KoromoEventScript.Runtime.Core.Effects;
using KoromoEventScript.Runtime.Core.Execution;
using UnityEngine;
using UnityEngine.UI;

namespace KoromoEventScript.Unity
{

[DisallowMultipleComponent]
[AddComponentMenu("KoromoEventScript/KES Presentation")]
public sealed class KesPresentation : MonoBehaviour
{
    private const float ActorWidth = 680f;
    private const float ActorHeight = 960f;
    private const float ActorTop = 120f;
    private const float ActorCenter = 960f;
    private const float ActorPositionStep = 420f;

    [SerializeField]
    private Camera presentationCamera;

    [SerializeField]
    private SpriteRenderer backgroundRenderer;

    [SerializeField]
    private Transform actorRoot;

    [SerializeField]
    private GameObject messageRoot;

    [SerializeField]
    private Text speakerText;

    [SerializeField]
    private Text messageText;

    [SerializeField]
    private GameObject choiceRoot;

    [SerializeField]
    private Text choiceText;

    [SerializeField]
    private MonoBehaviour assetResolverBehaviour;

    [SerializeField]
    private KesAudioPresenter audioPresenter;

    [SerializeField]
    private string backgroundSortingLayer = "KES Background";

    [SerializeField]
    private string actorSortingLayer = "KES Actor";

    [SerializeField]
    private int backgroundSortingOrder = -1000;

    [SerializeField]
    private int actorSortingOrder;

    private readonly Dictionary<string, ActorPresentationState> actors = new(StringComparer.Ordinal);
    private IKesAssetResolver assetResolver;
    private string backgroundAssetId = string.Empty;

    public string BackgroundAssetId => backgroundAssetId;

    public string Speaker => speakerText == null ? string.Empty : speakerText.text;

    public string Message => messageText == null ? string.Empty : messageText.text;

    public int ActorCount => actors.Count;

    public event Action<RuntimeDiagnostic> DiagnosticPublished;

    public void SetSceneReferences(Camera camera, SpriteRenderer background, Transform actorsRoot)
    {
        presentationCamera = camera;
        backgroundRenderer = background;
        actorRoot = actorsRoot;
        ConfigureRenderer(backgroundRenderer, backgroundSortingLayer, backgroundSortingOrder);
    }

    public void SetUiReferences(
        GameObject newMessageRoot,
        Text newSpeakerText,
        Text newMessageText,
        GameObject newChoiceRoot,
        Text newChoiceText)
    {
        messageRoot = newMessageRoot;
        speakerText = newSpeakerText;
        messageText = newMessageText;
        choiceRoot = newChoiceRoot;
        choiceText = newChoiceText;
    }

    public void SetAssetResolver(IKesAssetResolver resolver)
    {
        assetResolver = resolver;
        assetResolverBehaviour = resolver as MonoBehaviour;
    }

    public void SetAudioPresenter(KesAudioPresenter presenter)
    {
        audioPresenter = presenter;
    }

    public bool TryGetActorRenderer(string actorId, out SpriteRenderer renderer)
    {
        renderer = null;
        if (!actors.TryGetValue(actorId, out var actor))
        {
            return false;
        }

        renderer = actor.Renderer;
        return renderer != null;
    }

    public string GetActorAssetId(string actorId)
    {
        return actors.TryGetValue(actorId, out var actor) ? actor.AssetId : string.Empty;
    }

    public void Apply(RuntimeEffectBatch batch)
    {
        if (batch == null)
        {
            return;
        }

        EnsureResolver();
        for (var i = 0; i < batch.Effects.Count; i++)
        {
            Apply(batch.Effects[i]);
        }
    }

    public void ApplyContinuation(RuntimeContinuation continuation)
    {
        if (continuation == null)
        {
            HideChoices();
            return;
        }

        if (continuation.Kind != RuntimeContinuationKind.WaitingForSelection)
        {
            HideChoices();
            return;
        }

        if (choiceRoot != null)
        {
            choiceRoot.SetActive(true);
        }

        if (choiceText != null)
        {
            var choices = new string[continuation.PendingChoices.Count];
            for (var i = 0; i < continuation.PendingChoices.Count; i++)
            {
                choices[i] = continuation.PendingChoices[i].Text;
            }

            choiceText.text = string.Join("\n", choices);
        }
    }

    public void ResetPresentation()
    {
        if (assetResolver != null && !string.IsNullOrEmpty(backgroundAssetId))
        {
            assetResolver.Release(backgroundAssetId);
        }

        backgroundAssetId = string.Empty;
        if (backgroundRenderer != null)
        {
            backgroundRenderer.sprite = null;
        }

        foreach (var actor in actors.Values)
        {
            if (assetResolver != null && !string.IsNullOrEmpty(actor.AssetId) && actor.IsAssetLoaded)
            {
                assetResolver.Release(actor.AssetId);
            }

            if (actor.Renderer != null)
            {
                Destroy(actor.Renderer.gameObject);
            }
        }

        actors.Clear();
        SetDialogue(string.Empty, string.Empty, false);
        HideChoices();
    }

    private void Awake()
    {
        if (presentationCamera == null)
        {
            presentationCamera = Camera.main;
        }

        EnsureResolver();
        ConfigureRenderer(backgroundRenderer, backgroundSortingLayer, backgroundSortingOrder);
    }

    private void Apply(RuntimeEffect effect)
    {
        if (effect == null)
        {
            return;
        }

        switch (effect.Kind)
        {
            case RuntimeEffectKind.Scene:
                ApplyScene(effect);
                break;

            case RuntimeEffectKind.Ui:
                ApplyUi(effect);
                break;

            case RuntimeEffectKind.Audio:
                audioPresenter?.Apply(effect);
                break;
        }
    }

    private void ApplyScene(RuntimeEffect effect)
    {
        switch (effect.Name)
        {
            case "scene.bg":
                SetBackground(Read(effect.Payload, "id", string.Empty));
                break;

            case "actor.show":
                ShowActor(effect.Payload);
                break;

            case "actor.face":
                SetActorFace(effect.Payload);
                break;

            case "actor.hide":
                HideActor(effect.Payload);
                break;

            case "actor.move":
                MoveActor(effect.Payload);
                break;
        }
    }

    private void ApplyUi(RuntimeEffect effect)
    {
        switch (effect.Name)
        {
            case "scenario.say":
                SetDialogue(
                    NormalizeSpeaker(Read(effect.Payload, "actor", string.Empty)),
                    Read(effect.Payload, "text", string.Empty),
                    true);
                break;

            case "scenario.nar":
                SetDialogue(string.Empty, Read(effect.Payload, "text", string.Empty), true);
                break;

            case "text.cm":
                SetDialogue(string.Empty, string.Empty, false);
                break;

            case "text.r":
                if (messageText != null)
                {
                    messageText.text += "\n";
                }
                break;
        }
    }

    private void SetBackground(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            PublishError("KESU3001", "scene.bg did not provide a background asset id.");
            return;
        }

        var newAssetId = id.StartsWith("assets.", StringComparison.Ordinal)
            ? id
            : "assets.bg." + id;
        var isSameAsset = StringComparer.Ordinal.Equals(backgroundAssetId, newAssetId);
        if (isSameAsset && backgroundRenderer != null && backgroundRenderer.sprite != null)
        {
            return;
        }

        if (assetResolver != null &&
            !string.IsNullOrEmpty(backgroundAssetId) &&
            !isSameAsset)
        {
            assetResolver.Release(backgroundAssetId);
        }

        backgroundAssetId = newAssetId;
        var requestedId = backgroundAssetId;
        LoadSprite(requestedId, sprite =>
        {
            if (backgroundRenderer == null || !StringComparer.Ordinal.Equals(backgroundAssetId, requestedId))
            {
                return;
            }

            backgroundRenderer.sprite = sprite;
            ConfigureRenderer(backgroundRenderer, backgroundSortingLayer, backgroundSortingOrder);
            FitRenderer(backgroundRenderer, new Vector2(960f, 540f), new Vector2(1920f, 1080f));
        });
    }

    private void ShowActor(IReadOnlyDictionary<string, string> payload)
    {
        var actorId = Read(payload, "actor", string.Empty);
        if (string.IsNullOrWhiteSpace(actorId))
        {
            PublishError("KESU3002", "actor.show did not provide an actor id.");
            return;
        }

        if (!actors.TryGetValue(actorId, out var actor))
        {
            var actorObject = new GameObject("Actor - " + NormalizeSpeaker(actorId));
            actorObject.transform.SetParent(actorRoot == null ? transform : actorRoot, false);
            var renderer = actorObject.AddComponent<SpriteRenderer>();
            actor = new ActorPresentationState(renderer);
            actors.Add(actorId, actor);
        }

        actor.AssetBaseName = Read(payload, "assetBaseName", NormalizeSpeaker(actorId).ToLowerInvariant());
        actor.Face = Read(payload, "face", "normal");
        actor.Position = ReadFloat(payload, "pos", 0f);
        actor.SortingOffset = Mathf.RoundToInt(ReadFloat(payload, "layer", 0f) * 100f) +
            Mathf.RoundToInt(ReadFloat(payload, "z", 0f));
        actor.IsVisible = true;
        actor.Renderer.gameObject.SetActive(true);
        ConfigureRenderer(actor.Renderer, actorSortingLayer, actorSortingOrder + actor.SortingOffset);
        PositionActor(actor);
        RequestActorSprite(actor);
    }

    private void SetActorFace(IReadOnlyDictionary<string, string> payload)
    {
        var actorId = Read(payload, "actor", string.Empty);
        if (!actors.TryGetValue(actorId, out var actor))
        {
            PublishError("KESU3003", "actor.face referenced an actor that is not being presented: " + actorId);
            return;
        }

        actor.AssetBaseName = Read(payload, "assetBaseName", actor.AssetBaseName);
        actor.Face = Read(payload, "exp", Read(payload, "face", actor.Face));
        RequestActorSprite(actor);
    }

    private void HideActor(IReadOnlyDictionary<string, string> payload)
    {
        var actorId = Read(payload, "actor", string.Empty);
        if (actors.TryGetValue(actorId, out var actor) && actor.Renderer != null)
        {
            actor.IsVisible = false;
            actor.RequestVersion++;
            if (assetResolver != null && !string.IsNullOrEmpty(actor.AssetId))
            {
                assetResolver.Release(actor.AssetId);
                actor.IsAssetLoaded = false;
                actor.Renderer.sprite = null;
            }

            actor.Renderer.gameObject.SetActive(false);
        }
    }

    private void MoveActor(IReadOnlyDictionary<string, string> payload)
    {
        var actorId = Read(payload, "actor", string.Empty);
        if (!actors.TryGetValue(actorId, out var actor))
        {
            PublishError("KESU3004", "actor.move referenced an actor that is not being presented: " + actorId);
            return;
        }

        actor.Position = ReadFloat(payload, "pos", actor.Position);
        PositionActor(actor);
    }

    private void RequestActorSprite(ActorPresentationState actor)
    {
        var requestedId = "assets.actor." + actor.AssetBaseName + "_" + actor.Face;
        if (actor.IsAssetLoaded && StringComparer.Ordinal.Equals(actor.AssetId, requestedId))
        {
            return;
        }

        if (assetResolver != null &&
            !string.IsNullOrEmpty(actor.AssetId) &&
            !StringComparer.Ordinal.Equals(actor.AssetId, requestedId))
        {
            assetResolver.Release(actor.AssetId);
            actor.IsAssetLoaded = false;
        }

        actor.AssetId = requestedId;
        var requestVersion = ++actor.RequestVersion;
        LoadSprite(requestedId, sprite =>
        {
            if (!actor.IsVisible ||
                requestVersion != actor.RequestVersion ||
                !StringComparer.Ordinal.Equals(actor.AssetId, requestedId) ||
                actor.Renderer == null)
            {
                return;
            }

            actor.Renderer.sprite = sprite;
            actor.IsAssetLoaded = true;
            PositionActor(actor);
        });
    }

    private void PositionActor(ActorPresentationState actor)
    {
        if (actor.Renderer == null)
        {
            return;
        }

        var center = new Vector2(
            ActorCenter + (actor.Position * ActorPositionStep),
            ActorTop + (ActorHeight * 0.5f));
        actor.Renderer.transform.position = KesCoordinateMapper.DesignToWorld(presentationCamera, center);
        FitRenderer(actor.Renderer, center, new Vector2(ActorWidth, ActorHeight));
    }

    private void FitRenderer(SpriteRenderer renderer, Vector2 center, Vector2 designSize)
    {
        if (renderer == null || renderer.sprite == null)
        {
            return;
        }

        renderer.transform.position = KesCoordinateMapper.DesignToWorld(presentationCamera, center, renderer.transform.position.z);
        var worldSize = KesCoordinateMapper.DesignSizeToWorld(presentationCamera, designSize, renderer.transform.position.z);
        var spriteSize = renderer.sprite.bounds.size;
        if (spriteSize.x <= 0f || spriteSize.y <= 0f)
        {
            return;
        }

        renderer.transform.localScale = new Vector3(
            worldSize.x / spriteSize.x,
            worldSize.y / spriteSize.y,
            1f);
    }

    private void LoadSprite(string assetId, Action<Sprite> onLoaded)
    {
        if (assetResolver == null)
        {
            return;
        }

        var requestedId = assetId;
        assetResolver.LoadSprite(
            requestedId,
            sprite =>
            {
                if (sprite == null)
                {
                    PublishError("KESU3005", "Resolved sprite is null: " + requestedId);
                    return;
                }

                onLoaded(sprite);
            },
            error => PublishError("KESU3005", "Could not resolve sprite '" + requestedId + "': " + error));
    }

    private void SetDialogue(string speaker, string text, bool visible)
    {
        if (speakerText != null)
        {
            speakerText.text = speaker;
        }

        if (messageText != null)
        {
            messageText.text = text;
        }

        if (messageRoot != null)
        {
            messageRoot.SetActive(visible);
        }
    }

    private void HideChoices()
    {
        if (choiceRoot != null)
        {
            choiceRoot.SetActive(false);
        }

        if (choiceText != null)
        {
            choiceText.text = string.Empty;
        }
    }

    private void EnsureResolver()
    {
        if (assetResolver == null && assetResolverBehaviour != null)
        {
            assetResolver = assetResolverBehaviour as IKesAssetResolver;
        }
    }

    private void OnDestroy()
    {
        if (assetResolver != null && !string.IsNullOrEmpty(backgroundAssetId))
        {
            assetResolver.Release(backgroundAssetId);
        }

        foreach (var actor in actors.Values)
        {
            if (assetResolver != null && actor.IsAssetLoaded && !string.IsNullOrEmpty(actor.AssetId))
            {
                assetResolver.Release(actor.AssetId);
            }
        }

        audioPresenter?.StopAll();
    }

    private void PublishError(string code, string message)
    {
        var diagnostic = RuntimeDiagnostic.Error(code, message, RuntimeFailureKind.Runtime);
        Debug.LogError(code + ": " + message, this);
        DiagnosticPublished?.Invoke(diagnostic);
    }

    private static void ConfigureRenderer(SpriteRenderer renderer, string sortingLayer, int sortingOrder)
    {
        if (renderer == null)
        {
            return;
        }

        renderer.sortingLayerName = HasSortingLayer(sortingLayer) ? sortingLayer : "Default";
        renderer.sortingOrder = sortingOrder;
    }

    private static bool HasSortingLayer(string sortingLayer)
    {
        var layers = SortingLayer.layers;
        for (var i = 0; i < layers.Length; i++)
        {
            if (StringComparer.Ordinal.Equals(layers[i].name, sortingLayer))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeSpeaker(string actor)
    {
        if (string.IsNullOrWhiteSpace(actor))
        {
            return string.Empty;
        }

        var normalized = actor.Replace('\\', '/');
        var slashIndex = normalized.LastIndexOf('/');
        if (slashIndex >= 0)
        {
            normalized = normalized.Substring(slashIndex + 1);
        }

        var dotIndex = normalized.LastIndexOf('.');
        return dotIndex >= 0 && dotIndex + 1 < normalized.Length
            ? normalized.Substring(dotIndex + 1)
            : normalized;
    }

    private static string Read(IReadOnlyDictionary<string, string> payload, string key, string fallback)
    {
        return payload != null && payload.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;
    }

    private static float ReadFloat(IReadOnlyDictionary<string, string> payload, string key, float fallback)
    {
        return payload != null &&
            payload.TryGetValue(key, out var value) &&
            float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? number
            : fallback;
    }

    private sealed class ActorPresentationState
    {
        public ActorPresentationState(SpriteRenderer renderer)
        {
            Renderer = renderer;
        }

        public SpriteRenderer Renderer { get; }

        public string AssetBaseName { get; set; } = string.Empty;

        public string Face { get; set; } = string.Empty;

        public string AssetId { get; set; } = string.Empty;

        public float Position { get; set; }

        public int SortingOffset { get; set; }

        public bool IsAssetLoaded { get; set; }

        public bool IsVisible { get; set; }

        public int RequestVersion { get; set; }
    }
}
}
