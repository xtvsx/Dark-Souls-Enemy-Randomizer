using EnemyRandomizer.App.ViewModels;
using EnemyRandomizer.Core.Abstractions;
using EnemyRandomizer.Core.Configuration;

namespace EnemyRandomizer.App.Tests;

public class BackupsViewModelTests
{
    private sealed class FakeBackups : IBackupService
    {
        public string BackupDirectory => "C:/game/data/backups";

        public List<BackupSet> Sets { get; } = [];

        public GameFilesState State { get; set; } = GameFilesState.Randomized;

        public List<(string Id, bool Effects)> Restored { get; } = [];

        public List<string> Deleted { get; } = [];

        public BackupReport Check() => new([.. Sets], State, BackupDirectory);

        public int Restore(string id, bool restoreEffects)
        {
            Restored.Add((id, restoreEffects));
            return 7;
        }

        public bool Delete(string id)
        {
            Deleted.Add(id);
            return Sets.RemoveAll(s => s.Id == id) > 0;
        }
    }

    private readonly FakeBackups _backups = new();
    private readonly FakeDialogs _dialogs = new();
    private readonly FakeShell _shell = new();

    private static BackupSet Initial(bool current = false, bool broken = false) =>
        new("initial", BackupKind.Initial, new DateTime(2026, 11, 1, 10, 0, 0), 53, 70_000_000, current, broken);

    private static BackupSet Snapshot(string id = "2026-11-27_17-40-12", bool current = false) =>
        new(id, BackupKind.Snapshot, new DateTime(2026, 11, 27, 17, 40, 12), 40, 30_000_000, current, false);

    private async Task<BackupsViewModel> Loaded()
    {
        var vm = new BackupsViewModel(_backups, _dialogs, _shell, ShippedConfig.Get<MessageSettings>());
        await vm.RefreshCommand.ExecuteAsync(null);
        return vm;
    }

    [Fact]
    public async Task Without_any_backup_it_explains_when_the_first_one_is_made()
    {
        var vm = await Loaded();

        Assert.Empty(vm.Items);
        Assert.Contains("first time", vm.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_first_backup_is_titled_initial_game_state_and_marked_as_original()
    {
        _backups.Sets.AddRange([Initial(), Snapshot()]);

        var vm = await Loaded();

        Assert.Equal(["Initial game state", "Before randomization"], vm.Items.Select(i => i.Title));
        Assert.Equal("Original game", vm.Items[0].Badge);
        Assert.False(vm.Items[0].CanDelete);
        Assert.True(vm.Items[1].CanDelete);
        Assert.Contains("2 backups", vm.Summary, StringComparison.Ordinal);
        Assert.Contains("randomized", vm.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_current_state_is_marked()
    {
        _backups.State = GameFilesState.Original;
        _backups.Sets.Add(Initial(current: true));

        var vm = await Loaded();

        Assert.Equal("Original game · current state", vm.Items[0].Badge);
        Assert.Contains("the original game", vm.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_list_never_shows_files_or_categories()
    {
        _backups.Sets.AddRange([Initial(), Snapshot()]);

        var vm = await Loaded();

        var shown = string.Join(" ", vm.Items.SelectMany(i => new[] { i.Title, i.Details, i.Badge }));
        Assert.DoesNotContain("map", shown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AI", shown, StringComparison.Ordinal);
        Assert.DoesNotContain(".msb", shown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Restoring_the_initial_state_asks_about_effect_files_like_revert()
    {
        _backups.Sets.Add(Initial());
        var vm = await Loaded();
        _dialogs.ConfirmOrCancelAnswer = false; // "No": keep the merged effect files

        await vm.RestoreCommand.ExecuteAsync(vm.Items[0]);

        Assert.Equal([("initial", false)], _backups.Restored);
        Assert.Contains("7 game files restored", _dialogs.Infos.Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancelling_the_question_restores_nothing()
    {
        _backups.Sets.Add(Initial());
        var vm = await Loaded();
        _dialogs.ConfirmOrCancelAnswer = null;

        await vm.RestoreCommand.ExecuteAsync(vm.Items[0]);

        Assert.Empty(_backups.Restored);
    }

    [Fact]
    public async Task Restoring_a_snapshot_needs_a_confirmation_and_never_touches_effect_files()
    {
        _backups.Sets.AddRange([Initial(), Snapshot()]);
        var vm = await Loaded();
        _dialogs.ConfirmAnswer = false;

        await vm.RestoreCommand.ExecuteAsync(vm.Items[1]);
        Assert.Empty(_backups.Restored);

        _dialogs.ConfirmAnswer = true;
        await vm.RestoreCommand.ExecuteAsync(vm.Items[1]);
        Assert.Equal([("2026-11-27_17-40-12", false)], _backups.Restored);
    }

    [Fact]
    public async Task Deleting_a_snapshot_removes_it_from_the_list_but_the_initial_state_cannot_be_deleted()
    {
        _backups.Sets.AddRange([Initial(), Snapshot()]);
        var vm = await Loaded();

        await vm.DeleteCommand.ExecuteAsync(vm.Items[0]);
        Assert.Empty(_backups.Deleted);

        await vm.DeleteCommand.ExecuteAsync(vm.Items[1]);
        Assert.Equal(["2026-11-27_17-40-12"], _backups.Deleted);
        Assert.Equal(["Initial game state"], vm.Items.Select(i => i.Title));
    }

    [Fact]
    public async Task A_damaged_backup_is_reported()
    {
        _backups.Sets.Add(Initial(broken: true));

        var vm = await Loaded();

        Assert.Contains("damaged", vm.Warning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Open_folder_opens_the_backup_directory()
    {
        var vm = await Loaded();

        vm.OpenFolderCommand.Execute(null);

        Assert.Contains("C:/game/data/backups", _shell.Opened);
    }
}
