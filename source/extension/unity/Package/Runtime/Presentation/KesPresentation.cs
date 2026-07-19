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
    private KesChoiceItemView choiceItemTemplate;

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
    private readonly List<string> choiceLabels = new();
    private readonly List<KesChoiceItemView> choiceItems = new();
    private int operationVersion;
    private bool isBackRenderTarget;
    private bool isBackRenderTargetReady;
    private bool cameraAutofocus;
    private string fullMessageText = string.Empty;
    private Coroutine typewriterCoroutine;
    private float charactersPerSecond = 60f;

    public string BackgroundAssetId => backgroundAssetId;

    public string Speaker => speakerText == null ? string.Empty : speakerText.text;

    public string Message => messageText == null ? string.Empty : messageText.text;

    public int ActorCount => actors.Count;

    public int SelectedChoiceIndex { get; private set; } = -1;

    public int ChoiceCount => choiceLabels.Count;

    public event Action<RuntimeDiagnostic> DiagnosticPublished;

    public bool IsBackRenderTarget => isBackRenderTarget;

    public bool IsBackRenderTargetReady => isBackRenderTargetReady;

    public bool CameraAutofocus => cameraAutofocus;

    public bool IsTyping { get; private set; }

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
        KesChoiceItemView newChoiceItemTemplate)
    {
        messageRoot = newMessageRoot;
        speakerText = newSpeakerText;
        messageText = newMessageText;
        choiceRoot = newChoiceRoot;
        choiceItemTemplate = newChoiceItemTemplate;
        if (choiceItemTemplate != null)
        {
            choiceItemTemplate.gameObject.SetActive(false);
        }
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

    public bool TryGetChoiceItem(int choiceIndex, out KesChoiceItemView item)
    {
        item = null;
        if (choiceIndex < 0 || choiceIndex >= choiceLabels.Count || choiceIndex >= choiceItems.Count)
        {
            return false;
        }

        item = choiceItems[choiceIndex];
        return item != null;
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

    public void Execute(RuntimeEffect effect, Action<KesHostOperationResult> completed)
    {
        if (effect == null)
        {
            completed?.Invoke(Failure("KESU3000", "Presentation received a null effect."));
            return;
        }

        EnsureResolver();
        if (effect.Kind == RuntimeEffectKind.Audio)
        {
            if (audioPresenter == null)
            {
                completed?.Invoke(Failure("KESU3000", "Audio presenter is not configured."));
                return;
            }

            audioPresenter.Execute(effect, completed);
            return;
        }

        if (effect.Kind != RuntimeEffectKind.Scene)
        {
            Apply(effect);
            completed?.Invoke(KesHostOperationResult.Succeeded());
            return;
        }

        switch (effect.Name)
        {
            case "scene.rt_back":
                isBackRenderTarget = true;
                isBackRenderTargetReady = false;
                completed?.Invoke(KesHostOperationResult.Succeeded());
                break;

            case "scene.rt_front":
                if (!isBackRenderTarget)
                {
                    completed?.Invoke(Failure(
                        "KESU3010",
                        "scene.rt_front was called without an active back render target."));
                    break;
                }

                isBackRenderTarget = false;
                isBackRenderTargetReady = true;
                completed?.Invoke(KesHostOperationResult.Succeeded());
                break;

            case "scene.camera_autofocus":
                cameraAutofocus = ReadBool(effect.Payload, "enabled", false);
                completed?.Invoke(KesHostOperationResult.Succeeded());
                break;

            case "scene.bg":
                ExecuteBackground(effect.Payload, completed);
                break;

            case "scene.trans":
                ExecuteTransition(effect.Payload, completed);
                break;

            case "actor.cast":
                ExecuteActorCast(effect.Payload, completed);
                break;

            case "actor.show":
                ExecuteActorShow(effect.Payload, completed);
                break;

            case "actor.hide":
                ExecuteActorHide(effect.Payload, completed);
                break;

            case "actor.face":
                ExecuteActorFace(effect.Payload, completed);
                break;

            case "actor.move":
                ExecuteActorMove(effect.Payload, completed);
                break;

            case "actor.action_jump":
                ExecuteActorJump(effect.Payload, completed);
                break;

            default:
                completed?.Invoke(Failure(
                    "KESU3000",
                    "Unsupported presentation effect: " + effect.Name));
                break;
        }
    }

    public void CancelOperations()
    {
        operationVersion++;
        StopAllCoroutines();
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

        choiceLabels.Clear();
        for (var i = 0; i < continuation.PendingChoices.Count; i++)
        {
            choiceLabels.Add(continuation.PendingChoices[i].Text);
        }

        SelectedChoiceIndex = choiceLabels.Count > 0 ? 0 : -1;
        RenderChoiceItems();
    }

    public void SetSelectedChoiceIndex(int choiceIndex)
    {
        SelectedChoiceIndex = choiceIndex >= 0 && choiceIndex < choiceLabels.Count ? choiceIndex : -1;
        RenderChoiceItems();
    }

    public void ClearDialoguePage()
    {
        if (messageText != null)
        {
            messageText.text = string.Empty;
        }
    }

    public void SetTextSpeed(float value)
    {
        charactersPerSecond = Mathf.Max(1f, value * 60f);
    }

    public void SetAudioVolume(string key, float value)
    {
        audioPresenter?.SetChannelVolume(key, value);
    }

    public bool CompleteTyping()
    {
        if (!IsTyping)
        {
            return false;
        }

        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }

        IsTyping = false;
        if (messageText != null)
        {
            messageText.text = fullMessageText;
        }

        return true;
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
                BeginDialogue(
                    NormalizeSpeaker(Read(effect.Payload, "actor", string.Empty)),
                    Read(effect.Payload, "text", string.Empty));
                break;

            case "scenario.nar":
                BeginDialogue(string.Empty, Read(effect.Payload, "text", string.Empty));
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
            actor.Renderer.gameObject.SetActive(false);
        }
    }

    private void ExecuteBackground(
        IReadOnlyDictionary<string, string> payload,
        Action<KesHostOperationResult> completed)
    {
        var id = Read(payload, "id", string.Empty);
        if (string.IsNullOrWhiteSpace(id))
        {
            completed?.Invoke(Failure("KESU3001", "scene.bg did not provide a background asset id."));
            return;
        }

        var newAssetId = id.StartsWith("assets.", StringComparison.Ordinal)
            ? id
            : "assets.bg." + id;
        if (StringComparer.Ordinal.Equals(backgroundAssetId, newAssetId) &&
            backgroundRenderer != null &&
            backgroundRenderer.sprite != null)
        {
            completed?.Invoke(KesHostOperationResult.Succeeded());
            return;
        }

        var previousAssetId = backgroundAssetId;
        var version = operationVersion;
        LoadSprite(
            newAssetId,
            sprite =>
            {
                if (version != operationVersion || backgroundRenderer == null)
                {
                    assetResolver?.Release(newAssetId);
                    return;
                }

                backgroundAssetId = newAssetId;
                backgroundRenderer.sprite = sprite;
                ConfigureRenderer(backgroundRenderer, backgroundSortingLayer, backgroundSortingOrder);
                FitRenderer(backgroundRenderer, new Vector2(960f, 540f), new Vector2(1920f, 1080f));
                if (!string.IsNullOrEmpty(previousAssetId) &&
                    !StringComparer.Ordinal.Equals(previousAssetId, newAssetId))
                {
                    assetResolver?.Release(previousAssetId);
                }

                completed?.Invoke(KesHostOperationResult.Succeeded());
            },
            error => completed?.Invoke(Failure(
                "KESU3005",
                "Could not resolve sprite '" + newAssetId + "': " + error)));
    }

    private void ExecuteActorCast(
        IReadOnlyDictionary<string, string> payload,
        Action<KesHostOperationResult> completed)
    {
        var actor = EnsureActor(payload, visible: false, out var failure);
        if (actor == null)
        {
            completed?.Invoke(failure);
            return;
        }

        ExecuteActorSpriteLoad(actor, completed);
    }

    private void ExecuteActorShow(
        IReadOnlyDictionary<string, string> payload,
        Action<KesHostOperationResult> completed)
    {
        var actor = EnsureActor(payload, visible: true, out var failure);
        if (actor == null)
        {
            completed?.Invoke(failure);
            return;
        }

        ExecuteActorSpriteLoad(actor, completed);
    }

    private void ExecuteActorHide(
        IReadOnlyDictionary<string, string> payload,
        Action<KesHostOperationResult> completed)
    {
        var actorId = Read(payload, "actor", string.Empty);
        if (!actors.TryGetValue(actorId, out var actor))
        {
            completed?.Invoke(Failure(
                "KESU3003",
                "actor.hide referenced an actor that has not been cast: " + actorId));
            return;
        }

        actor.IsVisible = false;
        actor.Renderer.gameObject.SetActive(false);
        completed?.Invoke(KesHostOperationResult.Succeeded());
    }

    private void ExecuteActorFace(
        IReadOnlyDictionary<string, string> payload,
        Action<KesHostOperationResult> completed)
    {
        var actorId = Read(payload, "actor", string.Empty);
        if (!actors.TryGetValue(actorId, out var actor))
        {
            completed?.Invoke(Failure(
                "KESU3003",
                "actor.face referenced an actor that has not been cast: " + actorId));
            return;
        }

        actor.AssetBaseName = Read(payload, "assetBaseName", actor.AssetBaseName);
        actor.Face = Read(payload, "exp", Read(payload, "face", actor.Face));
        ExecuteActorSpriteLoad(actor, completed);
    }

    private void ExecuteActorMove(
        IReadOnlyDictionary<string, string> payload,
        Action<KesHostOperationResult> completed)
    {
        var actorId = Read(payload, "actor", string.Empty);
        if (!actors.TryGetValue(actorId, out var actor) || !actor.IsVisible)
        {
            completed?.Invoke(Failure(
                "KESU3004",
                "actor.move requires a visible cast actor: " + actorId));
            return;
        }

        var targetPosition = ReadFloat(payload, "pos", actor.Position);
        var duration = ReadFloat(payload, "duration", 0f);
        if (!IsValidDuration(duration))
        {
            completed?.Invoke(Failure("KESU3006", "actor.move duration must be finite and non-negative."));
            return;
        }

        if (duration <= 0f)
        {
            actor.Position = targetPosition;
            PositionActor(actor);
            completed?.Invoke(KesHostOperationResult.Succeeded());
            return;
        }

        StartCoroutine(AnimateActorMove(actor, targetPosition, duration, operationVersion, completed));
    }

    private void ExecuteActorJump(
        IReadOnlyDictionary<string, string> payload,
        Action<KesHostOperationResult> completed)
    {
        var actorId = Read(payload, "actor", string.Empty);
        if (!actors.TryGetValue(actorId, out var actor) || !actor.IsVisible)
        {
            completed?.Invoke(Failure(
                "KESU3007",
                "actor.action_jump requires a visible cast actor: " + actorId));
            return;
        }

        StartCoroutine(AnimateActorJump(actor, operationVersion, completed));
    }

    private void ExecuteTransition(
        IReadOnlyDictionary<string, string> payload,
        Action<KesHostOperationResult> completed)
    {
        var effect = Read(payload, "effect", "crossfade");
        var duration = ReadFloat(payload, "duration", 0f);
        if (effect is not ("none" or "fade" or "crossfade"))
        {
            completed?.Invoke(Failure("KESU3008", "Unsupported transition effect: " + effect));
            return;
        }

        if (!IsValidDuration(duration))
        {
            completed?.Invoke(Failure("KESU3008", "Transition duration must be finite and non-negative."));
            return;
        }

        if (effect == "none" || duration <= 0f)
        {
            isBackRenderTargetReady = false;
            completed?.Invoke(KesHostOperationResult.Succeeded());
            return;
        }

        StartCoroutine(AnimateTransition(duration, operationVersion, completed));
    }

    private ActorPresentationState EnsureActor(
        IReadOnlyDictionary<string, string> payload,
        bool visible,
        out KesHostOperationResult failure)
    {
        failure = null;
        var actorId = Read(payload, "actor", string.Empty);
        if (string.IsNullOrWhiteSpace(actorId))
        {
            failure = Failure("KESU3002", "Actor effect did not provide an actor id.");
            return null;
        }

        if (!actors.TryGetValue(actorId, out var actor))
        {
            var actorObject = new GameObject("Actor - " + NormalizeSpeaker(actorId));
            actorObject.transform.SetParent(actorRoot == null ? transform : actorRoot, false);
            actor = new ActorPresentationState(actorObject.AddComponent<SpriteRenderer>());
            actors.Add(actorId, actor);
        }

        actor.AssetBaseName = Read(
            payload,
            "assetBaseName",
            string.IsNullOrEmpty(actor.AssetBaseName)
                ? NormalizeSpeaker(actorId).ToLowerInvariant()
                : actor.AssetBaseName);
        actor.Face = Read(payload, "face", Read(payload, "exp", string.IsNullOrEmpty(actor.Face) ? "normal" : actor.Face));
        actor.Position = ReadFloat(payload, "pos", actor.Position);
        actor.SortingOffset = Mathf.RoundToInt(ReadFloat(payload, "layer", 0f) * 100f) +
            Mathf.RoundToInt(ReadFloat(payload, "z", 0f));
        actor.IsVisible = visible;
        actor.Renderer.gameObject.SetActive(visible);
        ConfigureRenderer(actor.Renderer, actorSortingLayer, actorSortingOrder + actor.SortingOffset);
        PositionActor(actor);
        return actor;
    }

    private void ExecuteActorSpriteLoad(
        ActorPresentationState actor,
        Action<KesHostOperationResult> completed)
    {
        var requestedId = "assets.actor." + actor.AssetBaseName + "_" + actor.Face;
        if (actor.IsAssetLoaded && StringComparer.Ordinal.Equals(actor.AssetId, requestedId))
        {
            completed?.Invoke(KesHostOperationResult.Succeeded());
            return;
        }

        var previousAssetId = actor.AssetId;
        var requestVersion = ++actor.RequestVersion;
        var version = operationVersion;
        actor.AssetId = requestedId;
        LoadSprite(
            requestedId,
            sprite =>
            {
                if (version != operationVersion ||
                    requestVersion != actor.RequestVersion ||
                    actor.Renderer == null)
                {
                    assetResolver?.Release(requestedId);
                    return;
                }

                actor.Renderer.sprite = sprite;
                actor.IsAssetLoaded = true;
                actor.Renderer.gameObject.SetActive(actor.IsVisible);
                PositionActor(actor);
                if (!string.IsNullOrEmpty(previousAssetId) &&
                    !StringComparer.Ordinal.Equals(previousAssetId, requestedId))
                {
                    assetResolver?.Release(previousAssetId);
                }

                completed?.Invoke(KesHostOperationResult.Succeeded());
            },
            error => completed?.Invoke(Failure(
                "KESU3005",
                "Could not resolve actor sprite '" + requestedId + "': " + error)));
    }

    private System.Collections.IEnumerator AnimateActorMove(
        ActorPresentationState actor,
        float targetPosition,
        float duration,
        int version,
        Action<KesHostOperationResult> completed)
    {
        var start = actor.Position;
        var elapsed = 0f;
        while (elapsed < duration && version == operationVersion)
        {
            elapsed += Time.unscaledDeltaTime;
            actor.Position = Mathf.Lerp(start, targetPosition, Mathf.Clamp01(elapsed / duration));
            PositionActor(actor);
            yield return null;
        }

        if (version == operationVersion)
        {
            actor.Position = targetPosition;
            PositionActor(actor);
            completed?.Invoke(KesHostOperationResult.Succeeded());
        }
    }

    private System.Collections.IEnumerator AnimateActorJump(
        ActorPresentationState actor,
        int version,
        Action<KesHostOperationResult> completed)
    {
        var start = actor.Renderer.transform.position;
        const float duration = 0.25f;
        var height = KesCoordinateMapper.DesignSizeToWorld(
            presentationCamera,
            new Vector2(0f, 90f),
            start.z).y;
        var elapsed = 0f;
        while (elapsed < duration && version == operationVersion)
        {
            elapsed += Time.unscaledDeltaTime;
            var normalized = Mathf.Clamp01(elapsed / duration);
            var offset = Mathf.Sin(normalized * Mathf.PI) * height;
            actor.Renderer.transform.position = start + new Vector3(0f, offset, 0f);
            yield return null;
        }

        if (version == operationVersion)
        {
            actor.Renderer.transform.position = start;
            completed?.Invoke(KesHostOperationResult.Succeeded());
        }
    }

    private System.Collections.IEnumerator AnimateTransition(
        float duration,
        int version,
        Action<KesHostOperationResult> completed)
    {
        var renderers = new List<SpriteRenderer>();
        if (backgroundRenderer != null)
        {
            renderers.Add(backgroundRenderer);
        }

        foreach (var actor in actors.Values)
        {
            if (actor.Renderer != null && actor.IsVisible)
            {
                renderers.Add(actor.Renderer);
            }
        }

        var elapsed = 0f;
        while (elapsed < duration && version == operationVersion)
        {
            elapsed += Time.unscaledDeltaTime;
            var alpha = Mathf.Clamp01(elapsed / duration);
            for (var i = 0; i < renderers.Count; i++)
            {
                var color = renderers[i].color;
                color.a = alpha;
                renderers[i].color = color;
            }

            yield return null;
        }

        if (version == operationVersion)
        {
            for (var i = 0; i < renderers.Count; i++)
            {
                var color = renderers[i].color;
                color.a = 1f;
                renderers[i].color = color;
            }

            isBackRenderTargetReady = false;
            completed?.Invoke(KesHostOperationResult.Succeeded());
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
        LoadSprite(
            assetId,
            onLoaded,
            error => PublishError("KESU3005", "Could not resolve sprite '" + assetId + "': " + error));
    }

    private void LoadSprite(
        string assetId,
        Action<Sprite> onLoaded,
        Action<string> onFailed)
    {
        if (assetResolver == null)
        {
            onFailed?.Invoke("Asset resolver is not configured.");
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
                    onFailed?.Invoke("Resolved sprite is null.");
                    return;
                }

                onLoaded(sprite);
            },
            onFailed);
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

    private void BeginDialogue(string speaker, string text)
    {
        CompleteTyping();
        fullMessageText = text ?? string.Empty;
        if (!Application.isPlaying)
        {
            IsTyping = false;
            SetDialogue(speaker, fullMessageText, true);
            return;
        }

        SetDialogue(speaker, string.Empty, true);
        if (string.IsNullOrEmpty(fullMessageText))
        {
            IsTyping = false;
            return;
        }

        IsTyping = true;
        typewriterCoroutine = StartCoroutine(TypeDialogue());
    }

    private System.Collections.IEnumerator TypeDialogue()
    {
        var visibleCharacters = 0f;
        while (visibleCharacters < fullMessageText.Length)
        {
            visibleCharacters += charactersPerSecond * Time.unscaledDeltaTime;
            if (messageText != null)
            {
                messageText.text = fullMessageText.Substring(
                    0,
                    Mathf.Min(fullMessageText.Length, Mathf.FloorToInt(visibleCharacters)));
            }

            yield return null;
        }

        if (messageText != null)
        {
            messageText.text = fullMessageText;
        }

        IsTyping = false;
        typewriterCoroutine = null;
    }

    private void HideChoices()
    {
        choiceLabels.Clear();
        SelectedChoiceIndex = -1;
        if (choiceRoot != null)
        {
            choiceRoot.SetActive(false);
        }

        for (var i = 0; i < choiceItems.Count; i++)
        {
            if (choiceItems[i] != null)
            {
                choiceItems[i].SetContent(string.Empty, false);
                choiceItems[i].gameObject.SetActive(false);
            }
        }
    }

    private void RenderChoiceItems()
    {
        if (choiceItemTemplate == null)
        {
            return;
        }

        EnsureChoiceItemCount(choiceLabels.Count);
        for (var i = 0; i < choiceLabels.Count; i++)
        {
            var item = choiceItems[i];
            if (item == null)
            {
                continue;
            }

            item.SetContent(choiceLabels[i], i == SelectedChoiceIndex);
            item.gameObject.SetActive(true);
        }

        for (var i = choiceLabels.Count; i < choiceItems.Count; i++)
        {
            if (choiceItems[i] != null)
            {
                choiceItems[i].gameObject.SetActive(false);
            }
        }
    }

    private void EnsureChoiceItemCount(int count)
    {
        while (choiceItems.Count < count)
        {
            var item = Instantiate(choiceItemTemplate, choiceItemTemplate.transform.parent, false);
            item.name = "ChoiceItem" + (choiceItems.Count + 1).ToString(CultureInfo.InvariantCulture);
            item.gameObject.SetActive(false);
            choiceItems.Add(item);
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
        CancelOperations();
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

    private KesHostOperationResult Failure(string code, string message)
    {
        var diagnostic = RuntimeDiagnostic.Error(code, message, RuntimeFailureKind.Runtime);
        Debug.LogError(code + ": " + message, this);
        DiagnosticPublished?.Invoke(diagnostic);
        return KesHostOperationResult.Failed(diagnostic);
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

    private static bool ReadBool(IReadOnlyDictionary<string, string> payload, string key, bool fallback)
    {
        return payload != null &&
            payload.TryGetValue(key, out var value) &&
            bool.TryParse(value, out var result)
            ? result
            : fallback;
    }

    private static bool IsValidDuration(float duration)
    {
        return duration >= 0f && !float.IsNaN(duration) && !float.IsInfinity(duration);
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
