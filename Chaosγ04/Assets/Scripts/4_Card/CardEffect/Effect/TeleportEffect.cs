using UnityEngine;

/// <summary>
/// 卡牌效果：玩家传送（对应 CardEffectType.Movement 时的额外扩展脚本）。
/// 
/// 负责执行「卡牌打出后，将玩家直接传送到目标落点格」的瞬移逻辑。
/// 只有当 CardData 的 extraEffects 明确挂载了本脚本时才会实例化执行。
/// </summary>
public class TeleportEffect : CardEffectCore
{
    /// <summary>
    /// 效果基类入口：卡牌打出时经 CardData.extraEffects 动态实例化触发
    /// </summary>
    public override bool Execute(CardData card, Vector2Int targetGrid)
    {
        GridManager gridManager = Object.FindFirstObjectByType<GridManager>();
        return Execute(card, CardTarget.FromGrid(gridManager, targetGrid));
    }

    public override bool Execute(CardData card, CardTarget target)
    {
        GridManager gridManager = Object.FindFirstObjectByType<GridManager>();
        if (gridManager == null)
        {
            Debug.LogError("[TeleportEffect] 传送失败：未找到 GridManager。");
            return false;
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null)
        {
            Debug.LogError("[TeleportEffect] 传送失败：未找到 Tag 为 Player 的对象。");
            return false;
        }

        CellManager targetCell = target.Cell;
        if (targetCell == null && target.Coord.IsValid)
        {
            targetCell = gridManager.GetCellManagerAt(target.Coord);
        }

        if (targetCell == null)
        {
            Debug.LogWarning($"[TeleportEffect] 传送失败：目标格 [{target.Coord}] 无效。");
            return false;
        }

        if (targetCell.BlocksPlayer)
        {
            Debug.LogWarning($"[TeleportEffect] 传送失败：目标格 [{targetCell.Coord}] 不可作为玩家落点。");
            return false;
        }

        // 修改 Transform 位置实现瞬移
        Transform playerRoot = playerObj.transform.root;
        Vector3 targetPos = targetCell.transform.position;
        targetPos.x -= gridManager.cellOffset.x;
        targetPos.z -= gridManager.cellOffset.z;
        playerRoot.position = targetPos;

        // 同步玩家移动控制器的网格坐标
        PlayerMoveController moveController = Object.FindFirstObjectByType<PlayerMoveController>();
        if (moveController != null)
        {
            moveController.SyncCellPosition(targetCell);
        }

        Debug.Log($"[TeleportEffect] 玩家已成功传送至格点: {targetCell.Coord}");
        return true;
    }
}
