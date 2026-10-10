using UnityEngine;

/// <summary>
/// 向敌方单位身后突进的效果（对应 CardEffectType.Attack 勾选时生效）。
///
/// 出牌前先验证目标单位身后是否存在可落脚空格：
/// 目标格正前方一格必须在地图内、与目标格层差 ≤ 1、没有玩家不可进入的
/// 地形或单位，并且允许玩家落脚。验证失败时卡牌不能使用，避免前面的
/// TeleportEffect 先把玩家传送到敌方所在格后，再因突进失败留下重叠状态。
/// </summary>
public class DashBehindEffect : CardEffectCore
{
    public static bool IsAttachedTo(CardData card)
    {
        if (card == null || card.extraEffects == null)
        {
            return false;
        }

        foreach (ExtraCardEffect extra in card.extraEffects)
        {
            if (string.IsNullOrEmpty(extra.effectTypeName))
            {
                continue;
            }

            System.Type effectType = System.Type.GetType(extra.effectTypeName);
            if (effectType == typeof(DashBehindEffect))
            {
                return true;
            }
        }

        return false;
    }

    public override bool CanPlay(CardData card, Vector2Int targetGrid)
    {
        if (card == null || !card.effectFlags.HasFlag(CardEffectType.Attack))
        {
            return false;
        }

        GridManager gridManager = Object.FindFirstObjectByType<GridManager>();
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (gridManager == null || player == null)
        {
            return false;
        }

        Vector2Int startGrid = GetGridPosition(gridManager, player.transform.position);

        // CardUI 常态悬停会用玩家自身所在格询问卡牌是否可用；
        // 这不是实际出牌目标，不能在这里否决，否则费用足够也会显示红色。
        if (targetGrid == startGrid)
        {
            return true;
        }

        return TryGetLandingCell(
            gridManager,
            startGrid,
            targetGrid,
            requireTargetMonster: true,
            out _,
            out _);
    }

    public override bool Execute(CardData card, Vector2Int targetGrid)
    {
        if (card == null || !card.effectFlags.HasFlag(CardEffectType.Attack))
        {
            Debug.LogWarning($"[DashBehindEffect] 卡牌 [{card?.cardName}] 未勾选 Attack，效果不生效。");
            return false;
        }

        GridManager gridManager = Object.FindFirstObjectByType<GridManager>();
        if (gridManager == null)
        {
            Debug.LogError("[DashBehindEffect] 未找到 GridManager。");
            return false;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogError("[DashBehindEffect] 未找到 Tag 为 Player 的对象。");
            return false;
        }

        Vector2Int startGrid = new Vector2Int(-1, -1);
        if (CardManager.Instance != null)
        {
            startGrid = CardManager.Instance.playStartGrid;
        }

        if (startGrid.x < 0 || startGrid.y < 0)
        {
            startGrid = GetGridPosition(gridManager, player.transform.position);
        }

        if (!TryGetLandingCell(
                gridManager,
                startGrid,
                targetGrid,
                requireTargetMonster: false,
                out Vector2Int landingGrid,
                out CellManager landingCell))
        {
            // TeleportEffect 可能已经把玩家放进目标格；这里兜底恢复到出牌前位置，
            // 避免任何运行时状态变化导致玩家与敌方单位重叠。
            CellManager startCell = gridManager.GetCellManagerAt(startGrid.x, startGrid.y);
            if (startCell != null)
            {
                Transform playerTransformRoot = player.transform.root;
                Vector3 startPos = startCell.transform.position;
                startPos.x -= gridManager.cellOffset.x;
                startPos.z -= gridManager.cellOffset.z;
                playerTransformRoot.position = startPos;

                PlayerMoveController moveCtrl =
                    Object.FindFirstObjectByType<PlayerMoveController>();
                if (moveCtrl != null)
                {
                    moveCtrl.SyncGridPosition(startGrid);
                }
            }

            Debug.LogWarning(
                $"[DashBehindEffect] 目标格 [{targetGrid}] 身后没有合法落点，无法突进。");
            return false;
        }

        Transform playerRoot = player.transform.root;
        Vector3 targetPos = landingCell.transform.position;
        targetPos.x -= gridManager.cellOffset.x;
        targetPos.z -= gridManager.cellOffset.z;
        playerRoot.position = targetPos;

        PlayerMoveController moveController = Object.FindFirstObjectByType<PlayerMoveController>();
        if (moveController != null)
        {
            moveController.SyncGridPosition(landingGrid);
        }

        Debug.Log(
            $"[DashBehindEffect] 玩家突进至目标格 [{targetGrid}] 身后的 [{landingGrid}]。");
        return true;
    }

