using System.Windows;
using EnemyRandomizer.App.Services;

namespace EnemyRandomizer.App.Tests.Wpf;

/// <summary>Light and dark mode really change the window, and switching back and forth keeps working.</summary>
[Collection(WpfTestGroup.Name)]
public class ThemeTests
{
    private static bool IsDark(Window window) => UiProbe.Luminance(UiProbe.ColorOf(window.Background)!.Value) < 0.25;

    private static bool IsLight(Window window) => UiProbe.Luminance(UiProbe.ColorOf(window.Background)!.Value) > 0.6;

    private static async Task<(Window Window, string Screenshot)> Show(AppTheme theme)
    {
        var (window, _) = await WpfTestHost.CreateMainWindowAsync();
        return await WpfTestHost.RunAsync(() =>
        {
            ThemeService.Apply(theme);
            UiProbe.ShowOffScreen(window);
            return ((Window)window, UiProbe.Render(window, "main-" + theme));
        });
    }

    [Fact]
    public async Task Dark_theme_gives_a_dark_window_with_readable_text()
    {
        var (window, _) = await Show(AppTheme.Dark);

        var report = await WpfTestHost.RunAsync(() => (IsDark(window), UiProbe.LowContrastText(window)));
        await WpfTestHost.RunAsync(window.Close);

        Assert.True(report.Item1, "window background is not dark");
        Assert.Empty(report.Item2);
    }

    [Fact]
    public async Task Light_theme_gives_a_light_window_with_readable_text()
    {
        var (window, _) = await Show(AppTheme.Light);

        var report = await WpfTestHost.RunAsync(() => (IsLight(window), UiProbe.LowContrastText(window)));
        await WpfTestHost.RunAsync(window.Close);

        Assert.True(report.Item1, "window background is not light");
        Assert.Empty(report.Item2);
    }

    [Fact]
    public async Task Switching_the_theme_at_runtime_keeps_working()
    {
        var (window, viewModel) = await WpfTestHost.CreateMainWindowAsync();
        await WpfTestHost.RunAsync(() => UiProbe.ShowOffScreen(window));

        foreach (var theme in new[] { AppTheme.Dark, AppTheme.Light, AppTheme.Dark, AppTheme.Light, AppTheme.Dark })
        {
            var result = await WpfTestHost.RunAsync(() =>
            {
                viewModel.SetThemeCommand.Execute(theme);
                UiProbe.Flush();
                var palettes = Application.Current.Resources.MergedDictionaries
                    .Count(d => d.Source?.OriginalString.Contains("Colors.", StringComparison.Ordinal) == true);
                var wanted = theme == AppTheme.Dark ? IsDark(window) : IsLight(window);
                UiProbe.Render(window, "switch-" + theme);
                return (palettes, wanted, UiProbe.LowContrastText(window));
            });

            Assert.Equal(1, result.palettes); // switching replaces the palette, it never stacks them
            Assert.True(result.wanted, $"window did not change to {theme}");
            Assert.Empty(result.Item3);
        }

        await WpfTestHost.RunAsync(window.Close);
    }
}
