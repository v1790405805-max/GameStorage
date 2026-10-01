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

        (int selfX, int selfZ) = gridManager.GetGridPosition(self.transform.position);
        if (!gridManager.IsValidGridPosition(selfX, selfZ))
        {
            return false;
        }

        for (int x = 0; x < gridManager.width; x++)
        {
            for (int z = 0; z < gridManager.height; z++)
            {
                int distance = Mathf.Abs(x - selfX) + Mathf.Abs(z - selfZ);
                if (distance == 0 || distance > attackRange)
                {
                    continue;
                }

                foreach (CellManager cell in gridManager.GetCellManagersInColumn(x, z))
                {
                    if (cell == null)
                    {
                        continue;
                    }

                    if (cell.IsPlayerInside && self.IsHostileToPlayer())
                    {
                        return true;
                    }

                    foreach (MonsterIdentityManager other in cell.GetMonstersInside())
                    {
                        if (other != null && other != self && self.IsHostileTo(other))
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }
}
