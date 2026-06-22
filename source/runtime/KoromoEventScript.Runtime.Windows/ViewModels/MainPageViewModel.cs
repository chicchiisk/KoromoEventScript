using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace KoromoEventScript.Runtime.Windows.ViewModels;

/// <summary>
/// Holds the standard runtime UI shell state until VM-driven host state is wired in.
/// </summary>
public partial class MainPageViewModel : ObservableObject
{
    [ObservableProperty]
    public partial bool IsMessageVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool AreChoicesVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool IsBacklogVisible { get; set; }

    [ObservableProperty]
    public partial string SpeakerName { get; set; } = "Narrator";

    [ObservableProperty]
    public partial string MessageText { get; set; } = "Runtime message window";

    public ObservableCollection<string> Choices { get; } = ["Continue", "Open menu"];

    public ObservableCollection<string> BacklogEntries { get; } = ["Runtime message window"];
}
