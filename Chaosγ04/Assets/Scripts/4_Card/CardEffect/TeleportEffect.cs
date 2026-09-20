using UnityEngine;

/// <summary>
/// 卡牌效果：玩家传送（对应 CardEffectType.Movement 时的额外扩展脚本）。
/// 
/// 负责执行「卡牌打出后，将玩家直接传送到目标落点格」的瞬移逻辑。
/// 只有当 CardData 的 extraEffects 明确挂载了本脚本时才会实例化执行。
/// </summary>
public class TeleportEffect : CardEffect
{
    /// <summary>
    /// 效果基类入口：卡牌打出时经 CardData.extraEffects 动态实例化触发
    /// </summary>
    public override bool Execute(CardData card, Vector2Int targetGrid)
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

        CellManager targetCell = gridManager.GetCellManagerAt(targetGrid.x, targetGrid.y);
        if (targetCell == null)
        {
            Debug.LogWarning($"[TeleportEffect] 传送失败：目标格 [{targetGrid}] 无效。");
            return false;
        }

        if (targetCell.IsLocked)
        {
            Debug.LogWarning($"[TeleportEffect] 传送失败：目标格 [{targetGrid}] 处于状态锁，无法传送。");
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
            moveController.SyncGridPosition(targetGrid);
        }

        Debug.Log($"[TeleportEffect] 玩家已成功传送至格点: {targetGrid}");
        return true;
    }
}