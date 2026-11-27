namespace EnemyRandomizer.Core.Abstractions;

/// <summary>Condition of one backed-up game file.</summary>
public enum BackupState
{
    /// <summary>The backup exists and can be restored.</summary>
    Intact,

    /// <summary>The backup exists but is too small to be a real copy (interrupted copy); restoring it would break the game.</summary>
    Broken,
}

/// <summary>One game file that has a <c>.bak</c> copy.</summary>
/// <param name="Group">Kind of file for grouping in the UI (maps, events, AI scripts, effects).</param>
/// <param name="Name">Short name, e.g. the map or effect file name.</param>
/// <param name="GamePath">Absolute path of the game file the backup belongs to.</param>
/// <param name="State">Whether the backup is usable.</param>
/// <param name="GameFileChanged">True when the game file currently differs from its backup (randomized).</param>
/// <param name="BackupModified">When the backup was made.</param>
public sealed record BackupEntry(
    string Group,
    string Name,
    string GamePath,
    BackupState State,
    bool GameFileChanged,
    DateTime BackupModified);

/// <summary>Overview of every backup the randomizer made in the game folder.</summary>
/// <param name="Entries">Files that have a backup.</param>
/// <param name="FilesWithoutBackup">Candidate game files without a backup (never changed by the randomizer).</param>
public sealed record BackupReport(IReadOnlyList<BackupEntry> Entries, int FilesWithoutBackup)
{
    public int BrokenCount => Entries.Count(e => e.State == BackupState.Broken);

    public int ChangedCount => Entries.Count(e => e.GameFileChanged);
}

/// <summary>Lists and restores the <c>.bak</c> files next to the game files.</summary>
public interface IBackupService
{
    /// <summary>Checks every game file the randomizer can change. Returns an empty report when no game was found.</summary>
    BackupReport Check();

    /// <summary>Copies the backups of <paramref name="entries"/> over the game files. Broken backups are skipped. Returns the number of files restored.</summary>
    int Restore(IEnumerable<BackupEntry> entries);
}
