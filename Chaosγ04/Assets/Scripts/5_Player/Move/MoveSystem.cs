using System.Collections.Generic;
using UnityEngine;

public static class MoveSystem
{
    private class PathNode
    {
        public Vector2Int position;
        public int gCost;
        public int hCost;
        public int fCost => gCost + hCost;
        public PathNode parent;

        public PathNode(Vector2Int pos)
        {
            position = pos;
        }
    }

    private class PathCellNode
    {
        public CellManager cell;
        public int gCost;
        public int hCost;
        public int fCost => gCost + hCost;
        public PathCellNode parent;

        public PathCellNode(CellManager cell)
        {
            this.cell = cell;
        }
    }

    /// <summary>
    /// 格子级 A* 寻路（跨层）：节点为具体格子（含层）。
    /// 邻居判定：四方向相邻列中，与当前格子层差 ≤ 1 且属于 allowedCells 的格子可通行
    /// （L2 ↔ L1 / L3 可连，L1 ↔ L3 不可连）。
    /// </summary>
    public static List<CellManager> FindPathAStarCells(
        CellManager start, CellManager end,
        HashSet<CellManager> allowedCells, GridManager gridManager)
    {
        if (gridManager == null || start == null || end == null) return null;

        List<PathCellNode> openList = new List<PathCellNode>();
        HashSet<CellManager> closedList = new HashSet<CellManager>();

        PathCellNode startNode = new PathCellNode(start)
        {
            gCost = 0,
            hCost = GetCellManhattanDistance(gridManager, start, end)
        };

        openList.Add(startNode);

        while (openList.Count > 0)
        {
            PathCellNode currentNode = openList[0];
            for (int i = 1; i < openList.Count; i++)
            {
                if (openList[i].fCost < currentNode.fCost ||
                   (openList[i].fCost == currentNode.fCost && openList[i].hCost < currentNode.hCost))
                {
                    currentNode = openList[i];
                }
            }

            openList.Remove(currentNode);
            closedList.Add(currentNode.cell);

            if (currentNode.cell == end)
            {
                return RetraceCellPath(startNode, currentNode);
            }

            var (cx, cz) = gridManager.GetCellGridPosition(currentNode.cell);
            Vector2Int[] neighborCols =
            {
                new Vector2Int(cx, cz - 1),
                new Vector2Int(cx, cz + 1),
                new Vector2Int(cx - 1, cz),
                new Vector2Int(cx + 1, cz)
            };

            foreach (var neighborCol in neighborCols)
            {
                if (!gridManager.IsValidGridPosition(neighborCol.x, neighborCol.y)) continue;

                foreach (CellManager neighborCell in gridManager.GetCellManagersInColumn(neighborCol.x, neighborCol.y))
                {
                    if (neighborCell == null) continue;
                    if (closedList.Contains(neighborCell)) continue;
                    if (neighborCell.IsLocked) continue; // 状态锁：不可走上/穿过
                    if (allowedCells != null && !allowedCells.Contains(neighborCell)) continue;
                    if (neighborCell.HasMonsterInside) continue;
                    // 跨层连接规则：只相隔一层可连
                    if (!RangeSystem.CanConnectAcrossLayers(gridManager, currentNode.cell, neighborCell)) continue;

                    int newCost = currentNode.gCost + 1;
                    PathCellNode neighborNode = openList.Find(n => n.cell == neighborCell);

                    if (neighborNode == null)
                    {
                        neighborNode = new PathCellNode(neighborCell)
                        {
                            gCost = newCost,
                            hCost = GetCellManhattanDistance(gridManager, neighborCell, end),
                            parent = currentNode
                        };
                        openList.Add(neighborNode);
                    }
                    else if (newCost < neighborNode.gCost)
                    {
                        neighborNode.gCost = newCost;
                        neighborNode.parent = currentNode;
                    }
                }
            }
        }

        return null;
    }

    private static int GetCellManhattanDistance(GridManager gridManager, CellManager a, CellManager b)
    {
        var (ax, az) = gridManager.GetCellGridPosition(a);
        var (bx, bz) = gridManager.GetCellGridPosition(b);
        return Mathf.Abs(ax - bx) + Mathf.Abs(az - bz);
    }

    private static List<CellManager> RetraceCellPath(PathCellNode startNode, PathCellNode endNode)
    {
        List<CellManager> path = new List<CellManager>();
        PathCellNode currentNode = endNode;

        while (currentNode != null)
        {
            path.Add(currentNode.cell);
            currentNode = currentNode.parent;
        }

        path.Reverse();
        return path;
    }

    /// <summary>
    /// A* Ѱ·�㷨
    /// </summary>
    public static List<Vector2Int> FindPathAStar(Vector2Int start, Vector2Int end, HashSet<Vector2Int> allowedGrids, GridManager gridManager)
    {
        if (gridManager == null) return null;

        List<PathNode> openList = new List<PathNode>();
        HashSet<Vector2Int> closedList = new HashSet<Vector2Int>();

        PathNode startNode = new PathNode(start)
        {
            gCost = 0,
            hCost = GetManhattanDistance(start, end)
        };

        openList.Add(startNode);

        while (openList.Count > 0)
        {
            PathNode currentNode = openList[0];
            for (int i = 1; i < openList.Count; i++)
            {
                if (openList[i].fCost < currentNode.fCost ||
                   (openList[i].fCost == currentNode.fCost && openList[i].hCost < currentNode.hCost))
                {
                    currentNode = openList[i];
                }
            }

            openList.Remove(currentNode);
            closedList.Add(currentNode.position);

            if (currentNode.position == end)
            {
                return RetracePath(startNode, currentNode);
            }

            Vector2Int[] neighbors = {
                new Vector2Int(currentNode.position.x, currentNode.position.y - 1),
                new Vector2Int(currentNode.position.x, currentNode.position.y + 1),
                new Vector2Int(currentNode.position.x - 1, currentNode.position.y),
                new Vector2Int(currentNode.position.x + 1, currentNode.position.y)
            };

            foreach (var neighborPos in neighbors)
            {
                if (!gridManager.IsValidGridPosition(neighborPos.x, neighborPos.y)) continue;
                if (allowedGrids != null && !allowedGrids.Contains(neighborPos)) continue;
                if (closedList.Contains(neighborPos)) continue;

                CellManager neighborCell = gridManager.GetCellManagerAt(neighborPos.x, neighborPos.y);
                if (neighborCell != null && neighborCell.IsLocked) continue; // 状态锁：不可走上/穿过
                if (neighborCell != null && neighborCell.HasMonsterInside) continue;

                int newCost = currentNode.gCost + 1;
                PathNode neighborNode = openList.Find(n => n.position == neighborPos);

                if (neighborNode == null)
                {
                    neighborNode = new PathNode(neighborPos)
                    {
                        gCost = newCost,
                        hCost = GetManhattanDistance(neighborPos, end),
                        parent = currentNode
                    };
                    openList.Add(neighborNode);
                }
                else if (newCost < neighborNode.gCost)
                {
                    neighborNode.gCost = newCost;
                    neighborNode.parent = currentNode;
                }
            }
        }

        return null;
    }

    private static List<Vector2Int> RetracePath(PathNode startNode, PathNode endNode)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        PathNode currentNode = endNode;

        while (currentNode != null)
        {
            path.Add(currentNode.position);
            currentNode = currentNode.parent;
        }

        path.Reverse();
        return path;
    }

    public static int GetManhattanDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
}