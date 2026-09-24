using System.Collections.Generic;
using UnityEngine;

// 注意：此处不再定义 public enum RangeType，直接使用 CardData.cs 中的 RangeType

/// <summary>
/// 范围计算系统：负责纯数学形状范围与结合地图障碍的范围计算。
/// </summary>
public static class RangeSystem
{
    /// <summary>
    /// 获取原始数学形状范围（不结合地图裁剪，仅数学坐标）
    /// </summary>
    public static HashSet<Vector2Int> GetRawRange(Vector2Int center, int distance, RangeType type = RangeType.Diamond)
    {
        HashSet<Vector2Int> area = new HashSet<Vector2Int>();

        switch (type)
        {
            case RangeType.Point:
                area.Add(center);
                break;

            case RangeType.Straight:
                area.Add(center);
                if (distance <= 0) break;

                Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
                foreach (var dir in dirs)
                {
                    for (int step = 1; step <= distance; step++)
                    {
                        area.Add(center + dir * step);
                    }
                }
                break;

            case RangeType.Diamond:
                if (distance < 0) break;
                for (int dx = -distance; dx <= distance; dx++)
                {
                    int maxDy = distance - Mathf.Abs(dx);
                    for (int dy = -maxDy; dy <= maxDy; dy++)
                    {
                        area.Add(new Vector2Int(center.x + dx, center.y + dy));
                    }
                }
                break;
        }

        return area;
    }

    /// <summary>
    /// 返回悬停格对应的单一主方向。
    /// 对角格默认优先判定为垂直方向，不会同时激活两个方向。
    /// </summary>
    public static Vector2Int GetQuarterCircleDirection(Vector2Int origin, Vector2Int target)
    {
        int dx = target.x - origin.x;
        int dy = target.y - origin.y;
        if (dx == 0 && dy == 0) return Vector2Int.zero;

        int absDx = Mathf.Abs(dx);
        int absDy = Mathf.Abs(dy);

        if (absDy >= absDx)
            return dy > 0 ? Vector2Int.up : Vector2Int.down;

        return dx > 0 ? Vector2Int.right : Vector2Int.left;
    }

    /// <summary>
    /// 判断目标格是否位于指定的主方向四分之一圆内。
    /// 对角边界格同时属于相邻两个方向，因此不同方向的范围允许重叠。
    /// </summary>
    public static bool IsInsideQuarterCircle(Vector2Int origin, Vector2Int target, Vector2Int direction)
    {
        int dx = target.x - origin.x;
        int dy = target.y - origin.y;
        if (dx == 0 && dy == 0) return false;

        int absDx = Mathf.Abs(dx);
        int absDy = Mathf.Abs(dy);

        if (direction == Vector2Int.right)
            return dx > 0 && absDx >= absDy;
        if (direction == Vector2Int.up)
            return dy > 0 && absDy >= absDx;
        if (direction == Vector2Int.left)
            return dx < 0 && absDx >= absDy;
        if (direction == Vector2Int.down)
            return dy < 0 && absDy >= absDx;

        return false;
    }

    /// <summary>
    /// 从给定范围中筛选出悬停格所对应的四分之一圆目标。
    /// </summary>
    public static HashSet<Vector2Int> GetQuarterCircleTargetGrids(
        HashSet<Vector2Int> rangeSet,
        Vector2Int origin,
        Vector2Int hoverGrid)
    {
        HashSet<Vector2Int> result = new HashSet<Vector2Int>();
        if (rangeSet == null || rangeSet.Count == 0) return result;

        Vector2Int direction = GetQuarterCircleDirection(origin, hoverGrid);
        if (direction == Vector2Int.zero) return result;

        foreach (Vector2Int grid in rangeSet)
        {
            if (IsInsideQuarterCircle(origin, grid, direction))
                result.Add(grid);
        }

        return result;
    }

