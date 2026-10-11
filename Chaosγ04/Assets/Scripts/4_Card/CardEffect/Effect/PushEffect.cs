using UnityEngine;

/// <summary>
/// Generic push effect: pushes the target monster one cell farther along the
/// attack direction. Mounted through CardData.extraEffects, so several cards can
/// reuse it the same way TeleportEffect is reused.
///
/// This script only resolves and performs the push. It never moves the player,
/// never applies damage and never restores energy. The outcome is published in
/// <see cref="LastResult"/> so a later OnPushRecoverEnergyEffect on the same card
/// can settle the follow-up damage / energy, and OnPushRecoverEnergyEffect must be
/// listed after this script in the card's extraEffects.
///
/// Resolution order for the cell behind the target:
/// 1. No cell behind the target (off the map) - the target dies instantly.
/// 2. HardLocked cell (treated as no cell) - the target dies instantly.
/// 3. Wall cell, or a landing cell two or more layers higher (treated as a wall)
///    - the target is not pushed.
/// 4. Water cell - pushed only when the target is registered as water-traversable
///    in MonsterIdentityManager (CanTraverseWater); otherwise the target dies instantly.
/// 5. Cell occupied by another monster - the target is not pushed and both monsters
///    take the card's damage. Cell occupied by another object - the target is not pushed.
/// 6. pushedLayer exactly one layer higher than targetLayer - the target is not pushed.
/// 7. Otherwise (pushedLayer not greater than targetLayer and traversable) - the
///    target is pushed one cell back.
/// </summary>
public class PushEffect : CardEffectCore
{
    public enum PushOutcome
    {
        None = 0,
        Pushed,
        KilledTarget,
        BlockedByMonster,
        BlockedByObstacle,
        BlockedByWall,
        BlockedByHigherLayer
    }

    public struct PushResult
    {
        public bool isValid;
        public PushOutcome outcome;
        public MonsterIdentityManager target;
        public MonsterIdentityManager blocker;
        public int targetLayer;
        public int pushedLayer;
        public Vector2Int pushedGrid;
    }

    /// <summary>
    /// Result of the most recent PushEffect execution. OnPushRecoverEnergyEffect
    /// reads and consumes it, so PushEffect must be listed before
    /// OnPushRecoverEnergyEffect in the card's extraEffects.
    /// </summary>
    public static PushResult LastResult;

    private const string PlayerTag = "Player";

    public override bool Execute(CardData card, Vector2Int targetGrid)
    {
        return ResolvePush(card, targetGrid, applyBlockerDamage: true).isValid;
    }

