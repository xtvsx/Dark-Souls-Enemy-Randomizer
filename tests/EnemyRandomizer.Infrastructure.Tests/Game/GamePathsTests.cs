using EnemyRandomizer.Core.Configuration;
using EnemyRandomizer.Infrastructure.Game;

namespace EnemyRandomizer.Infrastructure.Tests.Game;

public class GamePathsTests
{
    [Fact]
    public void DataFolderStaysNextToTheExeWhenAnotherGameFolderIsChosen()
    {
        var exe = Path.Combine(Path.GetTempPath(), "er-exe");
        var game = Path.Combine(Path.GetTempPath(), "er-game");
        var settings = new AppSettings { GameDirectory = game, DataFolder = "enemyRandomizerData" };

        var paths = new GamePaths(settings, exe);

        Assert.Equal(Path.GetFullPath(game), paths.GameDirectory);
        Assert.Equal(Path.Combine(Path.GetFullPath(exe), "enemyRandomizerData"), paths.DataDirectory);
        Assert.StartsWith(Path.GetFullPath(game), paths.InGame("map/MapStudio/x.msb"), StringComparison.OrdinalIgnoreCase);
    }
}
