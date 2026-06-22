namespace KoromoEventScript.Runtime.Windows.Tests.StandardUi;

public sealed class MainPageXamlTests
{
    [Test]
    public void MainPage_DefinesStandardRuntimeUiAutomationIds()
    {
        var xaml = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "source/runtime/KoromoEventScript.Runtime.Windows/MainPage.xaml"));

        Assert.Multiple(() =>
        {
            Assert.That(xaml, Does.Contain("AutomationProperties.AutomationId=\"RuntimeMessageWindow\""));
            Assert.That(xaml, Does.Contain("AutomationProperties.AutomationId=\"RuntimeMessageText\""));
            Assert.That(xaml, Does.Contain("AutomationProperties.AutomationId=\"RuntimeChoiceList\""));
            Assert.That(xaml, Does.Contain("AutomationProperties.AutomationId=\"RuntimeBacklogPanel\""));
            Assert.That(xaml, Does.Contain("AutomationProperties.AutomationId=\"RuntimeBacklogList\""));
            Assert.That(xaml, Does.Contain("AutomationProperties.AutomationId=\"RuntimeSystemMenuButton\""));
            Assert.That(xaml, Does.Contain("AutomationProperties.AutomationId=\"RuntimeMenuSave\""));
            Assert.That(xaml, Does.Contain("AutomationProperties.AutomationId=\"RuntimeMenuLoad\""));
            Assert.That(xaml, Does.Contain("AutomationProperties.AutomationId=\"RuntimeMenuSettings\""));
            Assert.That(xaml, Does.Contain("AutomationProperties.AutomationId=\"RuntimeMenuTitle\""));
            Assert.That(xaml, Does.Contain("AutomationProperties.AutomationId=\"RuntimeMenuExit\""));
        });
    }

    [Test]
    public void MainPage_UsesStandardWinUiControlsForRuntimeUiShell()
    {
        var xaml = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "source/runtime/KoromoEventScript.Runtime.Windows/MainPage.xaml"));

        Assert.Multiple(() =>
        {
            Assert.That(xaml, Does.Contain("<ListView"));
            Assert.That(xaml, Does.Contain("<AppBarButton"));
            Assert.That(xaml, Does.Contain("<MenuFlyout"));
        });
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "KoromoEventScript.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test directory.");
    }
}
