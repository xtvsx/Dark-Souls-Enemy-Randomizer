using EnemyRandomizer.Core;
using EnemyRandomizer.Core.Abstractions;
using EnemyRandomizer.Core.Configuration;
using EnemyRandomizer.Infrastructure.Game;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EnemyRandomizer.Infrastructure.Setup;

/// <summary>
/// Overview and selective restore of the <c>.bak</c> files (see <see cref="BackupFiles"/>). It looks at exactly
/// the files <see cref="GameRestorer"/> reverts, so "restore all" and "Revert to normal" agree.
/// </summary>
public sealed partial class BackupService(
    IGamePaths paths,
    IGameInstallation installation,
    GameFileLayout layout,
    IOptions<GameDataSettings> gameData,
    ILogger<BackupService> logger) : IBackupService
{
    public BackupReport Check()
    {
        if (installation.Edition is not { } edition)
        {
            return new BackupReport([], 0);
        }

        var entries = new List<BackupEntry>();
        var without = 0;
        foreach (var (group, name, relativePath) in Candidates(edition))
        {
            var file = paths.InGame(relativePath);
            if (BackupFiles.BackupOf(file) is var backup && !File.Exists(backup))
            {
                without++;
                continue;
            }

            entries.Add(new BackupEntry(group, name, file, StateOf(file), IsChanged(file), File.GetLastWriteTime(backup)));
        }

        return new BackupReport(entries, without);
    }

    public int Restore(IEnumerable<BackupEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var restored = 0;
        foreach (var entry in entries.Where(e => e.State == BackupState.Intact))
        {
            if (BackupFiles.Restore(entry.GamePath))
            {
                restored++;
            }
        }

        LogRestored(logger, restored);
        return restored;
    }

    private IEnumerable<(string Group, string Name, string RelativePath)> Candidates(GameEdition edition)
    {
        foreach (var map in layout.Maps)
        {
            yield return ("Maps", map.Name, layout.MapStudio(map.Id));
            yield return ("Events", map.Name, layout.Event(map.Id, edition));
            if (map.Randomized)
            {
                yield return ("AI scripts", map.Name, layout.AiScript(map.Id, edition));
            }
        }

        foreach (var effect in gameData.Value.FfxFiles)
        {
            yield return ("Effects", effect, layout.Sfx(effect, edition));
        }
    }

    private static BackupState StateOf(string file) =>
        BackupFiles.IsValidCopy(BackupFiles.BackupOf(file)) ? BackupState.Intact : BackupState.Broken;

    /// <summary>True when the game file is missing or its bytes differ from the backup.</summary>
    private static bool IsChanged(string file)
    {
        var backup = BackupFiles.BackupOf(file);
        if (!File.Exists(file))
        {
            return true;
        }

        return new FileInfo(file).Length != new FileInfo(backup).Length
               || !File.ReadAllBytes(file).AsSpan().SequenceEqual(File.ReadAllBytes(backup));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Restored {Files} files from their backups")]
    private static partial void LogRestored(ILogger logger, int files);
}
