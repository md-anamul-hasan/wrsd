using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Wrsd.UI.Controls;

/// <summary>Custom title bar, the same behaviour and caption buttons as BatteryLens and MetaForge.</summary>
public partial class TitleBar : UserControl
{
    public TitleBar()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            var win = Window.GetWindow(this);
            if (win == null) return;
            win.StateChanged += (_, _) => UpdateMaximizeIcon(win.WindowState);
            UpdateMaximizeIcon(win.WindowState);
        };
    }

    private void UpdateMaximizeIcon(WindowState state)
    {
        if (state == WindowState.Maximized)
        {
            MaximizePath.Data = Geometry.Parse("M 2.5,0.5 H 9.5 V 7.5 H 7.5 V 9.5 H 0.5 V 2.5 H 2.5 Z M 2.5,2.5 H 7.5 V 7.5");
            MaximizeButton.ToolTip = "Restore Down";
        }
        else
        {
            MaximizePath.Data = Geometry.Parse("M 0.5,0.5 H 9.5 V 9.5 H 0.5 Z");
            MaximizeButton.ToolTip = "Maximize";
        }
    }

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) ToggleMaximize();
        else if (e.LeftButton == MouseButtonState.Pressed) Window.GetWindow(this)?.DragMove();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is { } w) w.WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Window.GetWindow(this)?.Close();

    private void ToggleMaximize()
    {
        if (Window.GetWindow(this) is { } w)
            w.WindowState = w.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }
}
