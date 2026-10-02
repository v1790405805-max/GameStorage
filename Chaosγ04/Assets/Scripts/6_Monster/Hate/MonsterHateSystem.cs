using System.Collections.Generic;
using UnityEngine;

public enum MonsterTargetKind
{
    Player,
    Monster
}

public sealed class MonsterTarget
{
    public MonsterTargetKind Kind { get; }
    public CellManager Cell { get; }
    public MonsterIdentityManager Monster { get; }

    public bool IsPlayer => Kind == MonsterTargetKind.Player;

    public string Key =>
        IsPlayer
            ? MonsterHateSystem.PlayerTargetKey
            : (Monster != null && !string.IsNullOrEmpty(Monster.monsterId)
                ? Monster.monsterId
                : Monster != null
                    ? Monster.GetInstanceID().ToString()
                    : "Unknown");

    public string DisplayName =>
        IsPlayer
            ? "Player"
            : (Monster != null ? Monster.monsterId : "Unknown Monster");

    public MonsterTarget(MonsterTargetKind kind, CellManager cell, MonsterIdentityManager monster)
    {
        Kind = kind;
        Cell = cell;
        Monster = monster;
    }
}

public sealed class MonsterHateDebugEntry
{
    public string TargetKey { get; }
    public string DisplayName { get; }
    public int Distance { get; }
    public int BaselineHate { get; }
    public int HitHate { get; }
    public int ProximityHate { get; }

    public bool HasDistance => Distance != int.MaxValue;
    public int TotalHate => BaselineHate + HitHate + ProximityHate;

    public MonsterHateDebugEntry(
        string targetKey,
        string displayName,
        int distance,
        int baselineHate,
        int hitHate,
        int proximityHate)
    {
        TargetKey = targetKey;
        DisplayName = displayName;
        Distance = distance;
        BaselineHate = baselineHate;
        HitHate = hitHate;
        ProximityHate = proximityHate;
    }
}

/// <summary>
/// Per-monster aggro state. A target score is:
/// baseline hate (initial faction hate)
/// + hit hate (accumulated from being attacked)
/// + current proximity hate (+3 at distance 1, +2 at distance 2, +1 at distance 3).
/// </summary>
[DisallowMultipleComponent]
public sealed class MonsterHateSystem : MonoBehaviour
{
    public const string PlayerTargetKey = "Player";

    private readonly Dictionary<string, int> baselineHateScores = new Dictionary<string, int>();
    private readonly Dictionary<string, int> hitHateScores = new Dictionary<string, int>();
    private MonsterIdentityManager selfIdentity;
    private bool baselineApplied;

    private void Awake()
    {
        selfIdentity = GetComponent<MonsterIdentityManager>();
    }

    public static MonsterHateSystem EnsureOn(MonsterIdentityManager monster)
    {
        if (monster == null)
        {
            return null;
        }

        MonsterHateSystem system = monster.GetComponent<MonsterHateSystem>();
        if (system == null)
        {
            system = monster.gameObject.AddComponent<MonsterHateSystem>();
        }

        return system;
    }

    public void RegisterHit(MonsterIdentityManager attacker)
    {
        if (attacker != null && selfIdentity != null && selfIdentity.IsHostileTo(attacker))
        {
            AddHitHate(GetMonsterKey(attacker), 5);
        }
        else
        {
            AddHitHate(PlayerTargetKey, 5);
        }
    }

    public void AddBaselineHateForPlayer(int amount)
    {
        AddBaselineHate(PlayerTargetKey, amount);
    }

    public List<MonsterTarget> GetHostileTargets(GridManager gridManager)
    {
        List<MonsterTarget> targets = new List<MonsterTarget>();
        if (selfIdentity == null || gridManager == null)
        {
            return targets;
        }

        EnsureBaselineHate();

        MonsterIdentitySystem monsterSystem = MonsterIdentitySystem.Instance;
        if (monsterSystem != null)
        {
            foreach (MonsterIdentityManager other in monsterSystem.GetAllMonsters())
            {
                if (other == null ||
                    other == selfIdentity ||
                    !other.gameObject.activeInHierarchy ||
                    !selfIdentity.IsHostileTo(other))
                {
                    continue;
                }

                CellManager cell = MonsterPathfinding.FindMonsterCell(gridManager, other);
                if (cell != null)
                {
                    targets.Add(new MonsterTarget(MonsterTargetKind.Monster, cell, other));
                }
            }
        }

        if (selfIdentity.IsHostileToPlayer())
        {
            CellManager playerCell = MonsterPathfinding.FindPlayerCell(gridManager);
            if (playerCell != null)
            {
                targets.Add(new MonsterTarget(MonsterTargetKind.Player, playerCell, null));
            }
        }

        return targets;
    }

    public bool TrySelectTarget(
        GridManager gridManager,
        int maxPathDistance,
        bool includePlayer,
        out MonsterTarget selectedTarget,
        out int selectedDistance,
        out int selectedHate,
        bool ignoreUnitBlockers = false)
    {
        selectedTarget = null;
        selectedDistance = int.MaxValue;
        selectedHate = 0;

        if (selfIdentity == null || gridManager == null || maxPathDistance < 0)
        {
            return false;
        }

        gridManager.EnsureGridSystemInitialized();

        CellManager selfCell = MonsterPathfinding.FindMonsterCell(gridManager, selfIdentity);
        if (selfCell == null)
        {
            return false;
        }

        int bestHate = int.MinValue;
        int bestDistance = int.MaxValue;

        foreach (MonsterTarget target in GetHostileTargets(gridManager))
        {
            if (target.IsPlayer && !includePlayer)
            {
                continue;
            }

            int distance = MonsterPathfinding.GetPathDistance(
                gridManager,
                selfIdentity,
                selfCell,
                target.Cell,
                ignoreUnitBlockers: ignoreUnitBlockers);

            if (distance == int.MaxValue || distance > maxPathDistance)
            {
                continue;
            }

            int hate = GetTotalHate(target, distance);

            bool better =
                hate > bestHate ||
                (hate == bestHate && distance < bestDistance);

            if (better)
            {
                bestHate = hate;
                bestDistance = distance;
                selectedTarget = target;
                selectedDistance = distance;
                selectedHate = hate;
            }
        }

        return selectedTarget != null;
    }

