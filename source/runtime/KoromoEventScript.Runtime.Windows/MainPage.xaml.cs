using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using KoromoEventScript.Runtime.Windows.Bootstrap;
using KoromoEventScript.Runtime.Windows.Input;
using KoromoEventScript.Runtime.Windows.Rendering;
using KoromoEventScript.Runtime.Windows.ViewModels;
using Windows.System;

namespace KoromoEventScript.Runtime.Windows;

public sealed partial class MainPage : Page
{
    private readonly Win2DSceneRenderer sceneRenderer = new();
    private readonly PageTapInputGate pageTapInputGate = new();
    private readonly Dictionary<string, CanvasBitmap> bitmapCache = new(StringComparer.Ordinal);
    private readonly HashSet<string> failedBitmapAssetIds = new(StringComparer.Ordinal);
    private RuntimeSessionController? sessionController;

    public MainPageViewModel ViewModel { get; } = new();

    public MainPage()
    {
        InitializeComponent();
        sceneRenderer.Apply(RuntimeSceneState.Empty);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (sessionController is null && e.Parameter is WindowsRuntimeOptions options)
        {
            sessionController = new RuntimeSessionController(options, ViewModel);
            sessionController.Initialize();
            SceneCanvas.Invalidate();
        }
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is UIElement element)
        {
            element.Focus(FocusState.Programmatic);
        }
    }

    private void SceneCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        if (sender.ActualWidth <= 0 || sender.ActualHeight <= 0)
        {
            return;
        }

        sceneRenderer.Apply(ViewModel.SceneState);
        var viewport = SceneCoordinateMapper.CreateViewport(sender.ActualWidth, sender.ActualHeight);
        args.DrawingSession.Clear(Colors.Black);
        foreach (var item in sceneRenderer.BuildRenderPlan(viewport).Items)
        {
            var bounds = item.DisplayBounds;
            if (TryGetBitmap(sender, item.Renderable.AssetId, out var bitmap))
            {
                args.DrawingSession.DrawImage(
                    bitmap,
                    new global::Windows.Foundation.Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height));
            }
            else
            {
                args.DrawingSession.FillRectangle(
                    (float)bounds.X,
                    (float)bounds.Y,
                    (float)bounds.Width,
                    (float)bounds.Height,
                    LayerColor(item.Renderable.Layer));

                if (!string.IsNullOrWhiteSpace(item.Renderable.AssetId))
                {
                    DrawMissingAssetLabel(args.DrawingSession, bounds, ResolveMissingAssetLabel(item.Renderable.AssetId));
                }
            }
        }
    }

    private void Page_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if (!pageTapInputGate.ShouldAdvance(ViewModel.AreChoicesVisible))
        {
            e.Handled = true;
            return;
        }

        sessionController?.Advance();
        SceneCanvas.Invalidate();
        e.Handled = true;
    }

    private void Page_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is VirtualKey.Enter or VirtualKey.Space)
        {
            sessionController?.Advance();
            SceneCanvas.Invalidate();
            e.Handled = true;
        }
    }

    private void ChoiceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListView listView || listView.SelectedIndex < 0)
        {
            return;
        }

        var selectedIndex = listView.SelectedIndex;
        listView.SelectedIndex = -1;
        pageTapInputGate.SuppressNextPageTap();
        sessionController?.ChooseSelection(selectedIndex);
        SceneCanvas.Invalidate();
    }

    private bool TryGetBitmap(CanvasControl canvas, string? assetId, out CanvasBitmap bitmap)
    {
        bitmap = null!;
        if (string.IsNullOrWhiteSpace(assetId) ||
            failedBitmapAssetIds.Contains(assetId) ||
            !ViewModel.TryGetAssetPath(assetId, out var path))
        {
            return false;
        }

        if (bitmapCache.TryGetValue(assetId, out var cached))
        {
            bitmap = cached;
            return true;
        }

        try
        {
            bitmap = CanvasBitmap.LoadAsync(canvas, path).AsTask().GetAwaiter().GetResult();
            bitmapCache[assetId] = bitmap;
            return true;
        }
        catch
        {
            failedBitmapAssetIds.Add(assetId);
            return false;
        }
    }

    private string ResolveMissingAssetLabel(string assetId)
    {
        return ViewModel.TryGetAssetPath(assetId, out var path)
            ? Path.GetFileName(path)
            : assetId;
    }

    private static void DrawMissingAssetLabel(CanvasDrawingSession drawingSession, SceneRect bounds, string label)
    {
        var labelBounds = new global::Windows.Foundation.Rect(
            bounds.X + 16,
            bounds.Y + Math.Max(0, (bounds.Height / 2) - 48),
            Math.Max(0, bounds.Width - 32),
            96);

        using var textFormat = new CanvasTextFormat
        {
            FontSize = 18,
            HorizontalAlignment = CanvasHorizontalAlignment.Center,
            VerticalAlignment = CanvasVerticalAlignment.Center,
            WordWrapping = CanvasWordWrapping.Wrap,
        };

        drawingSession.FillRectangle(labelBounds, global::Windows.UI.Color.FromArgb(176, 0, 0, 0));
        drawingSession.DrawText(label, labelBounds, Colors.White, textFormat);
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
