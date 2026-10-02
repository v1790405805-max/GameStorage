using UnityEngine;

/// <summary>
/// Range queries shared by monster actions.
/// </summary>
public static class MonsterTargetManager
{
    public static bool HasHostileInAttackRange(
        MonsterIdentityManager self,
        GridManager gridManager,
        int attackRange)
    {
        if (self == null || gridManager == null || attackRange <= 0)
        {
            return false;
        }

        gridManager.EnsureGridSystemInitialized();

        CellManager selfCell = MonsterPathfinding.FindMonsterCell(gridManager, self);
        if (selfCell == null)
        {
            return false;
        }

        MonsterIdentitySystem monsterSystem = MonsterIdentitySystem.Instance;
        if (monsterSystem != null)
        {
            foreach (MonsterIdentityManager other in monsterSystem.GetAllMonsters())
            {
                if (other == null ||
                    other == self ||
                    !other.gameObject.activeInHierarchy ||
                    !self.IsHostileTo(other))
                {
                    continue;
                }

                CellManager targetCell = MonsterPathfinding.FindMonsterCell(gridManager, other);
                if (targetCell == null)
                {
                    continue;
                }

                int distance = MonsterPathfinding.GetPathDistance(
                    gridManager,
                    self,
                    selfCell,
                    targetCell);

                if (distance > 0 && distance <= attackRange)
                {
                    return true;
                }
            }
        }

        if (self.IsHostileToPlayer())
        {
            CellManager playerCell = MonsterPathfinding.FindPlayerCell(gridManager);
            if (playerCell != null)
            {
                int distance = MonsterPathfinding.GetPathDistance(
                    gridManager,
                    self,
                    selfCell,
                    playerCell);

                if (distance > 0 && distance <= attackRange)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
