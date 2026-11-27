using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EnemyRandomizer.App.Services;
using EnemyRandomizer.Core.Abstractions;

namespace EnemyRandomizer.App.ViewModels;

/// <summary>One row of the backup list.</summary>
public sealed partial class BackupItemViewModel(BackupEntry entry) : ObservableObject
{
    public BackupEntry Entry { get; } = entry;

    public string Group => Entry.Group;

    public string Name => Entry.Name;

    public string Status => Entry.State == BackupState.Broken ? "Broken backup"
        : Entry.GameFileChanged ? "Randomized, can be restored"
        : "Original";

    public bool CanRestore => Entry.State == BackupState.Intact;

    public string Date => Entry.BackupModified.ToString("g", CultureInfo.CurrentCulture);

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>Backup manager: shows which game files have a backup, whether it is usable, and restores chosen files.</summary>
public sealed partial class BackupsViewModel : ObservableObject
{
    private readonly IBackupService _backups;
    private readonly IDialogService _dialogs;

    public BackupsViewModel(IBackupService backups, IDialogService dialogs)
    {
        _backups = backups;
        _dialogs = dialogs;
        Refresh();
    }

    public ObservableCollection<BackupItemViewModel> Items { get; } = [];

    [ObservableProperty]
    public partial string Summary { get; set; } = "";

    /// <summary>Problem line shown in red (broken backups), empty when everything is fine.</summary>
    [ObservableProperty]
    public partial string Warning { get; set; } = "";

    /// <summary>Checks the backups again (also what the "Verify" button does).</summary>
    [RelayCommand]
    private void Refresh()
    {
        var report = _backups.Check();

        Items.Clear();
        foreach (var entry in report.Entries)
        {
            var item = new BackupItemViewModel(entry);
            item.PropertyChanged += (_, _) => RestoreSelectedCommand.NotifyCanExecuteChanged();
            Items.Add(item);
        }

        Summary = report.Entries.Count == 0
            ? "No backups yet. The randomizer makes one the first time it changes a game file."
            : $"{report.Entries.Count} backups, {report.ChangedCount} of the game files are currently randomized. "
              + $"{report.FilesWithoutBackup} files were never changed.";
        Warning = report.BrokenCount == 0
            ? ""
            : $"{report.BrokenCount} backups are broken (too small) and are skipped when restoring. Verify the game files with Steam to repair them.";
        RestoreSelectedCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void SelectChanged()
    {
        foreach (var item in Items)
        {
            item.IsSelected = item.CanRestore && item.Entry.GameFileChanged;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void RestoreSelected()
    {
        var chosen = Items.Where(i => i.IsSelected && i.CanRestore).Select(i => i.Entry).ToList();
        if (chosen.Count == 0 || !_dialogs.Confirm("Restore backups", $"Replace {chosen.Count} game files with their original backups?"))
        {
            return;
        }

        var restored = _backups.Restore(chosen);
        Refresh();
        _dialogs.ShowInfo("Restore backups", $"{restored} files restored.");
    }

    private bool HasSelection() => Items.Any(i => i.IsSelected && i.CanRestore);
}
