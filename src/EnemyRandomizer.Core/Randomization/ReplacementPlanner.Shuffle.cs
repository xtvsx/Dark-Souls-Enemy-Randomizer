using EnemyRandomizer.Core.Configuration;
using EnemyRandomizer.Core.Data;

namespace EnemyRandomizer.Core.Randomization;

/// <summary>One map of a shuffle run: its id and original creatures (same row order as the map file).</summary>
public sealed record ShuffleMap(string MapId, IReadOnlyList<MapCreature> Creatures);

/// <summary>
/// Shuffle mode: instead of drawing a random enemy per spot, the enemies that stand in the game are permuted
/// between the spots. Normal spots only get normal enemies and boss spots only bosses; every enemy is used exactly
/// once. Pure logic like the rest of the planner.
/// </summary>
public sealed partial class ReplacementPlanner
{
    /// <summary>A boss spot is shuffled with this chance, otherwise it keeps its boss.</summary>
    public const int ShuffleBossChancePercent = 99;

    /// <summary>A creature that takes part in the shuffle.</summary>
    private sealed record ShuffleSlot(
        int MapIndex, string MapId, int Row, MapCreature Creature, ReplacementTarget Target, EnemyType Type,
        IReadOnlyList<CreatureRule> Rules, EnemyDefinition Original)
    {
        public int MaxSize { get; init; }
    }

    /// <summary>
    /// Plans all <paramref name="maps"/> at once (one pool per map, or one pool for the whole game, depending on
    /// <see cref="RunOptions.ShuffleWholeGame"/>). The result has one plan per map, in the same order.
    /// </summary>
    public IReadOnlyList<MapPlan> PlanShuffle(GameEdition edition, IReadOnlyList<ShuffleMap> maps)
    {
        ArgumentNullException.ThrowIfNull(maps);
        var decisions = maps.Select(m => new CreatureDecision?[m.Creatures.Count]).ToArray();
        var slots = CollectSlots(edition, maps, decisions);

        var groups = _options.ShuffleWholeGame
            ? [slots]
            : slots.GroupBy(s => s.MapIndex).Select(g => g.ToList()).ToList();

        foreach (var group in groups)
        {
            foreach (var type in new[] { EnemyType.Boss, EnemyType.Normal })
            {
                ShuffleGroup(edition, maps, [.. group.Where(s => s.Type == type)], type, decisions);
            }
        }

        return [.. maps.Select((m, i) => new MapPlan(
            m.MapId, _settings.Rules.UniqueEnemyLimit.For(m.MapId, edition), [.. decisions[i].Select(d => d!)]))];
    }

    private List<ShuffleSlot> CollectSlots(GameEdition edition, IReadOnlyList<ShuffleMap> maps, CreatureDecision?[][] decisions)
    {
        var catalog = _pool.Catalog.GroupBy(e => e.Id).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var slots = new List<ShuffleSlot>();
        for (var mapIndex = 0; mapIndex < maps.Count; mapIndex++)
        {
            var mapId = maps[mapIndex].MapId;
            for (var row = 0; row < maps[mapIndex].Creatures.Count; row++)
            {
                var creature = maps[mapIndex].Creatures[row];
                var part = creature.PartName;
                var target = _targets.Find(part);
                if (target is null)
                {
                    decisions[mapIndex][row] = new CreatureDecision(row, part, "", ReplacementOutcome.NotATarget);
                    continue;
                }

                var rules = _settings.Rules.CreatureRules.Where(r => r.Matches(mapId, part, _options.Value)).ToList();
                if (rules.Any(r => r.Skip))
                {
                    decisions[mapIndex][row] = new CreatureDecision(row, part, target.Name, ReplacementOutcome.SpecialCase);
                    continue;
                }

                var type = ReclassifiedType(rules) ?? target.Type;
                var fixedOutcome = FixedOutcome(part, type);
                if (fixedOutcome is null && (!catalog.TryGetValue(target.Id, out var original) || !original.IsEnabledOn(edition)))
                {
                    fixedOutcome = ReplacementOutcome.SpecialCase; // not an enemy that may be moved in this edition
                }

                if (fixedOutcome is { } outcome)
                {
                    decisions[mapIndex][row] = new CreatureDecision(row, part, target.Name, outcome) { OriginalType = type };
                    continue;
                }

                var enemy = catalog[target.Id];
                var maxSize = _options.FitMode == 0 || rules.Any(r => r.KeepOriginalSize) ? creature.Size : EnemyPool.SizeClasses - 1;
                slots.Add(new ShuffleSlot(mapIndex, mapId, row, creature, target, type, rules, enemy) { MaxSize = maxSize });
            }
        }

        return slots;
    }

