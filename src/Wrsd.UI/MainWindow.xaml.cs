using System.Windows;
using Wrsd.UI.Services;

namespace Wrsd.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Mdah.Windowing.MaximizeFix.Attach(this);
        Mdah.Windowing.ResponsiveScale.Attach(this, Body);
        SourceInitialized += MainWindow_SourceInitialized;
        ThemeManager.ThemeChanged += (mode, isDark) =>
        {
            ThemeManager.UpdateWindowTitleBar(this, isDark);
        };
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        ThemeManager.UpdateWindowTitleBar(this, ThemeManager.IsDarkThemeActive);
    }
}