using KoromoEventScript.Runtime.Windows.Rendering;
using Windows.System;

namespace KoromoEventScript.Runtime.Windows.Input;

public enum WindowsPointerButton
{
    Left,
    Right,
}

public enum RuntimeInputSource
{
    Keyboard,
    Mouse,
}

public enum RuntimeInputAction
{
    AdvanceText,
    ConfirmChoice,
    OpenSystemMenu,
    CloseSystemMenu,
    StartSkip,
    StopSkip,
    StartAuto,
    StopAuto,
    ShowBacklog,
    MoveChoiceUp,
    MoveChoiceDown,
    EnterFullscreen,
    LeaveFullscreen,
}

public sealed record RuntimeInputState(
    bool HasChoices = false,
    bool SystemMenuOpen = false,
    bool SkipActive = false,
    bool AutoMode = false,
    bool BacklogVisible = false,
    bool Fullscreen = false);

public sealed record RuntimeInputEvent(
    RuntimeInputAction Action,
    RuntimeInputSource Source,
    RuntimeInputState State,
    ScenePoint? ProductionPoint = null,
    int ChoiceDelta = 0);

public sealed class WindowsInputRouter
{
    private RuntimeInputState state;

    public WindowsInputRouter()
        : this(new RuntimeInputState())
    {
    }

    public WindowsInputRouter(RuntimeInputState initialState)
    {
        state = initialState;
    }

    public RuntimeInputState State => state;

    public void UpdateState(RuntimeInputState newState)
    {
        state = newState;
    }

    public RuntimeInputEvent? RouteKeyDown(VirtualKey key)
    {
        return key switch
        {
            VirtualKey.Enter or VirtualKey.Space => AdvanceOrConfirm(RuntimeInputSource.Keyboard, null),
            VirtualKey.Escape => ToggleSystemMenu(RuntimeInputSource.Keyboard),
            VirtualKey.Control => ToggleSkip(),
            VirtualKey.Tab => ToggleAuto(),
            VirtualKey.Up when state.HasChoices => new RuntimeInputEvent(RuntimeInputAction.MoveChoiceUp, RuntimeInputSource.Keyboard, state, ChoiceDelta: -1),
            VirtualKey.Down when state.HasChoices => new RuntimeInputEvent(RuntimeInputAction.MoveChoiceDown, RuntimeInputSource.Keyboard, state, ChoiceDelta: 1),
            VirtualKey.F11 => ToggleFullscreen(),
            _ => null,
        };
    }

    public RuntimeInputEvent? RoutePointerPressed(
        WindowsPointerButton button,
        ScenePoint? displayPoint = null,
        SceneViewport? viewport = null)
    {
        var productionPoint = displayPoint is { } point && viewport is { } sceneViewport
            ? SceneCoordinateMapper.TryToProductionPoint(sceneViewport, point)
            : null;

        return button switch
        {
            WindowsPointerButton.Left => AdvanceOrConfirm(RuntimeInputSource.Mouse, productionPoint),
            WindowsPointerButton.Right => ToggleSystemMenu(RuntimeInputSource.Mouse),
            _ => null,
        };
    }

    public RuntimeInputEvent? RouteMouseWheel(int wheelDelta)
    {
        if (wheelDelta <= 0)
        {
            return null;
        }

        state = state with { BacklogVisible = true };
        return new RuntimeInputEvent(RuntimeInputAction.ShowBacklog, RuntimeInputSource.Mouse, state);
    }

    private RuntimeInputEvent AdvanceOrConfirm(RuntimeInputSource source, ScenePoint? productionPoint)
    {
        var action = state.HasChoices ? RuntimeInputAction.ConfirmChoice : RuntimeInputAction.AdvanceText;
        return new RuntimeInputEvent(action, source, state, productionPoint);
    }

    private RuntimeInputEvent ToggleSystemMenu(RuntimeInputSource source)
    {
        state = state with { SystemMenuOpen = !state.SystemMenuOpen };
        var action = state.SystemMenuOpen ? RuntimeInputAction.OpenSystemMenu : RuntimeInputAction.CloseSystemMenu;
        return new RuntimeInputEvent(action, source, state);
    }

    private RuntimeInputEvent ToggleSkip()
    {
        state = state with { SkipActive = !state.SkipActive };
        var action = state.SkipActive ? RuntimeInputAction.StartSkip : RuntimeInputAction.StopSkip;
        return new RuntimeInputEvent(action, RuntimeInputSource.Keyboard, state);
    }

    private RuntimeInputEvent ToggleAuto()
    {
        state = state with { AutoMode = !state.AutoMode };
        var action = state.AutoMode ? RuntimeInputAction.StartAuto : RuntimeInputAction.StopAuto;
        return new RuntimeInputEvent(action, RuntimeInputSource.Keyboard, state);
    }

    private RuntimeInputEvent ToggleFullscreen()
    {
        state = state with { Fullscreen = !state.Fullscreen };
        var action = state.Fullscreen ? RuntimeInputAction.EnterFullscreen : RuntimeInputAction.LeaveFullscreen;
        return new RuntimeInputEvent(action, RuntimeInputSource.Keyboard, state);
    }
}
