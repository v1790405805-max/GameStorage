using UnityEngine;

/// <summary>
/// Attack movement effect that advances into the target's cell and pushes the
/// target one cell farther in the attack direction.
///
/// After attacking a target, the player moves into the target's cell and the
/// target is pushed one cell farther in the attack direction. If there is no
/// movable cell in that direction, or the only destination is a higher layer,
/// neither unit moves. Failed pushes and pushes that lower the target's cell
/// layer restore 1 cost.
/// </summary>
public class PushEffect : CardEffectCore
{
    private const string PlayerTag = "Player";
    private const int CostRestoreAmount = 1;

    public override bool Execute(CardData card, Vector2Int targetGrid)
    {
        if (card == null || !card.effectFlags.HasFlag(CardEffectType.Attack))
        {
            Debug.LogWarning("[PushEffect] The card is not configured as an attack.");
            return false;
        }

        GridManager gridManager = Object.FindFirstObjectByType<GridManager>();
        if (gridManager == null)
        {
            Debug.LogError("[PushEffect] GridManager was not found.");
            return false;
        }

        GameObject player = GameObject.FindGameObjectWithTag(PlayerTag);
        if (player == null)
        {
            Debug.LogError($"[PushEffect] No object with tag [{PlayerTag}] was found.");
            return false;
        }

        Vector2Int startGrid = GetPlayerStartGrid(gridManager, player);
        if (startGrid.x < 0 || startGrid.y < 0)
        {
            Debug.LogWarning("[PushEffect] The player's starting cell is invalid.");
            return false;
        }

        Vector2Int direction = GetCardinalDirection(startGrid, targetGrid);
        if (direction == Vector2Int.zero)
        {
            Debug.LogWarning("[PushEffect] The player and target cannot occupy the same cell.");
            return false;
        }

        MonsterIdentityManager targetMonster = FindMonsterAtGrid(gridManager, targetGrid);
        if (targetMonster == null)
        {
            Debug.Log($"[PushEffect] No living enemy was found at target cell [{targetGrid}].");
            return false;
        }

        CellManager targetCell = FindCellContainingMonster(gridManager, targetMonster, targetGrid);
        if (targetCell == null)
        {
            Debug.LogWarning($"[PushEffect] Could not resolve the cell containing the target at [{targetGrid}].");
            return false;
        }

        int targetLayer = gridManager.GetCellLayer(targetCell);
        Vector2Int pushedGrid = targetGrid + direction;
        CellManager pushedCell = FindMovableCell(gridManager, pushedGrid, targetLayer);

        if (pushedCell == null)
        {
            RestoreCost();
            Debug.Log($"[PushEffect] No movable cell exists behind target [{targetGrid}] from L{targetLayer}; no movement occurred and 1 cost was restored.");
            return true;
        }

        int pushedLayer = gridManager.GetCellLayer(pushedCell);
        if (pushedLayer > targetLayer)
        {
            RestoreCost();
            Debug.Log($"[PushEffect] Target cannot be pushed upward from L{targetLayer} to L{pushedLayer}; treated as a wall, no movement occurred and 1 cost was restored.");
            return true;
        }

        bool shouldRestoreCost = pushedLayer < targetLayer;

        // Move the enemy first so the player can enter the target cell without
        // temporarily sharing its position.
        targetMonster.transform.position = pushedCell.transform.position;
        MovePlayerToCell(gridManager, player, targetCell, targetGrid);

        if (shouldRestoreCost)
        {
            RestoreCost();
        }

        Debug.Log($"[PushEffect] Player moved to [{targetGrid}] and target was pushed from L{targetLayer} to L{pushedLayer} at [{pushedGrid}] (cost restored: {shouldRestoreCost}).");
        return true;
    }

    private static Vector2Int GetPlayerStartGrid(GridManager gridManager, GameObject player)
    {
        if (CardManager.Instance != null)
        {
            Vector2Int recordedGrid = CardManager.Instance.playStartGrid;
            if (recordedGrid.x >= 0 && recordedGrid.y >= 0)
            {
                return recordedGrid;
            }
        }

        gridManager.EnsureGridSystemInitialized();
        Vector3 logicalPosition = player.transform.position - gridManager.cellOffset;
        var (x, z) = gridManager.GetGridPosition(logicalPosition);
        return new Vector2Int(x, z);
    }

    private static Vector2Int GetCardinalDirection(Vector2Int from, Vector2Int to)
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

    private static MonsterIdentityManager FindMonsterAtGrid(GridManager gridManager, Vector2Int grid)
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

        return null;
    }

    private static CellManager FindCellContainingMonster(
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

        return gridManager.GetCellManagerAt(grid.x, grid.y);
    }

    private static CellManager FindMovableCell(
        GridManager gridManager,
        Vector2Int grid,
        int originLayer)
    {
        CellManager nearestValidCell = null;
        int nearestLayerDifference = int.MaxValue;

        foreach (CellManager cell in gridManager.GetCellManagersInColumn(grid.x, grid.y))
        {
            if (cell == null || cell.IsLocked)
            {
                continue;
            }

            int layerDifference = Mathf.Abs(gridManager.GetCellLayer(cell) - originLayer);
            if (layerDifference > 1)
            {
                continue;
            }

            if (layerDifference == 0)
            {
                return cell;
            }

            if (layerDifference < nearestLayerDifference)
            {
                nearestValidCell = cell;
                nearestLayerDifference = layerDifference;
            }
        }

        return nearestValidCell;
    }

    private static void MovePlayerToCell(
        GridManager gridManager,
        GameObject player,
        CellManager targetCell,
        Vector2Int targetGrid)
    {
        Transform playerRoot = player.transform.root;
        Vector3 targetPosition = targetCell.transform.position;
        targetPosition.x -= gridManager.cellOffset.x;
        targetPosition.z -= gridManager.cellOffset.z;
        playerRoot.position = targetPosition;

        PlayerMoveController moveController = Object.FindFirstObjectByType<PlayerMoveController>();
        if (moveController != null)
        {
            moveController.SyncGridPosition(targetGrid);
        }
    }

    private static void RestoreCost()
    {
        if (CombatStatsManager.Instance == null)
        {
            Debug.LogWarning("[PushEffect] CombatStatsManager was not found; cost was not restored.");
            return;
        }

        CombatStatsManager.Instance.ModifyEnergy(CostRestoreAmount, allowExceedMax: true);
    }
}
