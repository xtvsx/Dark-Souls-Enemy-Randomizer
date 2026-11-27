using EnemyRandomizer.Core;
using EnemyRandomizer.Core.Abstractions;
using EnemyRandomizer.Core.Configuration;
using EnemyRandomizer.Infrastructure.Game;
using EnemyRandomizer.Infrastructure.Setup;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EnemyRandomizer.Infrastructure.Tests.Setup;

/// <summary>The backup folder: initial state, snapshots, migration of v0.4.x <c>.bak</c> files.</summary>
public sealed class GameBackupsTests : IDisposable
{
    private readonly TempGameFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    private GameBackups Backups => _folder.Backups;

    private static byte[] Bytes(byte fill, int length = 20) => [.. Enumerable.Repeat(fill, length)];

    private string GameFile(string name, byte fill = 1) => _folder.Touch("map/MapStudio/" + name, Bytes(fill));

    [Fact]
    public void First_backup_goes_to_the_initial_folder_with_a_manifest_and_is_never_overwritten()
    {
        var file = GameFile("m10.msb", 1);

        Backups.EnsureBackup(file);
        File.WriteAllBytes(file, Bytes(2));
        Backups.EnsureBackup(file);

        var backup = Backups.BackupOf(file);
        Assert.StartsWith(Path.Combine(_folder.Paths.BackupDirectory, "initial"), backup, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(Bytes(1), File.ReadAllBytes(backup));
        Assert.True(File.Exists(Path.Combine(_folder.Paths.BackupDirectory, "initial", GameBackups.ManifestName)));
        Assert.False(File.Exists(file + ".bak")); // nothing is put next to the game files any more
    }

    [Fact]
    public void WriteWithBackup_keeps_the_original_and_writes_the_new_content()
    {
        var file = GameFile("m10.msb", 1);

        Backups.WriteWithBackup(file, Bytes(9));

        Assert.Equal(Bytes(9), File.ReadAllBytes(file));
        Assert.Equal(Bytes(1), File.ReadAllBytes(Backups.BackupOf(file)));
        Assert.True(Backups.Restore(file));
        Assert.Equal(Bytes(1), File.ReadAllBytes(file));
    }

    [Fact]
    public void Original_is_the_file_itself_until_it_is_backed_up()
    {
        var file = GameFile("m10.msb");

        Assert.Equal(file, Backups.Original(file));
        Assert.False(Backups.Restore(file));
    }

    [Fact]
    public void Legacy_bak_is_moved_into_the_initial_folder_when_the_file_is_touched()
    {
        var file = GameFile("m10.msb", 5);
        File.WriteAllBytes(file + ".bak", Bytes(1));

        var original = Backups.Original(file);

        Assert.Equal(Backups.BackupOf(file), original);
        Assert.Equal(Bytes(1), File.ReadAllBytes(original));
        Assert.False(File.Exists(file + ".bak"));
        Assert.Equal(Bytes(5), File.ReadAllBytes(file)); // the game file itself is untouched
    }

    [Fact]
    public void Bulk_migration_counts_the_moved_backups()
    {
        var a = GameFile("a.msb");
        var b = GameFile("b.msb");
        var c = GameFile("c.msb");
        File.WriteAllBytes(a + ".bak", Bytes(1));
        File.WriteAllBytes(b + ".bak", Bytes(1));

        Assert.Equal(2, Backups.MigrateLegacy([a, b, c]));
        Assert.Equal(0, Backups.MigrateLegacy([a, b, c]));
    }

    [Fact]
    public void A_legacy_bak_that_differs_from_an_existing_backup_is_kept()
    {
        var file = GameFile("m10.msb");
        Backups.WriteBackup(file, Bytes(1, 30));
        File.WriteAllBytes(file + ".bak", Bytes(2, 20));

        Backups.MigrateLegacy([file]);

        Assert.True(File.Exists(file + ".bak")); // nothing is deleted that was not copied exactly
        Assert.Equal(Bytes(1, 30), File.ReadAllBytes(Backups.BackupOf(file)));
    }

    [Fact]
    public void No_snapshot_while_the_game_is_still_original()
    {
        var file = GameFile("m10.msb");

        Assert.Null(Backups.CreateSnapshot([file]));          // never backed up: nothing randomized yet
        Backups.EnsureBackup(file);
        Assert.Null(Backups.CreateSnapshot([file]));          // backed up but identical
        Assert.Empty(Backups.SnapshotIds());
    }

    [Fact]
    public void Snapshot_keeps_the_current_content_when_the_game_is_randomized()
    {
        var a = GameFile("a.msb", 1);
        var b = GameFile("b.msb", 1);
        Backups.WriteWithBackup(a, Bytes(7));                  // a is randomized, b is not

        var id = Backups.CreateSnapshot([a, b]);

        Assert.NotNull(id);
        Assert.True(GameBackups.IsSnapshotId(id));
        var files = Backups.FilesOf(id).ToDictionary(f => Backups.GameFileOf(id, f));
        Assert.Equal(Bytes(7), File.ReadAllBytes(files[a]));
        Assert.Equal(Bytes(1), File.ReadAllBytes(files[b]));
        Assert.Equal(Bytes(1), File.ReadAllBytes(Backups.BackupOf(a)));    // the initial backup is untouched
    }

    [Fact]
    public void Only_the_newest_snapshots_are_kept_and_the_initial_backup_never_goes()
    {
        var file = GameFile("a.msb", 1);
        Backups.WriteWithBackup(file, Bytes(2));
        var kept = _folder.Settings.MaxBackupSnapshots; // 5 by default

        var ids = Enumerable.Range(0, kept + 2).Select(_ => Backups.CreateSnapshot([file])!).ToList();

        Assert.Equal(kept, Backups.SnapshotIds().Count);
        Assert.Equal(ids.Skip(2).Reverse(), Backups.SnapshotIds()); // newest first, the two oldest are gone
        Assert.True(Backups.InitialExists);
    }

    [Fact]
    public void Snapshots_can_be_restored_and_deleted_but_the_initial_set_cannot_be_deleted()
    {
        var file = GameFile("a.msb", 1);
        Backups.WriteWithBackup(file, Bytes(2));
        var id = Backups.CreateSnapshot([file])!;
        File.WriteAllBytes(file, Bytes(3));

        Assert.Equal(1, Backups.RestoreSnapshot(id));
        Assert.Equal(Bytes(2), File.ReadAllBytes(file));

        Assert.False(Backups.DeleteSnapshot("initial"));
        Assert.False(Backups.DeleteSnapshot("..")); // ids are never paths
        Assert.False(Backups.DeleteSnapshot(@"..\..\game"));
        Assert.True(Backups.DeleteSnapshot(id));
        Assert.Empty(Backups.SnapshotIds());
        Assert.True(Backups.InitialExists);
    }

    [Fact]
    public void Files_outside_the_game_folder_are_refused()
    {
        Assert.Throws<ArgumentException>(() => Backups.BackupOf(Path.Combine(Path.GetTempPath(), "elsewhere.msb")));
    }
}

public sealed class BackupServiceTests : IDisposable
{
    private readonly TempGameFolder _folder = new();
    private readonly GameDataSettings _gameData = ShippedSettings.Get<GameDataSettings>();
    private readonly GameFileLayout _layout;

