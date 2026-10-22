using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace Wrsd.UI.Services;

public enum AppThemeMode
{
    System = 0,
    Dark = 1,
    Light = 2
}

public static class ThemeManager
{
    private const string RegistryKeyPersonalize = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string RegistryValueAppsUseLightTheme = "AppsUseLightTheme";
    private const string WrsdSettingsKey = @"Software\WRSD";
    private const string ThemeSettingValue = "ThemeMode";

    private static AppThemeMode _currentMode = AppThemeMode.System;
    private static bool _listeningToSystemEvents;

    public static event Action<AppThemeMode, bool>? ThemeChanged;

    public static AppThemeMode CurrentMode => _currentMode;
    public static bool IsDarkThemeActive { get; private set; } = true;

    public static void Initialize()
    {
        _currentMode = LoadSavedThemeMode();
        EnsureSystemEventListener();
        ApplyTheme(_currentMode);
    }

    public static void SetTheme(AppThemeMode mode)
    {
        _currentMode = mode;
        SaveThemeMode(mode);
        ApplyTheme(mode);
    }

    public static AppThemeMode CycleNextTheme()
    {
        var next = _currentMode switch
        {
            AppThemeMode.System => AppThemeMode.Dark,
            AppThemeMode.Dark => AppThemeMode.Light,
            AppThemeMode.Light => AppThemeMode.System,
            _ => AppThemeMode.System
        };
        SetTheme(next);
        return next;
    }

    public static bool DetectSystemIsDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPersonalize);
            if (key != null)
            {
                var val = key.GetValue(RegistryValueAppsUseLightTheme);
                if (val is int intVal)
                {
                    // 0 = Dark, 1 = Light
                    return intVal == 0;
                }
            }
        }
        catch
        {
            // Fallback to dark if registry cannot be read
        }
        return true;
    }

    public static void ApplyTheme(AppThemeMode mode)
    {
        _currentMode = mode;
        bool isDark = mode switch
        {
            AppThemeMode.Dark => true,
            AppThemeMode.Light => false,
            AppThemeMode.System => DetectSystemIsDark(),
            _ => true
        };

        IsDarkThemeActive = isDark;

        var themeUri = new Uri(isDark ? "Styles/ThemeDark.xaml" : "Styles/ThemeLight.xaml", UriKind.Relative);
        var newThemeDict = new ResourceDictionary { Source = themeUri };

        var app = Application.Current;
        if (app != null)
        {
            var merged = app.Resources.MergedDictionaries;
            ResourceDictionary? existingTheme = null;
            foreach (var dict in merged)
            {
                if (dict.Source != null && (dict.Source.OriginalString.Contains("ThemeDark.xaml") || dict.Source.OriginalString.Contains("ThemeLight.xaml")))
                {
                    existingTheme = dict;
                    break;
                }
            }

            if (existingTheme != null)
            {
                var index = merged.IndexOf(existingTheme);
                merged[index] = newThemeDict;
            }
            else
            {
                merged.Add(newThemeDict);
            }
        }

        if (Application.Current?.MainWindow != null)
        {
            UpdateWindowTitleBar(Application.Current.MainWindow, isDark);
        }

        ThemeChanged?.Invoke(_currentMode, isDark);
    }

    public static void UpdateWindowTitleBar(Window window, bool isDark)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            int useDarkMode = isDark ? 1 : 0;
            // DWMWA_USE_IMMERSIVE_DARK_MODE = 20, BEFORE_20H1 = 19
            if (DwmSetWindowAttribute(hwnd, 20, ref useDarkMode, sizeof(int)) != 0)
            {
                DwmSetWindowAttribute(hwnd, 19, ref useDarkMode, sizeof(int));
            }

            // Windows 11 DWMWA_SYSTEMBACKDROP_TYPE = 38 (2 = Mica, 4 = Mica Alt)
            int backdropType = 2;
            DwmSetWindowAttribute(hwnd, 38, ref backdropType, sizeof(int));
        }
        catch
        {
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private static void EnsureSystemEventListener()
    {
        if (_listeningToSystemEvents) return;
        try
        {
            SystemEvents.UserPreferenceChanged += (s, e) =>
            {
                if (_currentMode == AppThemeMode.System)
                {
                    Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        ApplyTheme(AppThemeMode.System);
                    });
                }
            };
            _listeningToSystemEvents = true;
        }
        catch
        {
        }
    }

    private static AppThemeMode LoadSavedThemeMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(WrsdSettingsKey);
            if (key != null)
            {
                var val = key.GetValue(ThemeSettingValue);
                if (val is string str && Enum.TryParse<AppThemeMode>(str, out var mode))
                {
                    return mode;
                }
            }
        }
        catch
        {
        }
        return AppThemeMode.System;
    }

    private static void SaveThemeMode(AppThemeMode mode)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(WrsdSettingsKey);
            key?.SetValue(ThemeSettingValue, mode.ToString());
        }
        catch
        {
        }
    }
}
