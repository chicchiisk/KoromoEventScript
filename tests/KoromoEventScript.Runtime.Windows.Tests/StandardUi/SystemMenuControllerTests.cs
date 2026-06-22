using KoromoEventScript.Runtime.Windows.Ui;

namespace KoromoEventScript.Runtime.Windows.Tests.StandardUi;

public sealed class SystemMenuControllerTests
{
    [TestCase(StandardSystemMenuCommand.Save, RuntimeMenuAction.Save)]
    [TestCase(StandardSystemMenuCommand.Load, RuntimeMenuAction.Load)]
    [TestCase(StandardSystemMenuCommand.Settings, RuntimeMenuAction.OpenSettings)]
    [TestCase(StandardSystemMenuCommand.Title, RuntimeMenuAction.ReturnToTitle)]
    [TestCase(StandardSystemMenuCommand.Exit, RuntimeMenuAction.Exit)]
    public void Execute_MapsStandardMenuCommandToRuntimeAction(StandardSystemMenuCommand command, RuntimeMenuAction action)
    {
        var controller = new SystemMenuController();

        var result = controller.Execute(command);

        Assert.That(result.Action, Is.EqualTo(action));
    }
}