    /// <summary>
    /// 格子级四分之一圆筛选：保留范围内的层信息，主方向内所有层的格子都会成为目标。
    /// 对角边界格会同时出现在相邻两个方向的目标集合中。
    /// </summary>
    public static HashSet<CellManager> GetQuarterCircleTargetCells(
        HashSet<CellManager> rangeSet,
        Vector2Int origin,
        Vector2Int hoverGrid,
        GridManager gridManager)
    {
        HashSet<CellManager> result = new HashSet<CellManager>();
        if (rangeSet == null || rangeSet.Count == 0 || gridManager == null) return result;

        Vector2Int direction = GetQuarterCircleDirection(origin, hoverGrid);
        if (direction == Vector2Int.zero) return result;

        foreach (CellManager cell in rangeSet)
        {
            if (cell == null) continue;

            var (cellX, cellZ) = gridManager.GetCellGridPosition(cell);
            if (IsInsideQuarterCircle(origin, new Vector2Int(cellX, cellZ), direction))
                result.Add(cell);
        }

        return result;
    }

    /// <summary>
    /// 判断两个格子是否允许在范围/寻路中互相连接。
    /// 规则：只相隔一层（层差 ≤ 1）可以连接，如 L2 可连 L1 / L3；层差 ≥ 2 无法连接，如 L1 无法连 L3。
    /// </summary>
    public static bool CanConnectAcrossLayers(GridManager gridManager, CellManager from, CellManager to)
    {
        if (gridManager == null || from == null || to == null) return false;
        int layerDiff = Mathf.Abs(gridManager.GetCellLayer(from) - gridManager.GetCellLayer(to));
        return layerDiff <= 1;
    }

    /// <summary>
    /// 格子级 BFS（跨层）：以具体格子（含层）为起点，结合地图边界/障碍物/层差规则求实际可达范围。
    /// 邻居判定：四方向相邻列中，与当前格子层差 ≤ 1 的格子视为可连接（L2 ↔ L1 / L3 可连，L1 ↔ L3 不可连）。
    /// 形状处理：BFS 扩展阶段不做形状裁剪（允许绕行路径走出形状区域再绕回），
    /// 最终结果再按 RangeType 形状（Straight 十字 / Diamond 菱形）过滤，保留特殊范围的视觉语义。
    /// </summary>
    public static HashSet<CellManager> CalculateReachableCells(
        CellManager startCell,
        int maxRange,
        GridManager gridManager,
        RangeType type = RangeType.Diamond,
        bool blockByMonster = true,
        bool useAbilityTraversal = false)
    {
        HashSet<CellManager> reachable = new HashSet<CellManager>();
        if (gridManager == null || startCell == null) return reachable;

        var (startX, startZ) = gridManager.GetCellGridPosition(startCell);
        if (!gridManager.IsValidGridPosition(startX, startZ)) return reachable;

        if (maxRange <= 0 || type == RangeType.Point)
        {
            reachable.Add(startCell);
            return reachable;
        }

        // 形状限定（最终结果过滤用）：Straight = 十字，Diamond = 菱形
        HashSet<Vector2Int> rawShape = GetRawRange(new Vector2Int(startX, startZ), maxRange, type);

        Queue<(CellManager cell, int step)> queue = new Queue<(CellManager, int)>();
        queue.Enqueue((startCell, 0));
        reachable.Add(startCell);
        HashSet<CellManager> traversed = new HashSet<CellManager> { startCell };

        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        // BFS 扩展阶段不做形状裁剪：绕行路径允许走出形状区域再绕回
        while (queue.Count > 0)
        {
            var (current, step) = queue.Dequeue();
            if (step >= maxRange) continue;

            var (cx, cz) = gridManager.GetCellGridPosition(current);

            foreach (var dir in directions)
            {
                Vector2Int nextCol = new Vector2Int(cx + dir.x, cz + dir.y);

                if (!gridManager.IsValidGridPosition(nextCol.x, nextCol.y)) continue;

                // 遍历邻居列的所有层，只连接层差 ≤ 1 的格子
                foreach (CellManager neighbor in gridManager.GetCellManagersInColumn(nextCol.x, nextCol.y))
                {
                    if (neighbor == null) continue;
                    if (neighbor.IsLocked) continue; // 状态锁：不可移动上去、不在范围判定内
                    if (!CanConnectAcrossLayers(gridManager, current, neighbor)) continue;
                    if (!traversed.Add(neighbor)) continue;

                    // 穿过敌人：敌人格可以继续扩展路径，但不会成为可停留的落点。
                    if (blockByMonster && neighbor.HasMonsterInside)
                    {
                        if (useAbilityTraversal && AbilityCore.CanTraverseCell(neighbor))
                        {
                            if (AbilityCore.CanLandOnCell(neighbor))
                            {
                                reachable.Add(neighbor);
                            }

                            queue.Enqueue((neighbor, step + 1));
                        }

                        continue;
                    }

                    reachable.Add(neighbor);
                    queue.Enqueue((neighbor, step + 1));
                }
            }
        }

        // 最终结果按形状过滤，保留特殊范围视觉语义（Straight 十字 / Diamond 菱形）。
        // 层差规则完全由 BFS 扩展阶段逐步判定（相邻格层差 ≤ 1 才可连接）：
        // 链式可达的跨层格（如 L3 → L2 → L1）属于范围内，不按起点层一刀切。
        reachable.RemoveWhere(cell =>
        {
            if (cell == null) return true;
            var (cx, cz) = gridManager.GetCellGridPosition(cell);
            return !rawShape.Contains(new Vector2Int(cx, cz));
        });

        return reachable;
    }

