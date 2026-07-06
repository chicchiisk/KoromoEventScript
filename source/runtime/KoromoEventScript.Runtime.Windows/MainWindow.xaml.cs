using Microsoft.UI.Xaml;
using Windows.Graphics;
using KoromoEventScript.Runtime.Windows.Bootstrap;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace KoromoEventScript.Runtime.Windows;

/// <summary>
/// The application window. This hosts a Frame that displays pages. Add your
/// UI and logic to MainPage.xaml / MainPage.xaml.cs instead of here so you
/// can use Page features such as navigation events and the Loaded lifecycle.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow(WindowsRuntimeOptions? options = null)
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        SetWindowIcon();
        ApplyWindowOptions(options);

        // Navigate the root frame to the main page on startup.
        RootFrame.Navigate(typeof(MainPage), options);
    }

    private void SetWindowIcon()
    {
        foreach (var candidate in new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"),
            Path.Combine(AppContext.BaseDirectory, "AppX", "Assets", "AppIcon.ico"),
        })
        {
            if (File.Exists(candidate))
            {
                AppWindow.SetIcon(candidate);
                break;
            }
        }
    }

    private void ApplyWindowOptions(WindowsRuntimeOptions? options)
    {
        if (options is null)
        {
            return;
        }

        if (options.Width is not null && options.Height is not null)
        {
            AppWindow.Resize(new SizeInt32(options.Width.Value, options.Height.Value));
        }

        if (options.Fullscreen)
        {
            AppWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen);
        }
    }
}
