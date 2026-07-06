using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using KoromoEventScript.Runtime.Core.Manifests;
using KoromoEventScript.Runtime.Windows.Rendering;

namespace KoromoEventScript.Runtime.Windows.ViewModels;

/// <summary>
/// Holds the standard runtime UI shell state until VM-driven host state is wired in.
/// </summary>
public partial class MainPageViewModel : ObservableObject
{
    [ObservableProperty]
    public partial bool IsMessageVisible { get; set; }

    [ObservableProperty]
    public partial bool AreChoicesVisible { get; set; }

    [ObservableProperty]
    public partial bool IsBacklogVisible { get; set; }

    [ObservableProperty]
    public partial string SpeakerName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MessageText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial RuntimeSceneState SceneState { get; set; } = RuntimeSceneState.Empty;

    public IReadOnlyDictionary<string, string> AssetPaths { get; private set; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public ObservableCollection<string> Choices { get; } = [];

    public ObservableCollection<string> BacklogEntries { get; } = [];

    public void ShowDialogue(string speakerName, string message)
    {
        SpeakerName = speakerName;
        MessageText = message;
        IsMessageVisible = true;
        BacklogEntries.Add(string.IsNullOrWhiteSpace(speakerName) ? message : $"{speakerName}: {message}");
    }

    public void ShowChoices(IEnumerable<string> choices)
    {
        Choices.Clear();
        foreach (var choice in choices)
        {
            Choices.Add(choice);
        }

        AreChoicesVisible = Choices.Count > 0;
    }

    public void HideChoices()
    {
        Choices.Clear();
        AreChoicesVisible = false;
    }

    public void SetAssets(IEnumerable<RuntimeAssetEntry> assets)
    {
        AssetPaths = assets
            .GroupBy(static asset => asset.AssetId, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.First().ResolvedPath, StringComparer.Ordinal);
    }

    public bool TryGetAssetPath(string assetId, out string path)
    {
        return AssetPaths.TryGetValue(assetId, out path!);
    }

    public void ApplySceneState(RuntimeSceneState sceneState)
    {
        SceneState = sceneState;
    }

    public void ShowRuntimeError(string message)
    {
        HideChoices();
        SpeakerName = "Runtime";
        MessageText = message;
        IsMessageVisible = true;
    }
}