    /// <summary>
    /// BFS 结合地图边界/障碍物后求实际可达范围（列级兼容旧版：仅取每列顶层格子）
    /// </summary>
    public static HashSet<Vector2Int> CalculateReachableGrids(
        Vector2Int startPos,
        int maxRange,
        GridManager gridManager,
        RangeType type = RangeType.Diamond,
        bool blockByMonster = true)
    {
        HashSet<Vector2Int> reachable = new HashSet<Vector2Int>();
        if (gridManager == null || !gridManager.IsValidGridPosition(startPos.x, startPos.y))
            return reachable;

        if (maxRange <= 0 || type == RangeType.Point)
        {
            reachable.Add(startPos);
            return reachable;
        }

        // 获取形状限定下的理论范围
        HashSet<Vector2Int> rawShape = GetRawRange(startPos, maxRange, type);

        Queue<(Vector2Int pos, int step)> queue = new Queue<(Vector2Int, int)>();
        queue.Enqueue((startPos, 0));
        reachable.Add(startPos);

        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        while (queue.Count > 0)
        {
            var (current, step) = queue.Dequeue();
            if (step >= maxRange) continue;

            foreach (var dir in directions)
            {
                Vector2Int next = current + dir;

                if (!rawShape.Contains(next)) continue;
                if (!gridManager.IsValidGridPosition(next.x, next.y)) continue;
                if (reachable.Contains(next)) continue;

                CellManager cell = gridManager.GetCellManagerAt(next.x, next.y);
                if (cell != null && cell.IsLocked) continue; // 状态锁：不在范围判定内
                if (blockByMonster && cell != null && cell.HasMonsterInside) continue;

                reachable.Add(next);
                queue.Enqueue((next, step + 1));
            }
        }

        return reachable;
    }

