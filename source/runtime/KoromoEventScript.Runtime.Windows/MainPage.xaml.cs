using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using KoromoEventScript.Runtime.Windows.ViewModels;
using KoromoEventScript.Runtime.Windows.Rendering;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace KoromoEventScript.Runtime.Windows;

/// <summary>
/// The main content page displayed inside the application window.
/// </summary>
public sealed partial class MainPage : Page
{
    private readonly Win2DSceneRenderer sceneRenderer = new();

    public MainPageViewModel ViewModel { get; } = new();

    public MainPage()
    {
        InitializeComponent();
        sceneRenderer.Apply(
            new RuntimeSceneState(
                [
                    new SceneRenderable("background", SceneLayer.Background, new SceneRect(0, 0, 1920, 1080)),
                    new SceneRenderable("actor-preview", SceneLayer.Actor, new SceneRect(620, 120, 680, 960)),
                    new SceneRenderable("message-preview", SceneLayer.Text, new SceneRect(160, 760, 1600, 240)),
                ]));
    }

    private void SceneCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        if (sender.ActualWidth <= 0 || sender.ActualHeight <= 0)
        {
            return;
        }

        var viewport = SceneCoordinateMapper.CreateViewport(sender.ActualWidth, sender.ActualHeight);
        args.DrawingSession.Clear(Colors.Black);
        foreach (var item in sceneRenderer.BuildRenderPlan(viewport).Items)
        {
            var bounds = item.DisplayBounds;
            args.DrawingSession.FillRectangle(
                (float)bounds.X,
                (float)bounds.Y,
                (float)bounds.Width,
                (float)bounds.Height,
                LayerColor(item.Renderable.Layer));
        }
    }

    private static global::Windows.UI.Color LayerColor(SceneLayer layer)
    {
        return layer switch
        {
            SceneLayer.Background => Colors.MidnightBlue,
            SceneLayer.Actor => Colors.DarkSlateGray,
            SceneLayer.Effects => Colors.Transparent,
            SceneLayer.Text => Colors.Black,
            SceneLayer.Choices => Colors.DimGray,
            SceneLayer.SystemUi => Colors.DarkGray,
            _ => Colors.Black,
        };
    }

    public static Visibility BoolToVisibility(bool value)
    {
        return value ? Visibility.Visible : Visibility.Collapsed;
    }
}
