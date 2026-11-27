using EnemyRandomizer.Core;
using EnemyRandomizer.Core.Abstractions;
using EnemyRandomizer.Core.Configuration;
using EnemyRandomizer.Infrastructure.Game;
using EnemyRandomizer.Infrastructure.Setup;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EnemyRandomizer.Infrastructure.Tests.Setup;

public sealed class BackupServiceTests : IDisposable
{
    private readonly TempGameFolder _folder = new();
    private readonly GameDataSettings _gameData = ShippedSettings.Get<GameDataSettings>();
    private readonly GameFileLayout _layout;

    public BackupServiceTests() => _layout = new GameFileLayout(Options.Create(_gameData));

    public void Dispose() => _folder.Dispose();

    private BackupService Service(GameEdition? edition = GameEdition.Ptde) =>
        new(_folder.Paths, new FixedInstallation(edition), _layout, Options.Create(_gameData), NullLogger<BackupService>.Instance);

    private static byte[] Bytes(byte fill, int length = 20) => [.. Enumerable.Repeat(fill, length)];

    [Fact]
    public void No_game_found_gives_an_empty_report()
    {
        var report = Service(edition: null).Check();

        Assert.Empty(report.Entries);
        Assert.Equal(0, report.FilesWithoutBackup);
    }

    [Fact]
    public void Files_without_backup_are_only_counted()
    {
        _folder.CreateGameFiles(GameEdition.Ptde, _layout);

        var report = Service().Check();

        Assert.Empty(report.Entries);
        Assert.True(report.FilesWithoutBackup > 0);
    }

    [Fact]
    public void Randomized_file_is_reported_as_changed_and_untouched_one_as_original()
    {
        _folder.CreateGameFiles(GameEdition.Ptde, _layout);
        var changed = _folder.Paths.InGame(_layout.MapStudio("m10_00_00_00"));
        var untouched = _folder.Paths.InGame(_layout.MapStudio("m10_01_00_00"));
        BackupFiles.WriteWithBackup(changed, Bytes(9));
        BackupFiles.EnsureBackup(untouched);

        var report = Service().Check();

        var entries = report.Entries.ToDictionary(e => e.GamePath);
        Assert.Equal(2, entries.Count);
        Assert.True(entries[changed].GameFileChanged);
        Assert.False(entries[untouched].GameFileChanged);
        Assert.All(entries.Values, e => Assert.Equal(BackupState.Intact, e.State));
        Assert.Equal(1, report.ChangedCount);
    }

    [Fact]
    public void Tiny_backup_is_broken()
    {
        var file = _folder.Touch(_layout.MapStudio("m10_00_00_00"), Bytes(1));
        File.WriteAllBytes(BackupFiles.BackupOf(file), [1, 2, 3]);

        var entry = Assert.Single(Service().Check().Entries);

        Assert.Equal(BackupState.Broken, entry.State);
        Assert.Equal(1, Service().Check().BrokenCount);
    }

    [Fact]
    public void Restore_copies_intact_backups_back_and_skips_broken_ones()
    {
        var good = _folder.Touch(_layout.MapStudio("m10_00_00_00"), Bytes(1));
        BackupFiles.WriteWithBackup(good, Bytes(9));
        var broken = _folder.Touch(_layout.MapStudio("m10_01_00_00"), Bytes(5));
        File.WriteAllBytes(BackupFiles.BackupOf(broken), [1, 2, 3]);
        var service = Service();

        var restored = service.Restore(service.Check().Entries);

        Assert.Equal(1, restored);
        Assert.Equal(Bytes(1), File.ReadAllBytes(good));
        Assert.Equal(Bytes(5), File.ReadAllBytes(broken)); // untouched
        Assert.False(service.Check().Entries.Single(e => e.GamePath == good).GameFileChanged);
    }
}
