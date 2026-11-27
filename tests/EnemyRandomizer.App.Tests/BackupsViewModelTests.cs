using EnemyRandomizer.App.ViewModels;
using EnemyRandomizer.Core.Abstractions;

namespace EnemyRandomizer.App.Tests;

public class BackupsViewModelTests
{
    private sealed class FakeBackups : IBackupService
    {
        public List<BackupEntry> Entries { get; } = [];

        public List<BackupEntry> Restored { get; } = [];

        public BackupReport Check() => new([.. Entries], 3);

        public int Restore(IEnumerable<BackupEntry> entries)
        {
            Restored.AddRange(entries);
            foreach (var entry in Restored)
            {
                Entries[Entries.FindIndex(e => e.GamePath == entry.GamePath)] = entry with { GameFileChanged = false };
            }

            return Restored.Count;
        }
    }

    private readonly FakeBackups _backups = new();
    private readonly FakeDialogs _dialogs = new();

    private static BackupEntry Entry(string name, bool changed = true, BackupState state = BackupState.Intact) =>
        new("Maps", name, @"C:\game\" + name, state, changed, DateTime.Now);

    private BackupsViewModel Create() => new(_backups, _dialogs);

    [Fact]
    public void Empty_report_explains_that_backups_appear_after_the_first_run()
    {
        var vm = Create();

        Assert.Empty(vm.Items);
        Assert.Contains("No backups yet", vm.Summary, StringComparison.Ordinal);
        Assert.Equal("", vm.Warning);
    }

    [Fact]
    public void Summary_counts_backups_and_randomized_files_and_warns_about_broken_ones()
    {
        _backups.Entries.AddRange([Entry("a"), Entry("b", changed: false), Entry("c", state: BackupState.Broken)]);

        var vm = Create();

        Assert.Equal(3, vm.Items.Count);
        Assert.Contains("3 backups, 2 of the game files", vm.Summary, StringComparison.Ordinal);
        Assert.Contains("1 backups are broken", vm.Warning, StringComparison.Ordinal);
    }

    [Fact]
    public void Select_randomized_picks_only_restorable_changed_files()
    {
        _backups.Entries.AddRange([Entry("a"), Entry("b", changed: false), Entry("c", state: BackupState.Broken)]);
        var vm = Create();

        vm.SelectChangedCommand.Execute(null);

        Assert.Equal(["a"], vm.Items.Where(i => i.IsSelected).Select(i => i.Name));
    }

    [Fact]
    public void Restore_needs_a_selection_and_a_confirmation()
    {
        _backups.Entries.Add(Entry("a"));
        var vm = Create();
        Assert.False(vm.RestoreSelectedCommand.CanExecute(null));

        vm.Items[0].IsSelected = true;
        Assert.True(vm.RestoreSelectedCommand.CanExecute(null));

        _dialogs.ConfirmAnswer = false;
        vm.RestoreSelectedCommand.Execute(null);
        Assert.Empty(_backups.Restored);

        _dialogs.ConfirmAnswer = true;
        vm.RestoreSelectedCommand.Execute(null);
        Assert.Single(_backups.Restored);
        Assert.Contains("1 files restored", _dialogs.Infos.Single(), StringComparison.Ordinal);
        Assert.Equal("Original", vm.Items.Single().Status); // list was checked again
    }

    [Fact]
    public void Broken_backup_cannot_be_selected_for_restore()
    {
        _backups.Entries.Add(Entry("a", state: BackupState.Broken));
        var vm = Create();

        vm.Items[0].IsSelected = true;

        Assert.False(vm.RestoreSelectedCommand.CanExecute(null));
    }
}
