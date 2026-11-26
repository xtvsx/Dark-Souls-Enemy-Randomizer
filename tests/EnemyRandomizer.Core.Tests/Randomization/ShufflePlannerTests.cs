using EnemyRandomizer.Core.Configuration;
using EnemyRandomizer.Core.Data;
using EnemyRandomizer.Core.Randomization;
using EnemyRandomizer.Core.Settings;

namespace EnemyRandomizer.Core.Tests.Randomization;

public class ShufflePlannerTests
{
    private const string Asylum = "m18_01_00_00";

    private static EnemyDefinition E(int index, string id, EnemyType type, int size = 0) =>
        new(index, id, id, type, "0", size, 0, [], [index * 100], [index * 100], [-1], "");

    private static readonly EnemyDefinition[] Catalog =
    [
        E(0, "c2500", EnemyType.Normal),                // small hollow
        E(1, "c2510", EnemyType.Normal),                // small hollow 2
        E(2, "c1200", EnemyType.Normal, size: 3),       // big normal
        E(3, "c5290", EnemyType.Boss, size: 5),
        E(4, "c5320", EnemyType.Boss, size: 5),
        E(5, "c5350", EnemyType.Boss, size: 5),
        E(6, "c2640", EnemyType.Npc),
    ];

    private static readonly ReplacementTargets Targets = new(
    [
        new("c2500", "Hollow", EnemyType.Normal, 0),
        new("c2510", "Hollow 2", EnemyType.Normal, 0),
        new("c1200", "Big", EnemyType.Normal, 0),
        new("c5290", "Seath", EnemyType.Boss, 0),
        new("c5320", "Gwyndolin", EnemyType.Boss, 0),
        new("c5350", "Gargoyle", EnemyType.Boss, 0),
        new("c2640", "Andre", EnemyType.Npc, 0),
    ]);

    private static RandomizerSettings Settings(params (string Key, int Value)[] overrides)
    {
        var values = new Dictionary<string, int>
        {
            ["BossMode"] = 1, ["NormalMode"] = 2, ["NpcMode"] = 0, ["MimicMode"] = 1, ["FitMode"] = 1,
            ["DifficultyMode"] = 2, ["ReplaceChance"] = 100, ["BossChanceNormals"] = 10, ["BossChanceBosses"] = 90,
            ["GargoyleMode"] = 1, ["DiffStrictness"] = 1, ["TPoseCity"] = 0, ["BossSoulDrops"] = 50, ["PinwheelChaos"] = 1,
            ["TypeReplacement"] = 1, ["GwynNerf"] = 2, ["PreventSame"] = 1, ["UniqueBosses"] = 0, ["RespawningBosses"] = 0,
            ["HostileNpcs"] = 0, ["MosquitoReplacement"] = 1,
            ["RandomizeMode"] = 1, ["ShuffleScope"] = 1, ["ShuffleAsylumEasy"] = 0,
        };
        foreach (var (key, value) in overrides)
        {
            values[key] = value;
        }

        return new RandomizerSettings(values, "seed", "Default");
    }

    private static ReplacementPlanner Planner(IRandomSource random, RandomizerSettings? settings = null)
    {
        var rules = new ReplacementRuleSettings
        {
            UniqueEnemyLimit = new UniqueEnemyLimitSettings { Default = 30 },
            UniqueBossResetCount = 22,
            BannedPairings = [new BannedPairing { Reason = "flying", Originals = ["c5320"], Replacements = ["c5290"] }],
            CreatureRules = [new CreatureRule { Name = "skip", Parts = ["c2500_0099"], Skip = true }],
        };
        var patches = new MapPatchSettings
        {
            EasyAsylum = new EasyAsylumSettings { MapId = Asylum, NormalEnemyIndices = [0], BossIndices = [3], Targets = ["c2510_0000"] },
        };
        var curves = new DifficultyCurveSettings
        {
            MinClass = 0, MaxClass = 7, Offsets = [0],
            Curves = [new() { Chances = [100] }, new() { Chances = [100] }, new() { Chances = [100] }],
        };

        return new ReplacementPlanner(
            EnemyPool.Build(Catalog, GameEdition.Ptde, false, 7),
            Targets,
            new RunOptions(settings ?? Settings()),
            new PlannerSettings(rules, patches, curves, "c2780"),
            random);
    }

    private static ShuffleMap Map(string id, params (string Part, int Size)[] creatures) =>
        new(id, [.. creatures.Select(c => new MapCreature(c.Part, c.Size, -1))]);

    /// <summary>The enemy id standing on each creature after the shuffle (kept creatures keep their own).</summary>
    private static List<string> Placed(MapPlan plan) =>
        [.. plan.Decisions.Select(d => d.Outcome == ReplacementOutcome.Replaced ? d.Enemy!.Id : d.PartName.Split('_')[0])];

    private static List<string> Originals(ShuffleMap map) => [.. map.Creatures.Select(c => c.PartName.Split('_')[0])];

    [Theory]
    [InlineData("a")]
    [InlineData("b")]
    [InlineData("c")]
    [InlineData("d")]
    public void Whole_game_is_a_permutation_of_the_original_enemies(string seed)
    {
        var maps = new[]
        {
            Map("m10", ("c2500_0000", 3), ("c2510_0000", 3), ("c1200_0000", 3), ("c5290_0000", 5), ("c5320_0000", 5), ("c5350_0000", 5)),
            Map("m11", ("c2500_0001", 3), ("c1200_0001", 3), ("c5350_0001", 5), ("c2640_0000", 2), ("o1000_0000", 1)),
        };

        var plans = Planner(new SeededRandom(seed)).PlanShuffle(GameEdition.Ptde, maps);

        var before = maps.SelectMany(Originals).Order().ToList();
        var after = plans.SelectMany(Placed).Order().ToList();
        Assert.Equal(before, after);
        Assert.All(plans.SelectMany(p => p.Decisions).Where(d => d.PartName.StartsWith("c2640", StringComparison.Ordinal)),
            d => Assert.Equal(ReplacementOutcome.ModeOff, d.Outcome));
        Assert.Equal(ReplacementOutcome.NotATarget, plans[1].Decisions[4].Outcome);
    }

