using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 直线攻击效果。
/// 将原本「仅攻击目标落点格」的攻击结算改为：
/// 「从目标落点格到玩家出牌前所在格之间连成的直线」上的所有怪物均受到伤害。
/// 已接入动画命中关键帧并支持按卡牌生成专属受击爆点。
/// </summary>
public class LineAttackEffect : CardEffectCore
{
    public override bool Execute(CardData card, Vector2Int targetGrid)
    {
        GridManager gridManager = Object.FindFirstObjectByType<GridManager>();
        if (gridManager == null)
        {
            Debug.LogError("[LineAttackEffect] 未找到 GridManager。");
            return false;
        }

        Vector2Int startGrid = new Vector2Int(-1, -1);
        if (CardManager.Instance != null)
            startGrid = CardManager.Instance.playStartGrid;

        if (startGrid.x < 0 || startGrid.y < 0)
        {
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

        List<Vector2Int> lineCells = GetLineCells(targetGrid, startGrid);

        if (MonsterIdentitySystem.Instance != null)
        {
            List<MonsterIdentityManager> monsters = new List<MonsterIdentityManager>(MonsterIdentitySystem.Instance.GetAllMonsters());

            // 挂起到主角 OnHit 动画关键帧执行
            PlayerCombatReceiver.ExecuteOnHit(() =>
            {
                int hitCount = 0;
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

                        // 👉 根据当前打出的卡牌，在命中的怪物胸口生成专属爆点特效
                        if (card.hitVFXPrefab != null)
                        {
                            Vector3 spawnPos = monster.transform.position + card.hitVFXOffset;
                            GameObject vfx = Object.Instantiate(card.hitVFXPrefab, spawnPos, Quaternion.identity);
                            Object.Destroy(vfx, 0.5f);
                        }
                    }
                }

                Debug.Log($"[LineAttackEffect] 直线攻击结算：起始格 {startGrid} → 目标格 {targetGrid}，命中 {hitCount} 只怪物。");
            });
        }

        return true;
    }

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