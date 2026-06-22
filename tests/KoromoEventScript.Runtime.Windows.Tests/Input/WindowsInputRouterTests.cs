using KoromoEventScript.Runtime.Windows.Input;
using KoromoEventScript.Runtime.Windows.Rendering;
using Windows.System;

namespace KoromoEventScript.Runtime.Windows.Tests.Input;

public sealed class WindowsInputRouterTests
{
    [TestCase(VirtualKey.Enter)]
    [TestCase(VirtualKey.Space)]
    public void RouteKeyDown_WithAdvanceKeys_AdvancesText(VirtualKey key)
    {
        var router = new WindowsInputRouter();

        var input = router.RouteKeyDown(key);

        Assert.That(input?.Action, Is.EqualTo(RuntimeInputAction.AdvanceText));
    }

    [Test]
    public void RoutePointerPressed_WithLeftButton_AdvancesTextWithProductionPoint()
    {
        var router = new WindowsInputRouter();
        var viewport = SceneCoordinateMapper.CreateViewport(2560, 1080);

        var input = router.RoutePointerPressed(WindowsPointerButton.Left, new ScenePoint(420, 50), viewport);

        Assert.Multiple(() =>
        {
            Assert.That(input?.Action, Is.EqualTo(RuntimeInputAction.AdvanceText));
            Assert.That(input?.ProductionPoint?.X, Is.EqualTo(100d));
            Assert.That(input?.ProductionPoint?.Y, Is.EqualTo(50d));
        });
    }

    [TestCase(VirtualKey.Enter)]
    [TestCase(VirtualKey.Space)]
    public void RouteKeyDown_WithChoices_ConfirmsChoice(VirtualKey key)
    {
        var router = new WindowsInputRouter(new RuntimeInputState(HasChoices: true));

        var input = router.RouteKeyDown(key);

        Assert.That(input?.Action, Is.EqualTo(RuntimeInputAction.ConfirmChoice));
    }

    [Test]
    public void RoutePointerPressed_WithRightButton_TogglesSystemMenu()
    {
        var router = new WindowsInputRouter();

        var open = router.RoutePointerPressed(WindowsPointerButton.Right);
        var close = router.RoutePointerPressed(WindowsPointerButton.Right);

        Assert.Multiple(() =>
        {
            Assert.That(open?.Action, Is.EqualTo(RuntimeInputAction.OpenSystemMenu));
            Assert.That(open?.State.SystemMenuOpen, Is.True);
            Assert.That(close?.Action, Is.EqualTo(RuntimeInputAction.CloseSystemMenu));
            Assert.That(close?.State.SystemMenuOpen, Is.False);
        });
    }

    [Test]
    public void RouteKeyDown_WithEsc_TogglesSystemMenu()
    {
        var router = new WindowsInputRouter();

        var open = router.RouteKeyDown(VirtualKey.Escape);
        var close = router.RouteKeyDown(VirtualKey.Escape);

        Assert.Multiple(() =>
        {
            Assert.That(open?.Action, Is.EqualTo(RuntimeInputAction.OpenSystemMenu));
            Assert.That(close?.Action, Is.EqualTo(RuntimeInputAction.CloseSystemMenu));
        });
    }

    [Test]
    public void RouteKeyDown_WithControl_TogglesSkip()
    {
        var router = new WindowsInputRouter();

        var start = router.RouteKeyDown(VirtualKey.Control);
        var stop = router.RouteKeyDown(VirtualKey.Control);

        Assert.Multiple(() =>
        {
            Assert.That(start?.Action, Is.EqualTo(RuntimeInputAction.StartSkip));
            Assert.That(start?.State.SkipActive, Is.True);
            Assert.That(stop?.Action, Is.EqualTo(RuntimeInputAction.StopSkip));
            Assert.That(stop?.State.SkipActive, Is.False);
        });
    }

    [Test]
    public void RouteKeyDown_WithTab_TogglesAutoMode()
    {
        var router = new WindowsInputRouter();

        var start = router.RouteKeyDown(VirtualKey.Tab);
        var stop = router.RouteKeyDown(VirtualKey.Tab);

        Assert.Multiple(() =>
        {
            Assert.That(start?.Action, Is.EqualTo(RuntimeInputAction.StartAuto));
            Assert.That(start?.State.AutoMode, Is.True);
            Assert.That(stop?.Action, Is.EqualTo(RuntimeInputAction.StopAuto));
            Assert.That(stop?.State.AutoMode, Is.False);
        });
    }

    [Test]
    public void RouteMouseWheel_WithWheelUp_ShowsBacklog()
    {
        var router = new WindowsInputRouter();

        var input = router.RouteMouseWheel(120);

        Assert.Multiple(() =>
        {
            Assert.That(input?.Action, Is.EqualTo(RuntimeInputAction.ShowBacklog));
            Assert.That(input?.State.BacklogVisible, Is.True);
        });
    }

    [TestCase(VirtualKey.Up, RuntimeInputAction.MoveChoiceUp, -1)]
    [TestCase(VirtualKey.Down, RuntimeInputAction.MoveChoiceDown, 1)]
    public void RouteKeyDown_WithChoiceNavigation_MapsDirection(VirtualKey key, RuntimeInputAction action, int delta)
    {
        var router = new WindowsInputRouter(new RuntimeInputState(HasChoices: true));

        var input = router.RouteKeyDown(key);

        Assert.Multiple(() =>
        {
            Assert.That(input?.Action, Is.EqualTo(action));
            Assert.That(input?.ChoiceDelta, Is.EqualTo(delta));
        });
    }

    [Test]
    public void RouteKeyDown_WithF11_TogglesFullscreen()
    {
        var router = new WindowsInputRouter();

        var enter = router.RouteKeyDown(VirtualKey.F11);
        var leave = router.RouteKeyDown(VirtualKey.F11);

        Assert.Multiple(() =>
        {
            Assert.That(enter?.Action, Is.EqualTo(RuntimeInputAction.EnterFullscreen));
            Assert.That(enter?.State.Fullscreen, Is.True);
            Assert.That(leave?.Action, Is.EqualTo(RuntimeInputAction.LeaveFullscreen));
            Assert.That(leave?.State.Fullscreen, Is.False);
        });
    }

    [Test]
    public void RouteKeyDown_WithUnhandledKey_ReturnsNull()
    {
        var router = new WindowsInputRouter();

        var input = router.RouteKeyDown(VirtualKey.A);

        Assert.That(input, Is.Null);
    }
}