    /// <summary>
    /// 从格子级 rangeSet 中提取"边沿格"（格子级版本）。
    /// 规则与列级版本一致：Straight 取四方向距离 == distance 的最外沿格；Diamond 取曼哈顿距离 == distance 的外圈；Point 返回全集。
    /// 被地图裁剪/阻挡的方向不向内回退，避免把更近的格子误判为最外沿。
    /// </summary>
    public static HashSet<CellManager> GetEdgeCells(
        HashSet<CellManager> rangeSet,
        CellManager center,
        GridManager gridManager,
        RangeType rangeType,
        int distance)
    {
        HashSet<CellManager> edges = new HashSet<CellManager>();

        if (rangeSet == null || rangeSet.Count == 0) return edges;
        if (gridManager == null) return edges;

        var (cx, cz) = gridManager.GetCellGridPosition(center);

        switch (rangeType)
        {
            case RangeType.Point:
                edges.UnionWith(rangeSet);
                break;

            case RangeType.Straight:
                // 只取距离 == distance 的最外沿格：该方向第 distance 格被地图裁剪/阻挡时，
                // 该方向不产生边沿格（不向内回退，避免把更近的格子误判为最外沿）。
                Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
                foreach (var dir in dirs)
                {
                    Vector2Int candidate = new Vector2Int(cx + dir.x * distance, cz + dir.y * distance);
                    foreach (CellManager cell in rangeSet)
                    {
                        var (cellX, cellZ) = gridManager.GetCellGridPosition(cell);
                        if (cellX == candidate.x && cellZ == candidate.y)
                            edges.Add(cell); // 同一列可能多层，全部加入，保证任意层悬停都能命中
                    }
                }
                break;

            case RangeType.Diamond:
                // 只取曼哈顿距离 == distance 的最外圈；外圈被地图裁剪时该圈不产生边沿格，
                // 不向内回退（避免把更近的圈误判为最外沿）。
                foreach (CellManager cell in rangeSet)
                {
                    var (cellX, cellZ) = gridManager.GetCellGridPosition(cell);
                    int manhattan = Mathf.Abs(cellX - cx) + Mathf.Abs(cellZ - cz);
                    if (manhattan == distance)
                        edges.Add(cell);
                }
                break;
        }

        return edges;
    }

    /// <summary>
    /// 从 rangeSet 中提取"边沿格"，用于 TargetSelectMode.EdgeOnly 的落点验证。
    ///
    /// 规则：
    ///   Straight — 四个方向（上/下/左/右）各取距离 == distance 的最外沿格，最多返回 4 格；
    ///              该方向被地图裁剪/阻挡时不向内回退，不产生边沿格。
    ///   Diamond  — 取满足 |dx|+|dy| == distance 的格子（菱形最外圈），外圈被裁剪时不向内回退。
    ///   Point    — 返回 rangeSet 本身（只有一格，就是玩家所在格）。
    ///
    /// 传入 rangeSet 而非重新计算，保证边沿格与实际高亮范围一致（地图裁剪后的结果）。
    /// </summary>
    public static HashSet<Vector2Int> GetEdgeCells(
        HashSet<Vector2Int> rangeSet,
        Vector2Int center,
        RangeType rangeType,
        int distance)
    {
        HashSet<Vector2Int> edges = new HashSet<Vector2Int>();

        if (rangeSet == null || rangeSet.Count == 0) return edges;

        switch (rangeType)
        {
            case RangeType.Point:
                // Point 范围只有一格，直接返回
                edges.UnionWith(rangeSet);
                break;

            case RangeType.Straight:
                // 只取距离 == distance 的最外沿格；该方向被地图裁剪/阻挡时该方向不产生边沿格
                Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
                foreach (var dir in dirs)
                {
                    Vector2Int candidate = center + dir * distance;
                    if (rangeSet.Contains(candidate))
                        edges.Add(candidate);
                }
                break;

            case RangeType.Diamond:
                // 菱形外圈：满足曼哈顿距离 == distance 的格子；外圈被裁剪时不向内回退
                foreach (var pos in rangeSet)
                {
                    int manhattan = Mathf.Abs(pos.x - center.x) + Mathf.Abs(pos.y - center.y);
                    if (manhattan == distance)
                        edges.Add(pos);
                }
                break;
        }

        return edges;
    }
}