    public static bool TryGetLandingCell(
        GridManager gridManager,
        Vector2Int startGrid,
        Vector2Int targetGrid,
        bool requireTargetMonster,
        out Vector2Int landingGrid,
        out CellManager landingCell)
    {
        landingGrid = new Vector2Int(-1, -1);
        landingCell = null;

        if (gridManager == null ||
            startGrid.x < 0 ||
            startGrid.y < 0 ||
            !gridManager.IsValidGridPosition(targetGrid.x, targetGrid.y))
        {
            return false;
        }

        int dx = targetGrid.x - startGrid.x;
        int dy = targetGrid.y - startGrid.y;
        if (dx == 0 && dy == 0)
        {
            return false;
        }

        Vector2Int direction = Mathf.Abs(dx) >= Mathf.Abs(dy)
            ? new Vector2Int(dx > 0 ? 1 : -1, 0)
            : new Vector2Int(0, dy > 0 ? 1 : -1);

        CellManager targetCell = FindCellContainingMonster(gridManager, targetGrid);
        if (requireTargetMonster && targetCell == null)
        {
            return false;
        }

        if (targetCell == null)
        {
            targetCell = gridManager.GetCellManagerAt(targetGrid.x, targetGrid.y);
        }

        if (targetCell == null)
        {
            return false;
        }

        landingGrid = targetGrid + direction;
        landingCell = FindLandingCell(gridManager, targetCell, landingGrid);
        if (landingCell == null)
        {
            return false;
        }

        return true;
    }

    private static CellManager FindCellContainingMonster(
        GridManager gridManager,
        Vector2Int grid)
    {
        foreach (CellManager cell in gridManager.GetCellManagersInColumn(grid.x, grid.y))
        {
            if (cell != null && cell.HasMonsterInside)
            {
                return cell;
            }
        }

        return null;
    }

    private static CellManager FindLandingCell(
        GridManager gridManager,
        CellManager targetCell,
        Vector2Int landingGrid)
    {
        if (gridManager == null || targetCell == null)
        {
            return null;
        }

        int targetLayer = gridManager.GetCellLayer(targetCell);
        CellManager sameLayerCell = null;
        CellManager nearestLayerCell = null;
        int nearestLayerDifference = int.MaxValue;

        foreach (CellManager candidate in
                 gridManager.GetCellManagersInColumn(landingGrid.x, landingGrid.y))
        {
            if (candidate == null)
            {
                continue;
            }

            // 目标身后任意层已有单位时，不允许作为突进落点。
            if (candidate.HasMonsterInside)
            {
                return null;
            }

            if (candidate.BlocksPlayer ||
                !RangeSystem.CanConnectAcrossLayers(gridManager, targetCell, candidate) ||
                !AbilityCore.CanLandOnCell(candidate))
            {
                continue;
            }

            int layerDifference = Mathf.Abs(
                gridManager.GetCellLayer(candidate) - targetLayer);
            if (layerDifference == 0)
            {
                sameLayerCell = candidate;
            }
            else if (layerDifference < nearestLayerDifference)
            {
                nearestLayerDifference = layerDifference;
                nearestLayerCell = candidate;
            }
        }

        return sameLayerCell != null ? sameLayerCell : nearestLayerCell;
    }

    private static Vector2Int GetGridPosition(GridManager gridManager, Vector3 worldPosition)
    {
        gridManager.EnsureGridSystemInitialized();
        Vector3 logicPosition = worldPosition - gridManager.cellOffset;
        var (x, z) = gridManager.GetGridPosition(logicPosition);
        return new Vector2Int(x, z);
    }
}
