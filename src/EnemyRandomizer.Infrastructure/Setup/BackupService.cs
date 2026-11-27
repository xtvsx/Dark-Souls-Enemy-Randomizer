using EnemyRandomizer.Core;
using EnemyRandomizer.Core.Abstractions;
using EnemyRandomizer.Infrastructure.Game;
using Microsoft.Extensions.Logging;

namespace EnemyRandomizer.Infrastructure.Setup;

/// <summary>
/// The restore points the randomizer keeps (see <see cref="GameBackups"/>): the initial state of the game and one snapshot
/// per earlier randomization. Restoring the initial state is the same operation as "Revert to normal".
/// </summary>
public sealed partial class BackupService(
    IGamePaths paths,
    IGameInstallation installation,
    GameFileLayout layout,
    GameBackups backups,
    GameRestorer restorer,
    ILogger<BackupService> logger) : IBackupService
{
    public string BackupDirectory => paths.BackupDirectory;

    public BackupReport Check()
    {
        if (installation.Edition is not { } edition)
        {
            return new BackupReport([], GameFilesState.Original, BackupDirectory);
        }

        // Backups of v0.4.x next to the game files are moved into the backup folder first, so they show up here.
        backups.MigrateLegacy(layout.MapFiles(edition).Concat(layout.EffectFiles(edition)).Select(paths.InGame));

        var state = layout.MapFiles(edition).Select(paths.InGame)
            .Any(f => backups.HasBackup(f) && !GameBackups.SameContent(f, backups.BackupOf(f)))
            ? GameFilesState.Randomized
            : GameFilesState.Original;

        var sets = new List<BackupSet>();
        if (backups.InitialExists)
        {
            sets.Add(Describe(GameBackups.InitialId, BackupKind.Initial, state == GameFilesState.Original, backups.FilesOf(GameBackups.InitialId).ToList()));
        }

        foreach (var id in backups.SnapshotIds())
        {
            var files = backups.FilesOf(id).ToList();
            var isCurrent = files.Count > 0 && files.All(f => GameBackups.SameContent(backups.GameFileOf(id, f), f));
            sets.Add(Describe(id, BackupKind.Snapshot, isCurrent, files));
        }

        return new BackupReport(sets, state, BackupDirectory);
    }

    public int Restore(string id, bool restoreEffects)
    {
        int restored;
        if (id == GameBackups.InitialId)
        {
            restored = installation.Edition is { } edition
                ? restorer.Revert(edition, restoreEffects, new Progress<OperationProgress>(), CancellationToken.None).FilesRestored
                : 0;
        }
        else
        {
            restored = backups.RestoreSnapshot(id);
        }

        LogRestored(logger, id, restored);
        return restored;
    }

    public bool Delete(string id) => id != GameBackups.InitialId && backups.DeleteSnapshot(id);

    private BackupSet Describe(string id, BackupKind kind, bool isCurrent, List<string> files) =>
        new(id, kind, backups.CreatedOf(id), files.Count, files.Sum(f => new FileInfo(f).Length), isCurrent, files.Any(f => !GameBackups.IsValidCopy(f)));

    [LoggerMessage(Level = LogLevel.Information, Message = "Restored backup {Id}: {Files} files")]
    private static partial void LogRestored(ILogger logger, string id, int files);
}
