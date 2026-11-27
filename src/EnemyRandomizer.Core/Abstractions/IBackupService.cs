namespace EnemyRandomizer.Core.Abstractions;

/// <summary>What a backup set is.</summary>
public enum BackupKind
{
    /// <summary>The game as it was before the randomizer touched it. Made once, never removed.</summary>
    Initial,

    /// <summary>The game files as they were right before one randomization (what the previous randomization left behind).</summary>
    Snapshot,
}

/// <summary>Whether the game files differ from the original game.</summary>
public enum GameFilesState
{
    /// <summary>Every file the randomizer changes is as in the original game.</summary>
    Original,

    /// <summary>At least one file differs from the original game.</summary>
    Randomized,
}

/// <summary>One restore point.</summary>
/// <param name="Id">Folder name inside the backup directory (<c>initial</c> or a timestamp).</param>
/// <param name="Kind">Initial state or snapshot.</param>
/// <param name="Created">When the backup was made.</param>
/// <param name="FileCount">Number of backed-up files.</param>
/// <param name="Bytes">Total size of the backed-up files.</param>
/// <param name="IsCurrent">The game files are right now as in this backup.</param>
/// <param name="IsBroken">At least one backed-up file is too small to be a real copy (interrupted copy).</param>
public sealed record BackupSet(string Id, BackupKind Kind, DateTime Created, int FileCount, long Bytes, bool IsCurrent, bool IsBroken);

/// <summary>Overview of the backups; the first set is the initial state when it exists, snapshots follow newest first.</summary>
public sealed record BackupReport(IReadOnlyList<BackupSet> Sets, GameFilesState GameState, string Directory);

/// <summary>Lists, restores and deletes the backups the randomizer keeps in its backup directory.</summary>
public interface IBackupService
{
    /// <summary>The backup directory (it may not exist before the first randomization).</summary>
    string BackupDirectory { get; }

    /// <summary>Looks at every backup and at the game files. Slow with many files, call it off the UI thread.</summary>
    BackupReport Check();

    /// <summary>
    /// Puts a backup set back into the game. The initial set is a full "Revert to normal" (<paramref name="restoreEffects"/>
    /// decides whether effect files and the exe are restored too); a snapshot copies its files back. Returns the number of files restored.
    /// </summary>
    int Restore(string id, bool restoreEffects);

    /// <summary>Deletes a snapshot. The initial set cannot be deleted; returns false for it and for unknown ids.</summary>
    bool Delete(string id);
}