    public BackupServiceTests() => _layout = new GameFileLayout(Options.Create(_gameData));

    public void Dispose() => _folder.Dispose();

    private BackupService Service(GameEdition? edition = GameEdition.Ptde) => new(
        _folder.Paths,
        new FixedInstallation(edition),
        _layout,
        _folder.Backups,
        new GameRestorer(_folder.Paths, _layout, _folder.Backups, new NoExePatcher(), Options.Create(_gameData), NullLogger<GameRestorer>.Instance),
        NullLogger<BackupService>.Instance);

    private static byte[] Bytes(byte fill, int length = 20) => [.. Enumerable.Repeat(fill, length)];

    private string MapFile(string id = "m10_00_00_00") => _folder.Paths.InGame(_layout.MapStudio(id));

    [Fact]
    public void No_game_found_gives_an_empty_report()
    {
        var report = Service(edition: null).Check();

        Assert.Empty(report.Sets);
    }

    [Fact]
    public void Before_the_first_randomization_there_is_no_backup_and_the_game_is_original()
    {
        _folder.CreateGameFiles(GameEdition.Ptde, _layout);

        var report = Service().Check();

        Assert.Empty(report.Sets);
        Assert.Equal(GameFilesState.Original, report.GameState);
        Assert.Equal(_folder.Paths.BackupDirectory, Service().BackupDirectory);
    }

