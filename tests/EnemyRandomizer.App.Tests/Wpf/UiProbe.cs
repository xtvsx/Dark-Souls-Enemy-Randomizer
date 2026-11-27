using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace EnemyRandomizer.App.Tests.Wpf;

/// <summary>A piece of text whose colour is too close to the colour behind it.</summary>
internal sealed record ContrastFinding(string Text, Color Foreground, Color Background, double Ratio)
{
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"'{Text}' {Foreground} on {Background} = {Ratio:0.0}:1");
}

/// <summary>Looks at a shown window: renders it to a PNG and finds text that cannot be read.</summary>
internal static class UiProbe
{
    /// <summary>WCAG asks 4.5:1 for normal text; 3:1 is the floor for large or muted secondary text.</summary>
    public const double MinimumContrast = 3.0;

    /// <summary>Shows the window off-screen and lets the layout and the data bindings settle.</summary>
    public static void ShowOffScreen(Window window)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -20000;
        window.Top = 0;
        window.ShowActivated = false;
        window.ShowInTaskbar = false;
        window.Show();
        Flush();
    }

    public static void Flush() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);

    /// <summary>Saves what the window looks like; the file can be opened to check the result by eye.</summary>
    public static string Render(Window window, string name)
    {
        window.UpdateLayout();
        var width = (int)Math.Ceiling(window.ActualWidth);
        var height = (int)Math.Ceiling(window.ActualHeight);
        var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);

        // The window background is drawn by the window itself, which Render(visual) does not include for non-client parts.
        var fill = new DrawingVisual();
        using (var context = fill.RenderOpen())
        {
            context.DrawRectangle(window.Background, null, new Rect(0, 0, width, height));
        }

        target.Render(fill);
        target.Render((Visual)VisualTreeHelper.GetChild(window, 0));

        var file = Path.Combine(WpfTestHost.ScreenshotDirectory, name + ".png");
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(target));
        using var stream = File.Create(file);
        encoder.Save(stream);
        return file;
    }

    /// <summary>Relative luminance (WCAG) of a colour, 0 = black, 1 = white.</summary>
    public static double Luminance(Color color)
    {
        static double Channel(byte value)
        {
            var c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(color.R)) + (0.7152 * Channel(color.G)) + (0.0722 * Channel(color.B));
    }

    public static double Contrast(Color a, Color b)
    {
        var (light, dark) = Luminance(a) >= Luminance(b) ? (Luminance(a), Luminance(b)) : (Luminance(b), Luminance(a));
        return (light + 0.05) / (dark + 0.05);
    }

    public static Color? ColorOf(Brush? brush) => brush is SolidColorBrush { Color.A: > 0 } solid ? solid.Color : null;

    /// <summary>Every visible, enabled text of the window whose colour is hard to read on the colour behind it.</summary>
    public static IReadOnlyList<ContrastFinding> LowContrastText(Window window)
    {
        var findings = new List<ContrastFinding>();
        foreach (var text in Descendants<TextBlock>(window))
        {
            if (!text.IsVisible || !text.IsEnabled || string.IsNullOrWhiteSpace(text.Text) || text.ActualWidth <= 0)
            {
                continue;
            }

            if (ColorOf(text.Foreground) is not { } foreground)
            {
                continue;
            }

            var background = BackgroundBehind(text) ?? ColorOf(window.Background) ?? Colors.White;
            var ratio = Contrast(foreground, background);
            if (ratio < MinimumContrast)
            {
                findings.Add(new ContrastFinding(text.Text.Trim(), foreground, background, ratio));
            }
        }

        return findings;
    }

    /// <summary>The nearest opaque background colour that is really painted behind the element, or <c>null</c> when none is set.</summary>
    private static Color? BackgroundBehind(DependencyObject element)
    {
        for (var parent = VisualTreeHelper.GetParent(element); parent is not null; parent = VisualTreeHelper.GetParent(parent))
        {
            var brush = parent switch
            {
                Border border => border.Background,
                Panel panel => panel.Background,
                _ => null, // a Control's own Background is only painted when its template draws it (its Border then has it)
            };

            if (brush is SolidColorBrush { Color.A: > 200 } solid)
            {
                return solid.Color;
            }
        }

        return null;
    }

    public static IEnumerable<T> Descendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var nested in Descendants<T>(child))
            {
                yield return nested;
            }
        }
    }
}