    public int GetBaselineHate(MonsterTarget target)
    {
        if (target == null)
        {
            return 0;
        }

        return baselineHateScores.TryGetValue(target.Key, out int hate) ? hate : 0;
    }

    public int GetHitHate(MonsterTarget target)
    {
        if (target == null)
        {
            return 0;
        }

        return hitHateScores.TryGetValue(target.Key, out int hate) ? hate : 0;
    }

    public int GetTotalHate(MonsterTarget target, int distance)
    {
        return GetBaselineHate(target) + GetHitHate(target) + GetProximityHate(distance);
    }

    /// <summary>
    /// Read-only snapshot for the custom Inspector during Play mode.
    /// </summary>
    public List<MonsterHateDebugEntry> BuildDebugSnapshot(GridManager gridManager)
    {
        List<MonsterHateDebugEntry> result = new List<MonsterHateDebugEntry>();
        if (selfIdentity == null)
        {
            return result;
        }

        if (!Application.isPlaying)
        {
            return result;
        }

        EnsureBaselineHate();

        HashSet<string> coveredKeys = new HashSet<string>();
        if (gridManager != null)
        {
            gridManager.EnsureGridSystemInitialized();
            CellManager selfCell = MonsterPathfinding.FindMonsterCell(gridManager, selfIdentity);

            foreach (MonsterTarget target in GetHostileTargets(gridManager))
            {
                int distance = selfCell == null
                    ? int.MaxValue
                    : MonsterPathfinding.GetPathDistance(
                        gridManager,
                        selfIdentity,
                        selfCell,
                        target.Cell);

                int baselineHate = GetBaselineHate(target);
                int hitHate = GetHitHate(target);
                int proximityHate = GetProximityHate(distance);
                result.Add(new MonsterHateDebugEntry(
                    target.Key,
                    target.DisplayName,
                    distance,
                    baselineHate,
                    hitHate,
                    proximityHate));
                coveredKeys.Add(target.Key);
            }
        }

        HashSet<string> allHateKeys = new HashSet<string>(baselineHateScores.Keys);
        allHateKeys.UnionWith(hitHateScores.Keys);

        foreach (string hateKey in allHateKeys)
        {
            if (coveredKeys.Contains(hateKey))
            {
                continue;
            }

            int baselineHate = baselineHateScores.TryGetValue(hateKey, out int storedBaseline)
                ? storedBaseline
                : 0;
            int hitHate = hitHateScores.TryGetValue(hateKey, out int storedHit)
                ? storedHit
                : 0;

            result.Add(new MonsterHateDebugEntry(
                hateKey,
                hateKey,
                int.MaxValue,
                baselineHate,
                hitHate,
                0));
        }

        result.Sort((left, right) =>
        {
            int totalCompare = right.TotalHate.CompareTo(left.TotalHate);
            if (totalCompare != 0)
            {
                return totalCompare;
            }

            return string.Compare(left.DisplayName, right.DisplayName, System.StringComparison.Ordinal);
        });

        return result;
    }

    private int GetProximityHate(int distance)
    {
        switch (distance)
        {
            case 1:
                return 3;
            case 2:
                return 2;
            case 3:
                return 1;
            default:
                return 0;
        }
    }

    private void EnsureBaselineHate()
    {
        if (baselineApplied || selfIdentity == null)
        {
            return;
        }

        baselineApplied = true;

        // The Dog faction represents the hyenas in the current faction setup.
        if (selfIdentity.faction != MonsterIdentityManager.MonsterFaction.Dog)
        {
            return;
        }

        MonsterIdentitySystem monsterSystem = MonsterIdentitySystem.Instance;
        if (monsterSystem == null)
        {
            return;
        }

        foreach (MonsterIdentityManager other in monsterSystem.GetAllMonsters())
        {
            if (other != null &&
                other != selfIdentity &&
                other.faction == MonsterIdentityManager.MonsterFaction.Crocodile)
            {
                AddBaselineHate(GetMonsterKey(other), 8);
            }
        }
    }

    private void AddBaselineHate(string targetKey, int amount)
    {
        AddHate(baselineHateScores, targetKey, amount);
    }

    private void AddHitHate(string targetKey, int amount)
    {
        AddHate(hitHateScores, targetKey, amount);
    }

    private static void AddHate(Dictionary<string, int> hateTable, string targetKey, int amount)
    {
        if (string.IsNullOrEmpty(targetKey) || amount == 0)
        {
            return;
        }

        if (hateTable.TryGetValue(targetKey, out int current))
        {
            hateTable[targetKey] = current + amount;
        }
        else
        {
            hateTable[targetKey] = amount;
        }
    }

    private string GetMonsterKey(MonsterIdentityManager monster)
    {
        if (monster == null)
        {
            return string.Empty;
        }

        return string.IsNullOrEmpty(monster.monsterId)
            ? monster.GetInstanceID().ToString()
            : monster.monsterId;
    }
}
