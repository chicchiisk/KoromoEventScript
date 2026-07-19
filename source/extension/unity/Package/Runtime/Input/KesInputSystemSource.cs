using UnityEngine;
using UnityEngine.InputSystem;

namespace KoromoEventScript.Unity
{

[DisallowMultipleComponent]
[AddComponentMenu("KoromoEventScript/KES Input System Source")]
public sealed class KesInputSystemSource : MonoBehaviour, IKesInputSource
{
    private InputActionAsset actions;
    private InputActionMap gameplayMap;
    private InputActionMap uiMap;
    private InputAction advance;
    private InputAction openMenu;
    private InputAction skip;
    private InputAction toggleAuto;
    private InputAction submit;
    private InputAction cancel;
    private InputAction navigateUp;
    private InputAction navigateDown;
    private KesInputContext context;

    public KesInputContext Context => context;

    public KesInputFrame ReadFrame()
    {
        EnsureActions();
        return new KesInputFrame(
            advancePressed: advance.WasPressedThisFrame(),
            cancelPressed: (context == KesInputContext.Gameplay ? openMenu : cancel).WasPressedThisFrame(),
            submitPressed: submit.WasPressedThisFrame(),
            navigateUpPressed: navigateUp.WasPressedThisFrame(),
            navigateDownPressed: navigateDown.WasPressedThisFrame(),
            toggleAutoPressed: toggleAuto.WasPressedThisFrame(),
            skipHeld: skip.IsPressed());
    }

    public void SetContext(KesInputContext value)
    {
        EnsureActions();
        context = value;
        if (!isActiveAndEnabled)
        {
            return;
        }

        ApplyContext();
    }

    private void Awake()
    {
        EnsureActions();
    }

    private void OnEnable()
    {
        EnsureActions();
        ApplyContext();
    }

    private void OnDisable()
    {
        gameplayMap?.Disable();
        uiMap?.Disable();
    }

    private void OnDestroy()
    {
        if (actions != null)
        {
            if (Application.isPlaying)
            {
                Destroy(actions);
            }
            else
            {
                DestroyImmediate(actions);
            }
        }
    }

    private void EnsureActions()
    {
        if (actions != null)
        {
            return;
        }

        actions = ScriptableObject.CreateInstance<InputActionAsset>();
        actions.name = "KES Runtime Input";

        gameplayMap = actions.AddActionMap("Gameplay");
        advance = gameplayMap.AddAction("Advance", InputActionType.Button);
        advance.AddBinding("<Mouse>/leftButton");
        advance.AddBinding("<Keyboard>/enter");
        advance.AddBinding("<Keyboard>/space");
        openMenu = gameplayMap.AddAction("OpenMenu", InputActionType.Button);
        openMenu.AddBinding("<Mouse>/rightButton");
        openMenu.AddBinding("<Keyboard>/escape");
        skip = gameplayMap.AddAction("Skip", InputActionType.Button);
        skip.AddBinding("<Keyboard>/leftCtrl");
        skip.AddBinding("<Keyboard>/rightCtrl");
        toggleAuto = gameplayMap.AddAction("ToggleAuto", InputActionType.Button);
        toggleAuto.AddBinding("<Keyboard>/tab");

        uiMap = actions.AddActionMap("UI");
        submit = uiMap.AddAction("Submit", InputActionType.Button);
        submit.AddBinding("<Mouse>/leftButton");
        submit.AddBinding("<Keyboard>/enter");
        submit.AddBinding("<Keyboard>/space");
        cancel = uiMap.AddAction("Cancel", InputActionType.Button);
        cancel.AddBinding("<Mouse>/rightButton");
        cancel.AddBinding("<Keyboard>/escape");
        navigateUp = uiMap.AddAction("NavigateUp", InputActionType.Button);
        navigateUp.AddBinding("<Keyboard>/upArrow");
        navigateDown = uiMap.AddAction("NavigateDown", InputActionType.Button);
        navigateDown.AddBinding("<Keyboard>/downArrow");
    }

    private void ApplyContext()
    {
        if (context == KesInputContext.Gameplay)
        {
            uiMap.Disable();
            gameplayMap.Enable();
            return;
        }

        gameplayMap.Disable();
        uiMap.Enable();
    }
}
}
