using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using EnemyRandomizer.Core.Abstractions;
using EnemyRandomizer.Core.Configuration;
using Microsoft.Extensions.Options;

namespace EnemyRandomizer.Infrastructure.Game;

/// <summary>
/// The backups of the game files, kept in one folder (<see cref="IGamePaths.BackupDirectory"/>) instead of next to the files:
/// <code>
/// backups/initial/&lt;path relative to the game folder&gt;     the game as it was before the randomizer, never overwritten
/// backups/2026-11-27_17-40-12-123/&lt;path&gt;                   the files as they were before one randomization (snapshot)
/// </code>
/// Every set has a <c>backup.json</c> with its kind and creation time. Backups of v0.4.x (<c>&lt;file&gt;.bak</c> next to the
/// game file) are moved into <c>initial</c> the first time a file is touched (or in bulk by <see cref="MigrateLegacy(IEnumerable{string})"/>).
/// </summary>
public sealed partial class GameBackups(IGamePaths paths, IOptions<AppSettings> settings)
{
    public const string InitialId = "initial";
    public const string ManifestName = "backup.json";

    private const string LegacyExtension = ".bak";
    private const int MinimumValidBytes = 10;
    private const int CompareBufferSize = 81920;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private string InitialDirectory => Path.Combine(paths.BackupDirectory, InitialId);

    /// <summary>Where the original of <paramref name="gameFile"/> is kept (it may not exist yet).</summary>
    public string BackupOf(string gameFile) => InSet(InitialDirectory, gameFile);

    /// <summary>A copy counts as broken below 10 bytes (legacy check for interrupted copies).</summary>
    public static bool IsValidCopy(string file) => File.Exists(file) && new FileInfo(file).Length >= MinimumValidBytes;

    /// <summary>True when the original of the game file is backed up.</summary>
    public bool HasBackup(string gameFile)
    {
        MigrateLegacy(gameFile);
        return File.Exists(BackupOf(gameFile));
    }

    /// <summary>The unmodified version of a game file: its backup when one exists, else the file itself.</summary>
    public string Original(string gameFile)
    {
        MigrateLegacy(gameFile);
        var backup = BackupOf(gameFile);
        return File.Exists(backup) ? backup : gameFile;
    }

    /// <summary>Creates the backup once; never overwrites an existing one (it holds the original).</summary>
    public void EnsureBackup(string gameFile)
    {
        MigrateLegacy(gameFile);
        var backup = BackupOf(gameFile);
        if (File.Exists(backup) || !File.Exists(gameFile))
        {
            return;
        }

        CopyAtomically(gameFile, backup);
        EnsureManifest(InitialDirectory, BackupKind.Initial, DateTime.UtcNow);
    }

    /// <summary>Writes <paramref name="data"/> after making sure the original is backed up.</summary>
    public void WriteWithBackup(string gameFile, byte[] data)
    {
        EnsureBackup(gameFile);
        AtomicFile.WriteAllBytes(gameFile, data);
    }

    /// <summary>Writes the backup itself (used to repair backups made by v0.3.2).</summary>
    public void WriteBackup(string gameFile, byte[] data)
    {
        var backup = BackupOf(gameFile);
        Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
        AtomicFile.WriteAllBytes(backup, data);
        EnsureManifest(InitialDirectory, BackupKind.Initial, DateTime.UtcNow);
    }

    /// <summary>Copies the original over the game file. Returns false when there is no usable backup (a damaged copy is never restored).</summary>
    public bool Restore(string gameFile)
    {
        MigrateLegacy(gameFile);
        var backup = BackupOf(gameFile);
        if (!IsValidCopy(backup))
        {
            return false;
        }

        File.Copy(backup, gameFile, overwrite: true);
        return true;
    }

    /// <summary>Moves v0.4.x <c>.bak</c> files of the given game files into the initial backup. Returns how many were moved.</summary>
    public int MigrateLegacy(IEnumerable<string> gameFiles)
    {
        ArgumentNullException.ThrowIfNull(gameFiles);
        return gameFiles.Count(MigrateLegacy);
    }

