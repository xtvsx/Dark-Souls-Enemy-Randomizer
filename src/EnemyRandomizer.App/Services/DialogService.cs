using System.IO;
using System.Windows;
using System.Windows.Threading;
using EnemyRandomizer.App.ViewModels;
using EnemyRandomizer.App.Views;
using EnemyRandomizer.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Win32;

namespace EnemyRandomizer.App.Services;

/// <summary>
/// WPF implementation of <see cref="IDialogService"/>. Every message goes through the one themed
/// <see cref="DialogWindow"/> (no Windows message boxes, which ignore dark mode). Tool windows are created by DI and kept single-instance.
/// </summary>
public sealed class DialogService(IServiceProvider services) : IDialogService, IUiThread
{
    private readonly Dictionary<Type, Window> _openWindows = [];

    private static Window? Owner => Application.Current?.MainWindow is { IsLoaded: true } main ? main : null;

    public void ShowInfo(string title, string message) => Run(DialogViewModel.Info(title, message));

    public void ShowWarning(string title, string message) => Run(DialogViewModel.Warning(title, message));

    public void ShowError(string title, string message) => Run(DialogViewModel.Error(title, message));

    public bool Confirm(string title, string message) => Run(DialogViewModel.Confirm(title, message)) == DialogChoice.Yes;

    public bool? ConfirmOrCancel(string title, string message) =>
        Run(DialogViewModel.ConfirmOrCancel(title, message)) switch
        {
            DialogChoice.Yes => true,
            DialogChoice.No => false,
            _ => null,
        };

    public string? OpenFile(string filter, string? initialDirectory)
    {
        var dialog = new OpenFileDialog { Filter = filter, CheckFileExists = true };
        if (initialDirectory is not null && Directory.Exists(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public void ShowTextConfig() => ShowSingle<TextConfigWindow>();

    public void ShowEnemyConfigEditor() => ShowSingle<EnemyConfigEditorWindow>();

    /// <summary>About uses the same dialog as every other message, with the link to the original project.</summary>
    public void ShowAbout()
    {
        var app = services.GetRequiredService<IOptions<AppSettings>>().Value;
        var messages = services.GetRequiredService<IOptions<MessageSettings>>().Value;
        var text = messages.Get(MessageKeys.AboutText) + "\n\n" + messages.Get(MessageKeys.AboutThanks, app.OriginalAuthor);
        Run(DialogViewModel.About(messages.Get(MessageKeys.AboutTitle), text, app.ProjectUrl, services.GetRequiredService<IAppShell>()));
    }

    public void ShowBackups()
    {
        var window = services.GetRequiredService<BackupsWindow>();
        window.Owner = Owner;
        window.ShowDialog();
    }

    public void Post(Action action) =>
        Application.Current.Dispatcher.BeginInvoke(action, DispatcherPriority.Background);

    private static DialogChoice Run(DialogViewModel viewModel)
    {
        var window = new DialogWindow(viewModel);
        if (Owner is { } owner)
        {
            window.Owner = owner;
        }
        else
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        window.ShowDialog();
        return viewModel.Result;
    }

    private void ShowSingle<TWindow>()
        where TWindow : Window
    {
        if (_openWindows.TryGetValue(typeof(TWindow), out var open))
        {
            open.Activate();
            return;
        }

        var window = services.GetRequiredService<TWindow>();
        window.Owner = Owner;
        window.Closed += (_, _) => _openWindows.Remove(typeof(TWindow));
        _openWindows[typeof(TWindow)] = window;
        window.Show();
    }
}