    [Fact]
    public void Initial_set_comes_first_and_tells_whether_the_game_is_randomized()
    {
        _folder.CreateGameFiles(GameEdition.Ptde, _layout);
        _folder.Backups.WriteWithBackup(MapFile(), Bytes(9));
        var service = Service();

        var randomized = service.Check();

        var initial = Assert.Single(randomized.Sets);
        Assert.Equal(BackupKind.Initial, initial.Kind);
        Assert.Equal("initial", initial.Id);
        Assert.Equal(1, initial.FileCount);
        Assert.False(initial.IsCurrent);
        Assert.False(initial.IsBroken);
        Assert.Equal(GameFilesState.Randomized, randomized.GameState);

        service.Restore("initial", restoreEffects: false);

        var original = service.Check();
        Assert.Equal(GameFilesState.Original, original.GameState);
        Assert.True(original.Sets[0].IsCurrent);
    }

    [Fact]
    public void Snapshots_follow_newest_first_and_the_matching_one_is_current()
    {
        _folder.CreateGameFiles(GameEdition.Ptde, _layout);
        var file = MapFile();
        _folder.Backups.WriteWithBackup(file, Bytes(2));
        var older = _folder.Backups.CreateSnapshot([file])!;
        File.WriteAllBytes(file, Bytes(3));
        var newer = _folder.Backups.CreateSnapshot([file])!;
        File.WriteAllBytes(file, Bytes(2)); // the game is back at the state of the older snapshot

        var report = Service().Check();

        Assert.Equal(["initial", newer, older], report.Sets.Select(s => s.Id));
        Assert.Equal(BackupKind.Snapshot, report.Sets[1].Kind);
        Assert.Equal([false, false, true], report.Sets.Select(s => s.IsCurrent));
    }

    [Fact]
    public void A_tiny_backup_file_marks_the_set_as_broken_and_is_not_restored()
    {
        _folder.CreateGameFiles(GameEdition.Ptde, _layout);
        var file = MapFile();
        var before = File.ReadAllBytes(file);
        _folder.Backups.WriteBackup(file, [1, 2, 3]);
        var service = Service();

        Assert.True(service.Check().Sets[0].IsBroken);
        service.Restore("initial", restoreEffects: false);

        Assert.Equal(before, File.ReadAllBytes(file)); // a broken backup never overwrites the game file...
    }

    [Fact]
    public void Legacy_bak_files_show_up_as_the_initial_state_and_leave_the_game_folder()
    {
        _folder.CreateGameFiles(GameEdition.Ptde, _layout);
        var file = MapFile();
        File.Copy(file, file + ".bak");

        var report = Service().Check();

        Assert.Equal("initial", Assert.Single(report.Sets).Id);
        Assert.False(File.Exists(file + ".bak"));
    }

    [Fact]
    public void A_snapshot_is_restored_and_deleted_but_the_initial_state_is_not_deleted()
    {
        _folder.CreateGameFiles(GameEdition.Ptde, _layout);
        var file = MapFile();
        _folder.Backups.WriteWithBackup(file, Bytes(2));
        var id = _folder.Backups.CreateSnapshot([file])!;
        File.WriteAllBytes(file, Bytes(3));
        var service = Service();

        Assert.Equal(1, service.Restore(id, restoreEffects: false));
        Assert.Equal(Bytes(2), File.ReadAllBytes(file));

        Assert.False(service.Delete("initial"));
        Assert.True(service.Delete(id));
        Assert.DoesNotContain(service.Check().Sets, s => s.Id == id);
    }

    private sealed class NoExePatcher : IExePatcher
    {
        public bool PatchIfNeeded() => true;

        public bool Restore() => false;
    }
}