    [Fact]
    public void Per_map_scope_never_moves_enemies_between_maps()
    {
        var maps = new[]
        {
            Map("m10", ("c2500_0000", 3), ("c2510_0000", 3)),
            Map("m11", ("c1200_0000", 3), ("c2500_0001", 3)),
        };

        var plans = Planner(new SeededRandom("x"), Settings(("ShuffleScope", 0))).PlanShuffle(GameEdition.Ptde, maps);

        for (var i = 0; i < maps.Length; i++)
        {
            Assert.Equal(Originals(maps[i]).Order(), Placed(plans[i]).Order());
        }
    }

    [Fact]
    public void Enemies_only_go_where_they_fit()
    {
        // The big normal (size 3) fits only its own spot; the two small spots must swap their enemies.
        var map = Map("m10", ("c2500_0000", 0), ("c2510_0000", 0), ("c1200_0000", 3));

        var plan = Planner(new SeededRandom("s"), Settings(("FitMode", 0))).PlanShuffle(GameEdition.Ptde, [map]).Single();

        Assert.Equal(["c2510", "c2500", "c1200"], Placed(plan));
        Assert.Equal(ReplacementOutcome.ShuffleKept, plan.Decisions[2].Outcome);
    }

    [Fact]
    public void Bosses_swap_only_with_bosses()
    {
        var map = Map("m10", ("c2500_0000", 3), ("c5290_0000", 5), ("c5350_0000", 5), ("c2510_0000", 3));

        for (var i = 0; i < 20; i++)
        {
            var plan = Planner(new SeededRandom("seed" + i)).PlanShuffle(GameEdition.Ptde, [map]).Single();
            var placed = Placed(plan);
            Assert.Equal(["c5290", "c5350"], placed.Where(id => id.StartsWith("c5", StringComparison.Ordinal)).Order());
            Assert.StartsWith("c25", placed[0], StringComparison.Ordinal);
            Assert.StartsWith("c25", placed[3], StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Boss_spot_is_pinned_with_one_percent_chance()
    {
        var map = Map("m10", ("c5290_0000", 5), ("c5350_0000", 5), ("c5320_0000", 5));

        // Rolls of the three boss spots: 1 and 1 shuffle, 100 pins the third (99% chance to be shuffled).
        var plan = Planner(new ScriptedRandom(1, 1, 100)).PlanShuffle(GameEdition.Ptde, [map]).Single();

        Assert.Equal(ReplacementOutcome.ShuffleKept, plan.Decisions[2].Outcome);
        Assert.Equal(ReplacementOutcome.Replaced, plan.Decisions[0].Outcome);
        Assert.Equal(ReplacementOutcome.Replaced, plan.Decisions[1].Outcome);
        Assert.Equal(["c5350", "c5290", "c5320"], Placed(plan));
    }

    [Fact]
    public void Banned_pairings_and_skip_rules_are_respected()
    {
        // Seath may not replace Gwyndolin, so the two bosses cannot swap; the skipped hollow is left alone.
        var map = Map("m10", ("c5290_0000", 5), ("c5320_0000", 5), ("c2500_0099", 3), ("c2510_0000", 3));

        var plan = Planner(new SeededRandom("b")).PlanShuffle(GameEdition.Ptde, [map]).Single();

        Assert.Equal(ReplacementOutcome.ShuffleKept, plan.Decisions[0].Outcome);
        Assert.Equal(ReplacementOutcome.ShuffleKept, plan.Decisions[1].Outcome);
        Assert.Equal(ReplacementOutcome.SpecialCase, plan.Decisions[2].Outcome);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("3")]
    [InlineData("4")]
    [InlineData("5")]
    public void Easy_asylum_only_gets_beginner_enemies(string seed)
    {
        var maps = new[]
        {
            Map("m10", ("c2500_0000", 3), ("c2510_0001", 3), ("c1200_0000", 3)),
            Map(Asylum, ("c2510_0000", 3), ("c2500_0001", 3)),
        };

        var plans = Planner(new SeededRandom(seed), Settings(("ShuffleAsylumEasy", 1))).PlanShuffle(GameEdition.Ptde, maps);

        // Only c2500 (index 0) is allowed in the asylum, and the pool is still a permutation.
        Assert.All(Placed(plans[1]), id => Assert.Equal("c2500", id));
        Assert.Equal(maps.SelectMany(Originals).Order(), plans.SelectMany(Placed).Order());
    }

    [Fact]
    public void Same_seed_gives_the_same_layout()
    {
        var maps = new[]
        {
            Map("m10", ("c2500_0000", 3), ("c2510_0000", 3), ("c1200_0000", 3), ("c5290_0000", 5), ("c5350_0000", 5)),
            Map("m11", ("c2500_0001", 3), ("c1200_0001", 3), ("c5350_0001", 5)),
        };

        var first = Planner(new SeededRandom("same")).PlanShuffle(GameEdition.Ptde, maps);
        var second = Planner(new SeededRandom("same")).PlanShuffle(GameEdition.Ptde, maps);

        Assert.Equal(first.SelectMany(Placed), second.SelectMany(Placed));
    }
}