    /// <summary>True when a legacy backup was moved.</summary>
    private bool MigrateLegacy(string gameFile)
    {
        var legacy = gameFile + LegacyExtension;
        if (!File.Exists(legacy))
        {
            return false;
        }

        var backup = BackupOf(gameFile);
        if (!File.Exists(backup))
        {
            CopyAtomically(legacy, backup);
            EnsureManifest(InitialDirectory, BackupKind.Initial, File.GetLastWriteTimeUtc(legacy));
        }

        // The legacy file is only removed once the new copy is verified, so a failed move never loses the original.
        if (new FileInfo(backup).Length == new FileInfo(legacy).Length)
        {
            try
            {
                File.Delete(legacy);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Not removable (read-only, in use): the backup is safe in the new place, the .bak is just left over.
            }
        }

        return true;
    }

    /// <summary>
    /// Keeps the current content of <paramref name="gameFiles"/> as a snapshot, but only when the game is randomized
    /// (a file differs from its initial backup); otherwise the initial backup already is that state.
    /// Returns the snapshot id, or <c>null</c> when none was needed or snapshots are switched off.
    /// </summary>
    public string? CreateSnapshot(IEnumerable<string> gameFiles)
    {
        ArgumentNullException.ThrowIfNull(gameFiles);
        if (settings.Value.MaxBackupSnapshots <= 0)
        {
            return null;
        }

        var files = gameFiles.Where(File.Exists).ToList();
        if (!files.Any(f => HasBackup(f) && !SameContent(f, BackupOf(f))))
        {
            return null;
        }

        var id = NewSnapshotId();
        var directory = Path.Combine(paths.BackupDirectory, id);
        try
        {
            foreach (var file in files)
            {
                CopyAtomically(file, InSet(directory, file));
            }

            EnsureManifest(directory, BackupKind.Snapshot, DateTime.UtcNow);
        }
        catch
        {
            TryDelete(directory);
            throw;
        }

        Prune(settings.Value.MaxBackupSnapshots);
        return id;
    }

    /// <summary>Ids of the snapshots, newest first.</summary>
    public IReadOnlyList<string> SnapshotIds() =>
        !Directory.Exists(paths.BackupDirectory)
            ? []
            : [.. Directory.EnumerateDirectories(paths.BackupDirectory)
                .Select(d => Path.GetFileName(d))
                .Where(IsSnapshotId)
                .OrderByDescending(id => id, StringComparer.Ordinal)];

    public bool InitialExists => Directory.Exists(InitialDirectory) && FilesOf(InitialId).Any();

    public string SetDirectory(string id) =>
        id == InitialId ? InitialDirectory : Path.Combine(paths.BackupDirectory, IsSnapshotId(id) ? id : throw new ArgumentException("Not a backup id.", nameof(id)));