    /// <summary>
    /// Resolves the push and returns its outcome. <paramref name="applyBlockerDamage"/> controls
    /// whether a monster blocking the landing cell takes the card's damage (the regular
    /// behaviour). Card orchestrators that redirect a blocked push instead pass false.
    /// When <paramref name="dryRun"/> is true the resolution produces no side effects (no
    /// movement, damage or death) so it can be used for drag previews.
    /// </summary>
    public PushResult ResolvePush(
        CardData card,
        Vector2Int targetGrid,
        bool applyBlockerDamage,
        bool dryRun = false)
    {
        LastResult = default;

        if (card == null || !card.effectFlags.HasFlag(CardEffectType.Attack))
        {
            Debug.LogWarning("[PushEffect] The card is not configured as an attack.");
            return LastResult;
        }

        GridManager gridManager = Object.FindFirstObjectByType<GridManager>();
        if (gridManager == null)
        {
            Debug.LogError("[PushEffect] GridManager was not found.");
            return LastResult;
        }

        if (!TryGetPlayerStartGrid(gridManager, out Vector2Int startGrid))
        {
            Debug.LogWarning("[PushEffect] The player's starting cell is invalid.");
            return LastResult;
        }

        Vector2Int direction = GetCardinalDirection(startGrid, targetGrid);
        if (direction == Vector2Int.zero)
        {
            Debug.LogWarning("[PushEffect] The player and target cannot occupy the same cell.");
            return LastResult;
        }

        MonsterIdentityManager targetMonster =
            FindMonsterAtGrid(gridManager, targetGrid, includeInactive: true);
        if (targetMonster == null)
        {
            Debug.Log($"[PushEffect] No living enemy was found at target cell [{targetGrid}].");
            return LastResult;
        }

        CellManager targetCell = FindCellContainingMonster(gridManager, targetMonster, targetGrid);
        if (targetCell == null)
        {
            Debug.LogWarning($"[PushEffect] Could not resolve the cell containing the target at [{targetGrid}].");
            return LastResult;
        }

        int targetLayer = gridManager.GetCellLayer(targetCell);
        Vector2Int pushedGrid = targetGrid + direction;
        CellManager pushedCell = ResolveLandingCell(gridManager, pushedGrid, targetLayer);

        PushResult result = new PushResult
        {
            isValid = true,
            target = targetMonster,
            targetLayer = targetLayer,
            pushedLayer = targetLayer,
            pushedGrid = pushedGrid
        };

        // 1. No cell behind the target: dies instantly.
        if (pushedCell == null)
        {
            result.outcome = PushOutcome.KilledTarget;
            LastResult = result;
            if (!dryRun) KillTarget(targetMonster, "no landing cell");
            return result;
        }

        int pushedLayer = gridManager.GetCellLayer(pushedCell);
        result.pushedLayer = pushedLayer;

        // 2. HardLocked counts as no landing cell: dies instantly.
        if (pushedCell.IsHardLocked)
        {
            result.outcome = PushOutcome.KilledTarget;
            LastResult = result;
            if (!dryRun) KillTarget(targetMonster, "hard-locked landing cell");
            return result;
        }

        // 3. Wall, or a landing cell two or more layers higher (treated as a wall):
        //    not pushed.
        if (pushedCell.IsWall || pushedLayer - targetLayer >= 2)
        {
            result.outcome = PushOutcome.BlockedByWall;
            LastResult = result;
            Debug.Log($"[PushEffect] Landing cell [{pushedGrid}] is a wall (or {pushedLayer - targetLayer} layers higher); the target was not pushed.");
            return result;
        }

        // 4. Water: only water-traversable monsters can be pushed in.
        if (pushedCell.IsWater && (targetMonster == null || !targetMonster.CanTraverseWater))
        {
            result.outcome = PushOutcome.KilledTarget;
            LastResult = result;
            if (!dryRun) KillTarget(targetMonster, "non-traversable water landing cell");
            return result;
        }

        // 5. Occupied landing cell: not pushed.
        MonsterIdentityManager blocker = FindOtherMonsterInside(pushedCell, targetMonster);
        if (blocker != null)
        {
            result.blocker = blocker;
            result.outcome = PushOutcome.BlockedByMonster;
            LastResult = result;

            // Regular push behaviour: a monster standing behind the target blocks the push and
            // both monsters take the card's damage. Orchestrators that redirect the push instead
            // skip that damage.
            if (applyBlockerDamage && !dryRun)
            {
                ApplyCardDamage(card, targetMonster);
                ApplyCardDamage(card, blocker);
                Debug.Log($"[PushEffect] Landing cell [{pushedGrid}] is occupied by another monster; the target was not pushed and both took {card.damage} damage.");
            }
            else
            {
                Debug.Log($"[PushEffect] Landing cell [{pushedGrid}] is occupied by another monster; the target was not pushed (blocker damage skipped).");
            }

            return result;
        }

        if (pushedCell.IsPlayerInside)
        {
            result.outcome = PushOutcome.BlockedByObstacle;
            LastResult = result;
            Debug.Log($"[PushEffect] Landing cell [{pushedGrid}] is occupied by another object; the target was not pushed.");
            return result;
        }

        // 6. Higher layer acts like a wall: not pushed.
        if (pushedLayer > targetLayer)
        {
            result.outcome = PushOutcome.BlockedByHigherLayer;
            LastResult = result;
            Debug.Log($"[PushEffect] Target cannot be pushed upward from L{targetLayer} to L{pushedLayer}; the target was not pushed.");
            return result;
        }

        // 7. Normal push: move the enemy one cell back.
        result.outcome = PushOutcome.Pushed;
        LastResult = result;
        if (!dryRun) targetMonster.transform.position = pushedCell.transform.position;
        Debug.Log($"[PushEffect] Target [{targetMonster.monsterId}] was pushed from L{targetLayer} to L{pushedLayer} at [{pushedGrid}].");
        return result;
    }

    private static bool TryGetPlayerStartGrid(GridManager gridManager, out Vector2Int startGrid)
    {
        startGrid = new Vector2Int(-1, -1);

        if (CardManager.Instance != null)
        {
            Vector2Int recordedGrid = CardManager.Instance.playStartGrid;
            if (recordedGrid.x >= 0 && recordedGrid.y >= 0)
            {
                startGrid = recordedGrid;
                return true;
            }
        }

        GameObject player = GameObject.FindGameObjectWithTag(PlayerTag);
        if (player == null)
        {
            return false;
        }

        gridManager.EnsureGridSystemInitialized();
        Vector3 logicalPosition = player.transform.position - gridManager.cellOffset;
        var (x, z) = gridManager.GetGridPosition(logicalPosition);
        startGrid = new Vector2Int(x, z);
        return startGrid.x >= 0 && startGrid.y >= 0;
    }

    internal static Vector2Int GetCardinalDirection(Vector2Int from, Vector2Int to)
    {
        int dx = to.x - from.x;
        int dy = to.y - from.y;
        if (dx == 0 && dy == 0)
        {
            return Vector2Int.zero;
        }

        return Mathf.Abs(dx) >= Mathf.Abs(dy)
            ? new Vector2Int(dx > 0 ? 1 : -1, 0)
            : new Vector2Int(0, dy > 0 ? 1 : -1);
    }

