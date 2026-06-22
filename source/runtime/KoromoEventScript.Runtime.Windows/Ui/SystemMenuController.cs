namespace KoromoEventScript.Runtime.Windows.Ui;

public enum StandardSystemMenuCommand
{
    Save,
    Load,
    Settings,
    Title,
    Exit,
}

public enum RuntimeMenuAction
{
    Save,
    Load,
    OpenSettings,
    ReturnToTitle,
    Exit,
}

public sealed record RuntimeMenuCommandResult(RuntimeMenuAction Action);

public sealed class SystemMenuController
{
    public RuntimeMenuCommandResult Execute(StandardSystemMenuCommand command)
    {
        var action = command switch
        {
            StandardSystemMenuCommand.Save => RuntimeMenuAction.Save,
            StandardSystemMenuCommand.Load => RuntimeMenuAction.Load,
            StandardSystemMenuCommand.Settings => RuntimeMenuAction.OpenSettings,
            StandardSystemMenuCommand.Title => RuntimeMenuAction.ReturnToTitle,
            StandardSystemMenuCommand.Exit => RuntimeMenuAction.Exit,
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, null),
        };

        return new RuntimeMenuCommandResult(action);
    }
}
