using KoromoEventScript.Runtime.Core.Execution;
using UnityEngine;

namespace KoromoEventScript.Unity
{

[DisallowMultipleComponent]
[AddComponentMenu("KoromoEventScript/KES Input Controller")]
public sealed class KesInputController : MonoBehaviour
{
    [SerializeField]
    private KesManager manager;

    [SerializeField]
    private MonoBehaviour inputSourceBehaviour;

    [SerializeField]
    private KesPresentation presentation;

    [SerializeField]
    private GameObject menuRoot;

    [SerializeField, Min(0.01f)]
    private float skipIntervalSeconds = 0.05f;

    [SerializeField, Min(0.01f)]
    private float autoIntervalSeconds = 2f;

    private IKesInputTarget target;
    private IKesInputSource inputSource;
    private RuntimeContinuation activeSelection;
    private float skipElapsed;
    private float autoElapsed;
    private string skipMode = "off";

    public bool IsMenuOpen { get; private set; }

    public bool IsAutoEnabled { get; private set; }

    public int SelectedChoiceIndex { get; private set; } = -1;

    public KesInputContext Context => IsMenuOpen
        ? KesInputContext.Menu
        : CurrentContinuation.Kind == RuntimeContinuationKind.WaitingForSelection
            ? KesInputContext.Selection
            : KesInputContext.Gameplay;

    public void SetReferences(
        KesManager newManager,
        MonoBehaviour newInputSourceBehaviour,
        KesPresentation newPresentation,
        GameObject newMenuRoot)
    {
        manager = newManager;
        target = newManager;
        inputSourceBehaviour = newInputSourceBehaviour;
        inputSource = newInputSourceBehaviour as IKesInputSource;
        presentation = newPresentation;
        menuRoot = newMenuRoot;
        ApplyMenuVisibility();
        SynchronizeState();
    }

    public void SetTarget(IKesInputTarget value)
    {
        target = value;
        SynchronizeState();
    }

    public void SetInputSource(IKesInputSource value)
    {
        inputSource = value;
        inputSourceBehaviour = value as MonoBehaviour;
        SynchronizeState();
    }

    public void SetIntervals(float skipSeconds, float autoSeconds)
    {
        skipIntervalSeconds = Mathf.Max(0.01f, skipSeconds);
        autoIntervalSeconds = Mathf.Max(0.01f, autoSeconds);
    }

    public void SetAutoEnabled(bool enabled)
    {
        IsAutoEnabled = enabled;
        autoElapsed = 0f;
    }

    public void SetAutoInterval(float seconds)
    {
        autoIntervalSeconds = Mathf.Max(0.01f, seconds);
        autoElapsed = 0f;
    }

    public void SetSkipMode(string mode)
    {
        skipMode = mode is "read" or "all" ? mode : "off";
        skipElapsed = 0f;
    }

    public void ProcessInput(KesInputFrame frame, float unscaledDeltaTime)
    {
        SynchronizeState();

        if (IsMenuOpen)
        {
            if (frame.CancelPressed)
            {
                SetMenuOpen(false);
            }

            return;
        }

        if (frame.CancelPressed)
        {
            SetMenuOpen(true);
            return;
        }

        var continuation = CurrentContinuation;
        if (continuation.Kind == RuntimeContinuationKind.WaitingForSelection)
        {
            ProcessSelection(frame, continuation);
            return;
        }

        if (frame.ToggleAutoPressed)
        {
            skipElapsed = 0f;
            IsAutoEnabled = !IsAutoEnabled;
            autoElapsed = 0f;
            return;
        }

        if (continuation.Kind != RuntimeContinuationKind.WaitingForAdvance)
        {
            skipElapsed = 0f;
            autoElapsed = 0f;
            return;
        }

        if (frame.AdvancePressed)
        {
            skipElapsed = 0f;
            autoElapsed = 0f;
            target?.ContinueAdvance();
            return;
        }

        if (frame.SkipHeld || skipMode == "all")
        {
            skipElapsed += Mathf.Max(0f, unscaledDeltaTime);
            if (skipElapsed >= skipIntervalSeconds)
            {
                skipElapsed = 0f;
                autoElapsed = 0f;
                target?.ContinueAdvance();
            }

            return;
        }

        if (IsAutoEnabled)
        {
            skipElapsed = 0f;
            autoElapsed += Mathf.Max(0f, unscaledDeltaTime);
            if (autoElapsed >= autoIntervalSeconds)
            {
                autoElapsed = 0f;
                target?.ContinueAdvance();
            }
        }
        else
        {
            skipElapsed = 0f;
            autoElapsed = 0f;
        }
    }

    private RuntimeContinuation CurrentContinuation => target?.Continuation ?? RuntimeContinuation.Completed;

    private void Awake()
    {
        ResolveReferences();
        ApplyMenuVisibility();
        SynchronizeState();
    }

    private void Update()
    {
        ResolveReferences();
        if (inputSource != null)
        {
            inputSource.SetContext(Context);
            ProcessInput(inputSource.ReadFrame(), Time.unscaledDeltaTime);
        }
    }

    private void ResolveReferences()
    {
        if (target == null)
        {
            target = manager;
        }

        if (inputSource == null)
        {
            inputSource = inputSourceBehaviour as IKesInputSource;
        }
    }

    private void SynchronizeState()
    {
        var continuation = CurrentContinuation;
        if (continuation.Kind == RuntimeContinuationKind.WaitingForSelection)
        {
            if (!ReferenceEquals(activeSelection, continuation))
            {
                activeSelection = continuation;
                SelectedChoiceIndex = continuation.PendingChoices.Count > 0 ? 0 : -1;
                presentation?.SetSelectedChoiceIndex(SelectedChoiceIndex);
            }
        }
        else
        {
            activeSelection = null;
            SelectedChoiceIndex = -1;
        }

        inputSource?.SetContext(Context);
    }

    private void ProcessSelection(KesInputFrame frame, RuntimeContinuation continuation)
    {
        var choiceCount = continuation.PendingChoices.Count;
        if (choiceCount == 0)
        {
            return;
        }

        if (frame.NavigateUpPressed || frame.NavigateDownPressed)
        {
            var direction = frame.NavigateUpPressed ? -1 : 1;
            SelectedChoiceIndex = (SelectedChoiceIndex + direction + choiceCount) % choiceCount;
            presentation?.SetSelectedChoiceIndex(SelectedChoiceIndex);
            return;
        }

        if (frame.SubmitPressed && SelectedChoiceIndex >= 0 && SelectedChoiceIndex < choiceCount)
        {
            target?.ChooseSelection(SelectedChoiceIndex);
        }
    }

    private void SetMenuOpen(bool value)
    {
        IsMenuOpen = value;
        skipElapsed = 0f;
        autoElapsed = 0f;
        ApplyMenuVisibility();
        inputSource?.SetContext(Context);
    }

    private void ApplyMenuVisibility()
    {
        if (menuRoot != null)
        {
            menuRoot.SetActive(IsMenuOpen);
        }
    }
}
}
