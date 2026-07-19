namespace KoromoEventScript.Unity
{

public enum KesInputContext
{
    Gameplay = 0,
    Selection = 1,
    Menu = 2,
}

public readonly struct KesInputFrame
{
    public KesInputFrame(
        bool advancePressed = false,
        bool cancelPressed = false,
        bool submitPressed = false,
        bool navigateUpPressed = false,
        bool navigateDownPressed = false,
        bool toggleAutoPressed = false,
        bool skipHeld = false)
    {
        AdvancePressed = advancePressed;
        CancelPressed = cancelPressed;
        SubmitPressed = submitPressed;
        NavigateUpPressed = navigateUpPressed;
        NavigateDownPressed = navigateDownPressed;
        ToggleAutoPressed = toggleAutoPressed;
        SkipHeld = skipHeld;
    }

    public bool AdvancePressed { get; }

    public bool CancelPressed { get; }

    public bool SubmitPressed { get; }

    public bool NavigateUpPressed { get; }

    public bool NavigateDownPressed { get; }

    public bool ToggleAutoPressed { get; }

    public bool SkipHeld { get; }
}

public interface IKesInputSource
{
    KesInputFrame ReadFrame();

    void SetContext(KesInputContext context);
}
}
