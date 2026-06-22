namespace KoromoEventScript.Runtime.Windows.Bootstrap;

public static class WindowsRuntimeBootstrapper
{
    public static WindowsRuntimeBootstrapResult Bootstrap(string launchArguments, string baseDirectory)
    {
        return WindowsRuntimeArgumentParser.Parse(launchArguments, baseDirectory);
    }
}
