using System.Windows;
using Microsoft.Win32;

namespace EnemyRandomizer.App.Services;

/// <summary>Which palette the UI uses. <see cref="System"/> follows the Windows app-mode setting.</summary>
public enum AppTheme
{
    System,
    Light,
    Dark,
}

/// <summary>
/// Switches the built-in Fluent theme mode and our own colour palette (<c>Colors.Light/Dark.xaml</c>) together.
/// Both always follow the same light/dark decision, so they can never disagree.
/// </summary>
public static class ThemeService
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string PaletteFile = "Colors.";

    // Absolute pack URIs: a relative URI would be resolved against the entry assembly, which is not this one in tests.
    private static readonly Uri LightUri = new("pack://application:,,,/EnemyRandomizer;component/Themes/Colors.Light.xaml", UriKind.Absolute);
    private static readonly Uri DarkUri = new("pack://application:,,,/EnemyRandomizer;component/Themes/Colors.Dark.xaml", UriKind.Absolute);

    private static AppTheme _mode = AppTheme.System;
    private static bool _watching;

    public static bool SystemPrefersDark()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
        return key?.GetValue("AppsUseLightTheme") is 0;
    }

    /// <summary>True when the palette in use is the dark one.</summary>
    public static bool IsDark { get; private set; }

    public static void Apply(AppTheme mode)
    {
        _mode = mode;
        WatchWindowsSetting(mode == AppTheme.System);

        var dark = mode == AppTheme.Dark || (mode == AppTheme.System && SystemPrefersDark());
        IsDark = dark;

        // Fluent restyles every control; the palette below colours our own surfaces (cards, sidebar, text).
        Application.Current.ThemeMode = dark ? ThemeMode.Dark : ThemeMode.Light;

        // Exactly one palette lives in the resources. The new one goes last: WPF resolves merged dictionaries last-wins,
        // so adding it at the front (or next to the old one) would keep the old colours.
        var merged = Application.Current.Resources.MergedDictionaries;
        foreach (var old in merged.Where(IsPalette).ToList())
        {
            merged.Remove(old);
        }

        merged.Add(new ResourceDictionary { Source = dark ? DarkUri : LightUri });
    }

    private static bool IsPalette(ResourceDictionary dictionary) =>
        dictionary.Source?.OriginalString.Contains(PaletteFile, StringComparison.Ordinal) == true;

    /// <summary>"Follow Windows" must also follow the setting while the app is running.</summary>
    private static void WatchWindowsSetting(bool watch)
    {
        if (watch == _watching)
        {
            return;
        }

        _watching = watch;
        if (watch)
        {
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }
        else
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        }
    }

    private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (_mode == AppTheme.System && e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color)
        {
            Application.Current?.Dispatcher.BeginInvoke(() => Apply(AppTheme.System));
        }
    }
}
