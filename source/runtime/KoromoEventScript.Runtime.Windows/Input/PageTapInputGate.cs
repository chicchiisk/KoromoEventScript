namespace KoromoEventScript.Runtime.Windows.Input;

public sealed class PageTapInputGate
{
    private bool suppressNextPageTap;

    public void SuppressNextPageTap()
    {
        suppressNextPageTap = true;
    }

    public bool ShouldAdvance(bool choicesVisible)
    {
        if (suppressNextPageTap)
        {
            suppressNextPageTap = false;
            return false;
        }

        return !choicesVisible;
    }
}