    /// <summary>The backed-up files of a set as absolute paths inside the set folder.</summary>
    public IEnumerable<string> FilesOf(string id)
    {
        var directory = SetDirectory(id);
        return !Directory.Exists(directory)
            ? []
            : Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .Where(f => !string.Equals(Path.GetFileName(f), ManifestName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The game file a backed-up file belongs to.</summary>
    public string GameFileOf(string id, string backupFile) =>
        Path.GetFullPath(Path.Combine(paths.GameDirectory, Path.GetRelativePath(SetDirectory(id), backupFile)));

    public DateTime CreatedOf(string id)
    {
        var manifest = Path.Combine(SetDirectory(id), ManifestName);
        try
        {
            if (File.Exists(manifest) && JsonSerializer.Deserialize<BackupManifest>(File.ReadAllText(manifest), JsonOptions) is { } read)
            {
                return read.CreatedUtc.ToLocalTime();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // A damaged manifest only costs the exact time; fall back to the folder time.
        }

        return Directory.GetCreationTime(SetDirectory(id));
    }

    /// <summary>Copies the files of a snapshot back into the game. Returns how many were restored.</summary>
    public int RestoreSnapshot(string id)
    {
        if (!IsSnapshotId(id))
        {
            return 0;
        }

        var restored = 0;
        foreach (var backup in FilesOf(id).Where(IsValidCopy))
        {
            var target = GameFileOf(id, backup);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(backup, target, overwrite: true);
            restored++;
        }

        return restored;
    }

    /// <summary>Deletes a snapshot folder; the initial backup is protected.</summary>
    public bool DeleteSnapshot(string id)
    {
        if (!IsSnapshotId(id) || !Directory.Exists(SetDirectory(id)))
        {
            return false;
        }

        Directory.Delete(SetDirectory(id), recursive: true);
        return true;
    }

    public static bool SameContent(string a, string b)
    {
        var first = new FileInfo(a);
        var second = new FileInfo(b);
        if (!first.Exists || !second.Exists || first.Length != second.Length)
        {
            return false;
        }

        using var streamA = new FileStream(a, FileMode.Open, FileAccess.Read, FileShare.Read, CompareBufferSize);
        using var streamB = new FileStream(b, FileMode.Open, FileAccess.Read, FileShare.Read, CompareBufferSize);
        var bufferA = new byte[CompareBufferSize];
        var bufferB = new byte[CompareBufferSize];
        int read;
        while ((read = streamA.Read(bufferA, 0, bufferA.Length)) > 0)
        {
            streamB.ReadExactly(bufferB, 0, read);
            if (!bufferA.AsSpan(0, read).SequenceEqual(bufferB.AsSpan(0, read)))
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsSnapshotId(string id) => SnapshotIdPattern().IsMatch(id);

    /// <summary>
    /// A timestamp id that always sorts after every existing snapshot (ids are ordered by name, so a reused or older id would
    /// make a new snapshot look old, e.g. after the clock was set back or after pruning).
    /// </summary>
    private string NewSnapshotId()
    {
        var baseId = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff", CultureInfo.InvariantCulture);
        var existing = SnapshotIds();
        var latest = existing.Count > 0 ? existing[0] : null;
        if (latest is not null && string.CompareOrdinal(baseId, latest) <= 0)
        {
            baseId = latest;
        }

        var id = baseId;
        for (var n = 2; Directory.Exists(Path.Combine(paths.BackupDirectory, id)) || (latest is not null && string.CompareOrdinal(id, latest) <= 0); n++)
        {
            id = $"{baseId}_{n}";
        }

        return id;
    }

    private void Prune(int keep)
    {
        foreach (var old in SnapshotIds().Skip(Math.Max(keep, 1)))
        {
            TryDelete(SetDirectory(old));
        }
    }

    private static void TryDelete(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Leftover folders are harmless; they show up (or get pruned) next time.
        }
    }

    private string InSet(string setDirectory, string gameFile)
    {
        var relative = Path.GetRelativePath(paths.GameDirectory, gameFile);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            throw new ArgumentException($"'{gameFile}' is not inside the game folder.", nameof(gameFile));
        }

        return Path.Combine(setDirectory, relative);
    }

    /// <summary>Copies through a temp file so an interrupted copy never leaves a truncated "original" behind.</summary>
    private static void CopyAtomically(string source, string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var temp = target + ".tmp";
        File.Copy(source, temp, overwrite: true);
        File.Move(temp, target, overwrite: true);
    }

    private static void EnsureManifest(string setDirectory, BackupKind kind, DateTime createdUtc)
    {
        var manifest = Path.Combine(setDirectory, ManifestName);
        if (File.Exists(manifest))
        {
            return;
        }

        Directory.CreateDirectory(setDirectory);
        AtomicFile.WriteAllText(manifest, JsonSerializer.Serialize(new BackupManifest(kind.ToString(), createdUtc.ToUniversalTime()), JsonOptions));
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}(-\d{3})?(_\d+)*$")]
    private static partial Regex SnapshotIdPattern();

    private sealed record BackupManifest(string Kind, DateTime CreatedUtc);
}
