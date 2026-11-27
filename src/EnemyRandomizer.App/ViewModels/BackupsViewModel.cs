using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EnemyRandomizer.App.Services;
using EnemyRandomizer.Core;
using EnemyRandomizer.Core.Abstractions;
using EnemyRandomizer.Core.Configuration;
using Microsoft.Extensions.Options;

namespace EnemyRandomizer.App.ViewModels;

/// <summary>One restore point: the initial state of the game or the state before one randomization.</summary>
public sealed class BackupSetViewModel(BackupSet set)
{
    public BackupSet Set { get; } = set;

    public string Id => Set.Id;

    public bool IsInitial => Set.Kind == BackupKind.Initial;

    public string Title => IsInitial ? "Initial game state" : "Before randomization";

    /// <summary>When it was made and how big it is, without telling the user which game files are inside.</summary>
    public string Details => string.Create(CultureInfo.CurrentCulture, $"{Set.Created:g} · {FormatSize(Set.Bytes)}");

    /// <summary>The marker of the first backup is always shown; "current" tells what the game looks like right now.</summary>
    public string Badge => (IsInitial, Set.IsCurrent) switch
    {
        (true, true) => "Original game · current state",
        (true, false) => "Original game",
        (false, true) => "Current state",
        _ => "",
    };

    public bool HasBadge => Badge.Length > 0;

    public bool CanDelete => !IsInitial;

    public bool IsBroken => Set.IsBroken;

    private static string FormatSize(long bytes) =>
        bytes >= 1024 * 1024 ? string.Create(CultureInfo.CurrentCulture, $"{bytes / 1024.0 / 1024.0:0.#} MB") : string.Create(CultureInfo.CurrentCulture, $"{Math.Max(1, bytes / 1024)} KB");
}

/// <summary>
/// Backup manager: one list of restore points (initial state first), no files or categories. Restoring the initial
/// state is the same as "Revert to normal"; a snapshot puts the game back to what it was before that randomization.
/// </summary>
public sealed partial class BackupsViewModel(
    IBackupService backups,
    IDialogService dialogs,
    IAppShell shell,
    IOptions<MessageSettings> messages) : ObservableObject
{
    public ObservableCollection<BackupSetViewModel> Items { get; } = [];

    [ObservableProperty]
    public partial string Summary { get; set; } = "Checking the backups...";

    /// <summary>Problem line shown in red, empty when everything is fine.</summary>
    [ObservableProperty]
    public partial string Warning { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestoreCommand), nameof(DeleteCommand), nameof(RefreshCommand))]
    public partial bool IsBusy { get; set; }

    public string BackupDirectory => backups.BackupDirectory;

    /// <summary>Checks all backups again (also what the "Verify" button does). Runs off the UI thread because it compares files.</summary>
    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            var report = await Task.Run(backups.Check).ConfigureAwait(true);
            Show(report);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task RestoreAsync(BackupSetViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var restoreEffects = false;
        if (item.IsInitial)
        {
            // Same question as "Revert to normal": the merged effect files can stay, they do no harm.
            if (dialogs.ConfirmOrCancel(messages.Value.Get(MessageKeys.RevertTitle), messages.Value.Get(MessageKeys.RevertAskEffects)) is not { } answer)
            {
                return;
            }

            restoreEffects = answer;
        }
        else if (!dialogs.Confirm("Restore backup", $"Put the game back to the state before this randomization ({item.Set.Created:g})?"))
        {
            return;
        }

        IsBusy = true;
        try
        {
            var restored = await Task.Run(() => backups.Restore(item.Id, restoreEffects)).ConfigureAwait(true);
            dialogs.ShowInfo("Restore backup", $"{restored} game files restored.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            dialogs.ShowError("Restore backup", $"Restoring failed: {ex.Message}\n\nClose the game and any program that uses its files, then try again.");
        }
        finally
        {
            IsBusy = false;
        }

        await RefreshAsync().ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task DeleteAsync(BackupSetViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!item.CanDelete || !dialogs.Confirm("Delete backup", $"Delete the backup from {item.Set.Created:g}? This cannot be undone."))
        {
            return;
        }

        try
        {
            backups.Delete(item.Id);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            dialogs.ShowError("Delete backup", ex.Message);
        }

        await RefreshAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private void OpenFolder()
    {
        shell.OpenFolder(backups.BackupDirectory);
    }

    private void Show(BackupReport report)
    {
        Items.Clear();
        foreach (var set in report.Sets)
        {
            Items.Add(new BackupSetViewModel(set));
        }

        var state = report.GameState == GameFilesState.Original ? "the original game" : "randomized";
        Summary = report.Sets.Count == 0
            ? "No backup yet. The first backup of the original game is made when you randomize for the first time."
            : $"The game is currently {state}. {report.Sets.Count} backup{(report.Sets.Count == 1 ? "" : "s")} available.";
        var broken = report.Sets.Count(s => s.IsBroken);
        Warning = broken == 0
            ? ""
            : $"{broken} backup{(broken == 1 ? " is" : "s are")} damaged and cannot be fully restored. Verify the game files with Steam to repair the game.";
    }

    private bool IsNotBusy() => !IsBusy;
}
