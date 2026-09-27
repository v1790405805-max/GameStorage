using UnityEngine;

/// <summary>
/// 突进效果（对应 CardEffectType.Attack 勾选时生效）。
///
/// 玩家选定目标格并完成出牌后：
/// 沿「玩家出牌前所在格 → 目标格」的方向再延伸一格（目标格正前方一格），
/// 若该格满足条件：与目标格层差 ≤ 1（落差不过大，如 L1→L3 不允许）、
/// 且格子为空（无怪物、未锁定），则将玩家瞬移到该格。
/// 通过 CardData.extraEffects 挂载本脚本触发，机制与 TeleportEffect 相同。
/// </summary>
public class LungeEffect : CardEffectCore
{
    /// <summary>
    /// 效果基类入口：卡牌打出时经 CardData.extraEffects 动态实例化触发。
    /// </summary>
    public override bool Execute(CardData card, Vector2Int targetGrid)
    {
        // 仅在 Attack 勾选时生效
        if (!card.effectFlags.HasFlag(CardEffectType.Attack))
        {
            Debug.LogWarning($"[LungeEffect] 卡牌 [{card.cardName}] 未勾选 Attack，效果不生效。");
            return false;
        }

        GridManager gridManager = Object.FindFirstObjectByType<GridManager>();
        if (gridManager == null)
        {
            Debug.LogError("[LungeEffect] 未找到 GridManager。");
            return false;
        }

        // 起始格：优先取 CardManager 在位移效果执行前记录的玩家出牌前所在格
        Vector2Int startGrid = new Vector2Int(-1, -1);
        if (CardManager.Instance != null)
            startGrid = CardManager.Instance.playStartGrid;

        if (startGrid.x < 0 || startGrid.y < 0)
        {
            // 回退：取玩家当前位置作为起始格
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj == null)
            {
                Debug.LogError("[LungeEffect] 未找到 Tag 为 Player 的对象。");
                return false;
            }

            gridManager.EnsureGridSystemInitialized();
            Vector3 logicPos = playerObj.transform.position - gridManager.cellOffset;
            var (px, pz) = gridManager.GetGridPosition(logicPos);
            startGrid = new Vector2Int(px, pz);
        }

        // 方向：玩家起始格 → 目标格 的主轴方向（项目为四方向移动，取 |dx|/|dy| 较大的一轴）
        int dx = targetGrid.x - startGrid.x;
        int dy = targetGrid.y - startGrid.y;
        if (dx == 0 && dy == 0)
        {
            Debug.LogWarning("[LungeEffect] 目标格与玩家起始格重合，无法确定突进方向。");
            return false;
        }

        Vector2Int dir = Mathf.Abs(dx) >= Mathf.Abs(dy)
            ? new Vector2Int(dx > 0 ? 1 : -1, 0)
            : new Vector2Int(0, dy > 0 ? 1 : -1);

        Vector2Int frontGrid = targetGrid + dir;

        // 目标格与正前方一格（GetCellManagerAt 返回该列顶层格子，层号从格子名解析）
        CellManager targetCell = gridManager.GetCellManagerAt(targetGrid.x, targetGrid.y);
        CellManager frontCell = gridManager.GetCellManagerAt(frontGrid.x, frontGrid.y);
        if (targetCell == null || frontCell == null)
        {
            Debug.LogWarning($"[LungeEffect] 目标格 [{targetGrid}] 或正前方格 [{frontGrid}] 无效。");
            return false;
        }

        // 条件1：正前方一格与目标格落差不能过大（层差 ≤ 1 才允许）
        if (!RangeSystem.CanConnectAcrossLayers(gridManager, targetCell, frontCell))
        {
            Debug.Log($"[LungeEffect] 正前方格 [{frontGrid}] 落差过大（目标 L{gridManager.GetCellLayer(targetCell)} → 前方 L{gridManager.GetCellLayer(frontCell)}），不突进。");
            return false;
        }

        // 条件2：正前方一格必须为空（无怪物、未锁定）
        if (frontCell.IsLocked || frontCell.HasMonsterInside)
        {
            Debug.Log($"[LungeEffect] 正前方格 [{frontGrid}] 非空（锁定或站有怪物），不突进。");
            return false;
        }

        // 瞬移玩家到正前方一格（与 TeleportEffect 相同的坐标修正与同步方式）
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogError("[LungeEffect] 未找到 Tag 为 Player 的对象。");
            return false;
        }

        Transform playerRoot = player.transform.root;
        Vector3 targetPos = frontCell.transform.position;
        targetPos.x -= gridManager.cellOffset.x;
        targetPos.z -= gridManager.cellOffset.z;
        // Y 保留正前方格高度，跨层突进时避免悬空/穿模
        playerRoot.position = targetPos;

        PlayerMoveController moveController = Object.FindFirstObjectByType<PlayerMoveController>();
        if (moveController != null)
            moveController.SyncGridPosition(frontGrid);

        Debug.Log($"[LungeEffect] 玩家突进至目标格 [{targetGrid}] 正前方一格 [{frontGrid}]。");
        return true;
    }
}
