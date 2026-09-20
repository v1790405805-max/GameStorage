using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 可隐蔽点：挂载本组件的格子即被标注为"可隐蔽点"。
/// 连成一片的隐蔽点（同层 X/Z 相邻一格，或同列层级号连续 L±1）视野共通：
/// 玩家在隐蔽片内时，片外的怪物丢失视野、不会追踪/攻击玩家；
/// 怪物进入该片任意一格即获得全片视野，离开后再次丢失。
/// 挂载位置：与 CellManager 同一个 GameObject（格子的 Trigger 碰撞体在该物体上，
/// 本组件会独立接收玩家进出的 Trigger 事件）。
/// </summary>
public class ConcealmentCell : MonoBehaviour
{
    /// <summary>
    /// 全局隐蔽状态：玩家当前是否处于任一可隐蔽点内。
    /// 怪物 AI（MonsterMoveAction / MonsterAttackAction）每回合查询此值决定是否丢失视野。
    /// </summary>
    public static bool IsPlayerHidden
    {
        get
        {
            // 自愈：清除可能残留的失效引用（例如禁用 Domain Reload 时上一局遗留的引用）
            ActiveCells.RemoveWhere(cell => cell == null);
            return ActiveCells.Count > 0;
        }
    }

    /// <summary>当前玩家正处于其中的可隐蔽点集合（玩家同时压住多个格边界时仍保持隐蔽）</summary>
    private static readonly HashSet<ConcealmentCell> ActiveCells = new HashSet<ConcealmentCell>();

    /// <summary>进入本格 Trigger 的玩家碰撞体集合（玩家可能有多个带 Player 标签的碰撞体）</summary>
    private readonly HashSet<Collider> playerCollidersInside = new HashSet<Collider>();

    /// <summary>本格当前是否有玩家处于其中（实例级状态，供 UI / 调试使用）</summary>
    public bool IsPlayerInside => playerCollidersInside.Count > 0;

    /// <summary>
    /// 怪物是否能看到玩家（可隐蔽片视野规则）：
    /// - 玩家不在任何隐蔽点内 → 可见；
    /// - 玩家在隐蔽点内：怪物处于与玩家连成一片的隐蔽点内 → 可见（获得全片视野），否则丢失视野。
    /// 连片判定完全使用格子编号：同层 X/Z 相邻一格，或同列层级号连续（L±1），不使用世界坐标。
    /// </summary>
    public static bool CanMonsterSeePlayer(CellManager monsterCell)
    {
        // 信息不足时不阻断原有行为（按可见处理）
        if (monsterCell == null)
        {
            return true;
        }

        // 没有任何隐蔽点内有玩家 → 玩家在开阔地，正常可见
        if (!IsPlayerHidden)
        {
            return true;
        }

        ConcealmentCell monsterConceal = monsterCell.GetComponent<ConcealmentCell>();
        if (monsterConceal == null || !monsterConceal.enabled)
        {
            // 玩家在隐蔽片内，怪物不在任何隐蔽点内 → 丢失视野
            return false;
        }

        GridManager grid = FindFirstObjectByType<GridManager>();
        if (grid == null)
        {
            return false;
        }

        // BFS：从怪物所在隐蔽格出发，按编号连通关系遍历隐蔽格，寻找含玩家的格
        HashSet<ConcealmentCell> visited = new HashSet<ConcealmentCell>();
        Queue<ConcealmentCell> queue = new Queue<ConcealmentCell>();
        visited.Add(monsterConceal);
        queue.Enqueue(monsterConceal);

        while (queue.Count > 0)
        {
            ConcealmentCell current = queue.Dequeue();
            if (current.IsPlayerInside)
            {
                return true; // 与玩家同片 → 获得全片视野
            }

            CellManager currentCell = current.GetComponent<CellManager>();
            if (currentCell == null)
            {
                continue;
            }

            (int cx, int cz) = grid.GetCellGridPosition(currentCell);
            int layer = grid.GetCellLayer(currentCell);

            // 正交四邻 + 同列上下层（编号连续即连接）
            CellManager[] neighbors = new CellManager[]
            {
                grid.GetCellManagerAt(cx + 1, cz, layer),
                grid.GetCellManagerAt(cx - 1, cz, layer),
                grid.GetCellManagerAt(cx, cz + 1, layer),
                grid.GetCellManagerAt(cx, cz - 1, layer),
                grid.GetCellManagerAt(cx, cz, layer + 1),
                grid.GetCellManagerAt(cx, cz, layer - 1),
            };

            for (int i = 0; i < neighbors.Length; i++)
            {
                CellManager neighbor = neighbors[i];
                if (neighbor == null)
                {
                    continue;
                }

                ConcealmentCell neighborConceal = neighbor.GetComponent<ConcealmentCell>();
                if (neighborConceal == null || !neighborConceal.enabled)
                {
                    continue;
                }

                if (visited.Add(neighborConceal))
                {
                    queue.Enqueue(neighborConceal);
                }
            }
        }

        return false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null || !other.CompareTag("Player"))
        {
            return;
        }

        playerCollidersInside.Add(other);
        if (playerCollidersInside.Count == 1)
        {
            ActiveCells.Add(this);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other == null || !other.CompareTag("Player"))
        {
            return;
        }

        if (playerCollidersInside.Remove(other) && playerCollidersInside.Count == 0)
        {
            ActiveCells.Remove(this);
        }
    }

    private void OnDisable()
    {
        // 防止格子被销毁 / 停用 / 网格重生成时，玩家仍被记为"隐蔽"
        if (playerCollidersInside.Count > 0)
        {
            playerCollidersInside.Clear();
            ActiveCells.Remove(this);
        }
    }
}
