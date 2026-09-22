using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 直线攻击效果。
///
/// 将原本「仅攻击目标落点格」的攻击结算改为：
/// 「从目标落点格到玩家出牌前所在格之间连成的直线」上的所有怪物均受到伤害。
/// 通过 CardData.extraEffects 挂载本脚本触发，机制与 TeleportEffect 相同。
/// </summary>
public class LineAttackEffect : CardEffectCore
{
    /// <summary>
    /// 效果基类入口：卡牌打出时经 CardData.extraEffects 动态实例化触发。
    /// </summary>
    public override bool Execute(CardData card, Vector2Int targetGrid)
    {
        GridManager gridManager = Object.FindFirstObjectByType<GridManager>();
        if (gridManager == null)
        {
            Debug.LogError("[LineAttackEffect] 未找到 GridManager。");
            return false;
        }

        // 起始格：优先取 CardManager 在位移效果执行前记录的玩家起始格（避免 TeleportEffect 先位移导致直线退化）
        Vector2Int startGrid = new Vector2Int(-1, -1);
        if (CardManager.Instance != null)
            startGrid = CardManager.Instance.playStartGrid;

        if (startGrid.x < 0 || startGrid.y < 0)
        {
            // 回退：取玩家当前位置作为起始格
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj == null)
            {
                Debug.LogError("[LineAttackEffect] 未找到 Tag 为 Player 的对象。");
                return false;
            }

            gridManager.EnsureGridSystemInitialized();
            Vector3 logicPos = playerObj.transform.position - gridManager.cellOffset;
            var (px, pz) = gridManager.GetGridPosition(logicPos);
            startGrid = new Vector2Int(px, pz);
        }

        // 计算「目标格 → 起始格」直线经过的所有格子（含两端）
        List<Vector2Int> lineCells = GetLineCells(targetGrid, startGrid);

        int hitCount = 0;
        if (MonsterIdentitySystem.Instance != null)
        {
            // 遍历前先复制快照：TakeDamage 可能击杀怪物并从 MonsterIdentitySystem 移除，
            // 直接遍历原 HashSet 会抛出 “Collection was modified” 异常
            List<MonsterIdentityManager> monsters = new List<MonsterIdentityManager>(MonsterIdentitySystem.Instance.GetAllMonsters());
            foreach (var monster in monsters)
            {
                if (monster == null) continue;

                var (mx, mz) = gridManager.GetGridPosition(monster.transform.position);
                if (!lineCells.Contains(new Vector2Int(mx, mz))) continue;

                MonsterStats stats = monster.GetComponent<MonsterStats>();
                if (stats != null)
                {
                    stats.TakeDamage(card.damage);
                    hitCount++;
                }
            }
        }

        Debug.Log($"[LineAttackEffect] 直线攻击结算：起始格 {startGrid} → 目标格 {targetGrid}，命中 {hitCount} 只怪物。");
        return true;
    }

    /// <summary>
    /// Bresenham 直线算法：返回 from 到 to 连线上经过的所有格子（含两端点）。
    /// </summary>
    private static List<Vector2Int> GetLineCells(Vector2Int from, Vector2Int to)
    {
        List<Vector2Int> cells = new List<Vector2Int>();

        int x0 = from.x, y0 = from.y;
        int x1 = to.x, y1 = to.y;
        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            cells.Add(new Vector2Int(x0, y0));
            if (x0 == x1 && y0 == y1) break;

            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x0 += sx; }
            if (e2 < dx) { err += dx; y0 += sy; }
        }

        return cells;
    }
}
