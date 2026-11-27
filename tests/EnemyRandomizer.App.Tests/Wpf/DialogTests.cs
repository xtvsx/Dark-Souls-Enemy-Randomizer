using System.Windows;
using EnemyRandomizer.App.Services;
using EnemyRandomizer.App.ViewModels;
using EnemyRandomizer.App.Views;

namespace EnemyRandomizer.App.Tests.Wpf;

public class DialogViewModelTests
{
    [Fact]
    public void Info_has_one_button_that_is_default_and_cancel()
    {
        var vm = DialogViewModel.Info("Title", "Text");

        var button = Assert.Single(vm.Buttons);
        Assert.True(button.IsDefault);
        Assert.True(button.IsCancel);
        Assert.Equal(DialogChoice.None, vm.Result); // closing with the X chooses nothing
    }

    [Fact]
    public void Confirm_offers_yes_and_no_and_escape_means_no()
    {
        var vm = DialogViewModel.Confirm("t", "m");

        Assert.Equal([DialogChoice.Yes, DialogChoice.No], vm.Buttons.Select(b => b.Choice));
        Assert.Equal(DialogChoice.No, vm.Buttons.Single(b => b.IsCancel).Choice);
    }

    [Fact]
    public void ConfirmOrCancel_offers_three_buttons_and_escape_means_cancel()
    {
        var vm = DialogViewModel.ConfirmOrCancel("t", "m");

        Assert.Equal([DialogChoice.Yes, DialogChoice.No, DialogChoice.Cancel], vm.Buttons.Select(b => b.Choice));
        Assert.Equal(DialogChoice.Cancel, vm.Buttons.Single(b => b.IsCancel).Choice);
    }

    [Fact]
    public void Pressing_a_button_stores_the_choice_and_asks_the_window_to_close()
    {
        var vm = DialogViewModel.ConfirmOrCancel("t", "m");
        var closed = 0;
        vm.CloseRequested += (_, _) => closed++;

        vm.ChooseCommand.Execute(vm.Buttons[1]);

        Assert.Equal(DialogChoice.No, vm.Result);
        Assert.Equal(1, closed);
    }

    [Fact]
    public void About_shows_the_link_and_opens_it_through_the_shell()
    {
        var shell = new FakeShell();
        var vm = DialogViewModel.About("About", "text", "https://example.org/project", shell);

        Assert.True(vm.HasLink);
        vm.OpenLinkCommand.Execute(null);

        Assert.Equal(["https://example.org/project"], shell.Opened);
    }

    [Fact]
    public void Messages_have_no_link()
    {
        var vm = DialogViewModel.Info("t", "m");

        Assert.False(vm.HasLink);
        vm.OpenLinkCommand.Execute(null); // does nothing, must not throw
    }
}

/// <summary>The dialog really looks right: readable in both themes, never taller than the screen, About and info messages alike.</summary>
[Collection(WpfTestGroup.Name)]
public class DialogWindowTests
{
    private static readonly string LongText = string.Join("\n\n", Enumerable.Repeat(
        "The enemy placement copying lets you directly copy the enemy placement from a reference file if, for some reason, the same settings and seed fail to produce the same placement for different people.", 30));

    private static DialogViewModel About() => DialogViewModel.About(
        "About",
        "This is a .NET 10 migration of the Python Dark Souls Enemy Randomizer (v0.4.1.2), forked from:\n\nThanks to rycheNhavalys, the original author of the Enemy Randomizer.",
        "https://github.com/xtvsx/Dark-Souls-Enemy-Randomizer",
        new FakeShell());

    [Theory]
    [InlineData(AppTheme.Dark, "about")]
    [InlineData(AppTheme.Light, "about")]
    [InlineData(AppTheme.Dark, "long")]
    [InlineData(AppTheme.Light, "long")]
    [InlineData(AppTheme.Dark, "error")]
    [InlineData(AppTheme.Light, "error")]
    public async Task Dialog_is_readable_and_fits_on_screen(AppTheme theme, string kind)
    {
        var viewModel = kind switch
        {
            "about" => About(),
            "long" => DialogViewModel.Info("Enemy copy info", LongText),
            _ => DialogViewModel.Error("Randomizing failed", "Access to the path 'C:/game/map/MapStudio/m10_00_00_00.msb' is denied."),
        };

        var (findings, height, maxHeight, tooSmall) = await WpfTestHost.RunAsync(() =>
        {
            ThemeService.Apply(theme);
            var window = new DialogWindow(viewModel);
            UiProbe.ShowOffScreen(window);
            UiProbe.Render(window, $"dialog-{kind}-{theme}");
            var result = (UiProbe.LowContrastText(window), window.ActualHeight, window.MaxHeight, window.ActualWidth < 400);
            window.Close();
            return result;
        });

        Assert.Empty(findings);
        Assert.False(tooSmall);
        Assert.True(height <= maxHeight + 1, $"dialog is {height} high, the limit is {maxHeight}");
        Assert.True(maxHeight < SystemParameters.WorkArea.Height);
    }
}