    /// <summary>Spots that never take part: NPCs, replace mode "off" and kept mimics.</summary>
    private ReplacementOutcome? FixedOutcome(string part, EnemyType type)
    {
        switch (type)
        {
            case EnemyType.Boss:
                return _options.BossMode == ReplaceMode.Off ? ReplacementOutcome.ModeOff : null;
            case EnemyType.Normal:
                if (part.Contains(_settings.MimicModelId, StringComparison.Ordinal) && !_options.ReplaceMimics)
                {
                    return ReplacementOutcome.MimicKept;
                }

                return _options.NormalMode == ReplaceMode.Off ? ReplacementOutcome.ModeOff : null;
            default:
                return ReplacementOutcome.ModeOff; // NPCs keep their place (their talk and event scripts depend on it)
        }
    }

    private void ShuffleGroup(
        GameEdition edition, IReadOnlyList<ShuffleMap> maps, List<ShuffleSlot> all, EnemyType type, CreatureDecision?[][] decisions)
    {
        var active = new List<ShuffleSlot>();
        foreach (var slot in all)
        {
            if (type == EnemyType.Boss && _random.Between(1, 100) > ShuffleBossChancePercent)
            {
                decisions[slot.MapIndex][slot.Row] = Kept(slot);
            }
            else
            {
                active.Add(slot);
            }
        }

        if (active.Count == 0)
        {
            return;
        }

        var assigned = Match(active, type);
        for (var s = 0; s < active.Count; s++)
        {
            var slot = active[s];
            if (assigned[s] == s)
            {
                decisions[slot.MapIndex][slot.Row] = Kept(slot);
                continue;
            }

            _map = new MapState(slot.MapId);
            decisions[slot.MapIndex][slot.Row] =
                Place(slot.Row, slot.Creature, slot.Target, slot.Type, slot.Rules, active[assigned[s]].Original, edition);
        }
    }

    private static CreatureDecision Kept(ShuffleSlot slot) =>
        new(slot.Row, slot.Creature.PartName, slot.Target.Name, ReplacementOutcome.ShuffleKept) { OriginalType = slot.Type };

    /// <summary>
    /// Assigns every slot one enemy of the group (result: slot index → index of the slot the enemy came from).
    /// Bipartite matching (augmenting paths) over the allowed pairs. Enemies are first moved away from their own
    /// spot; a spot only gets its own enemy back when nothing else works, which keeps the result a permutation.
    /// </summary>
    private int[] Match(List<ShuffleSlot> slots, EnemyType type)
    {
        var count = slots.Count;
        var banned = new BannedPairingRule(_settings.Rules.BannedPairings, _options.ShuffleAsylumEasy);
        var easy = _settings.Patches.EasyAsylum;
        var easyAllowed = (type == EnemyType.Boss ? easy.BossIndices : easy.NormalEnemyIndices).ToHashSet();

        var accepted = new Dictionary<(int Slot, int Enemy), bool>();
        bool Accepts(int slot, int enemyIndex)
        {
            if (!accepted.TryGetValue((slot, enemyIndex), out var ok))
            {
                var spot = slots[slot];
                var enemy = _pool[enemyIndex];
                ok = enemy.Size <= spot.MaxSize
                     && !banned.Rejects(spot.Creature.PartName, enemy)
                     && !(_options.ShuffleAsylumEasy && IsAsylum(spot) && !easyAllowed.Contains(enemyIndex));
                accepted[(slot, enemyIndex)] = ok;
            }

            return ok;
        }

        var adjacency = new List<int>[count];
        for (var s = 0; s < count; s++)
        {
            adjacency[s] = [.. Enumerable.Range(0, count).Where(i => i != s && Accepts(s, slots[i].Original.Index))];
            Shuffle(adjacency[s]);
        }

        var owner = Enumerable.Repeat(-1, count).ToArray();   // enemy -> slot
        var assigned = Enumerable.Repeat(-1, count).ToArray(); // slot -> enemy
        var visited = new int[count];
        var stamp = 0;

        bool TryAssign(int slot)
        {
            foreach (var enemy in adjacency[slot])
            {
                if (visited[enemy] == stamp)
                {
                    continue;
                }

                visited[enemy] = stamp;
                if (owner[enemy] < 0 || TryAssign(owner[enemy]))
                {
                    owner[enemy] = slot;
                    assigned[slot] = enemy;
                    return true;
                }
            }

            return false;
        }

        // The most constrained spots first; ties in random order.
        var order = Enumerable.Range(0, count).ToList();
        Shuffle(order);
        foreach (var slot in order.OrderBy(s => adjacency[s].Count).ToList())
        {
            stamp++;
            TryAssign(slot);
        }

        // Spots left over may keep their own enemy (always a valid permutation, so this ends with a full match).
        for (var s = 0; s < count; s++)
        {
            adjacency[s].Add(s);
        }

        foreach (var slot in Enumerable.Range(0, count).Where(s => assigned[s] < 0).ToList())
        {
            stamp++;
            TryAssign(slot);
        }

        return assigned;
    }

    private bool IsAsylum(ShuffleSlot slot) => slot.MapId == _settings.Patches.EasyAsylum.MapId;

    private void Shuffle(List<int> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = _random.Between(0, i);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