    internal static MonsterIdentityManager FindMonsterAtGrid(
        GridManager gridManager,
        Vector2Int grid,
        bool includeInactive = false)
    {
        if (MonsterIdentitySystem.Instance != null)
        {
            foreach (MonsterIdentityManager monster in MonsterIdentitySystem.Instance.GetAllMonsters())
            {
                if (monster == null || !monster.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var (x, z) = gridManager.GetGridPosition(monster.transform.position);
                if (x == grid.x && z == grid.y)
                {
                    return monster;
                }
            }
        }

        foreach (CellManager cell in gridManager.GetCellManagersInColumn(grid.x, grid.y))
        {
            if (cell == null)
            {
                continue;
            }

            foreach (MonsterIdentityManager monster in cell.GetMonstersInside())
            {
                if (monster != null && monster.gameObject.activeInHierarchy)
                {
                    return monster;
                }
            }
        }

        if (!includeInactive)
        {
            return null;
        }

        // Die() deactivates the monster before PushEffect runs in scenes without
        // PlayerCombatReceiver. The inactive target still occupies its original
        // grid position and must participate in the push decision.
        MonsterIdentityManager[] inactiveMonsters =
            Object.FindObjectsByType<MonsterIdentityManager>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        foreach (MonsterIdentityManager monster in inactiveMonsters)
        {
            if (monster == null || monster.gameObject.activeInHierarchy)
            {
                continue;
            }

            var (x, z) = gridManager.GetGridPosition(monster.transform.position);
            if (x == grid.x && z == grid.y)
            {
                return monster;
            }
        }

        return null;
    }

    internal static MonsterIdentityManager FindOtherMonsterInside(
        CellManager cell,
        MonsterIdentityManager exclude)
    {
        if (cell == null)
        {
            return null;
        }

        foreach (MonsterIdentityManager monster in cell.GetMonstersInside())
        {
            if (monster != null && monster != exclude)
            {
                return monster;
            }
        }

        return null;
    }

    internal static CellManager FindCellContainingMonster(
        GridManager gridManager,
        MonsterIdentityManager monster,
        Vector2Int grid)
    {
        foreach (CellManager cell in gridManager.GetCellManagersInColumn(grid.x, grid.y))
        {
            if (cell == null)
            {
                continue;
            }

            foreach (MonsterIdentityManager cellMonster in cell.GetMonstersInside())
            {
                if (cellMonster == monster)
                {
                    return cell;
                }
            }
        }

        CellManager nearestCell = null;
        float nearestVerticalDistance = float.MaxValue;

        foreach (CellManager cell in gridManager.GetCellManagersInColumn(grid.x, grid.y))
        {
            if (cell == null)
            {
                continue;
            }

            float verticalDistance = Mathf.Abs(
                cell.transform.position.y - monster.transform.position.y);
            if (verticalDistance < nearestVerticalDistance)
            {
                nearestVerticalDistance = verticalDistance;
                nearestCell = cell;
            }
        }

        return nearestCell != null
            ? nearestCell
            : gridManager.GetCellManagerAt(grid.x, grid.y);
    }

    /// <summary>
    /// Picks the landing cell behind the target. Prefers the cell whose layer
    /// matches the target's layer, otherwise the cell with the closest layer.
    /// Returns null when the adjacent column has no cell at all.
    /// </summary>
    internal static CellManager ResolveLandingCell(
        GridManager gridManager,
        Vector2Int grid,
        int originLayer)
    {
        CellManager sameLayerCell = null;
        CellManager nearestCell = null;
        int nearestLayerDifference = int.MaxValue;

        foreach (CellManager cell in gridManager.GetCellManagersInColumn(grid.x, grid.y))
        {
            if (cell == null)
            {
                continue;
            }

            int layerDifference = Mathf.Abs(gridManager.GetCellLayer(cell) - originLayer);
            if (layerDifference == 0)
            {
                sameLayerCell = cell;
                break;
            }

            if (layerDifference < nearestLayerDifference)
            {
                nearestLayerDifference = layerDifference;
                nearestCell = cell;
            }
        }

        return sameLayerCell != null ? sameLayerCell : nearestCell;
    }

    private static void ApplyCardDamage(CardData card, MonsterIdentityManager monster)
    {
        if (card == null || card.damage <= 0 || monster == null)
        {
            return;
        }

        if (!monster.gameObject.activeInHierarchy)
        {
            return;
        }

        MonsterStats stats = monster.GetComponent<MonsterStats>();
        stats?.TakeDamage(card.damage);
    }

    private static void KillTarget(MonsterIdentityManager monster, string reason)
    {
        if (monster == null)
        {
            return;
        }

        // Already dead earlier in this resolution (e.g. the attack damage settled
        // synchronously without a PlayerCombatReceiver). Skip so the death event
        // and kill counters are not triggered twice.
        if (!monster.gameObject.activeInHierarchy)
        {
            return;
        }

        MonsterStats stats = monster.GetComponent<MonsterStats>();
        if (stats == null)
        {
            Debug.LogWarning($"[PushEffect] Target [{monster.monsterId}] has no MonsterStats; it could not be killed.");
            return;
        }

        stats.KillInstantly();
        Debug.Log($"[PushEffect] Target [{monster.monsterId}] died instantly ({reason}).");
    }
}
