using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Shared cell graph helpers for monster movement, targeting, and leave decisions.
/// All distances are calculated on the layered cell graph, never on the flat XZ grid.
/// </summary>
public static class MonsterPathfinding
{
    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    public static CellManager FindMonsterCell(GridManager gridManager, MonsterIdentityManager monster)
    {
        if (gridManager == null || monster == null)
        {
            return null;
        }

        for (int x = 0; x < gridManager.width; x++)
        {
            for (int z = 0; z < gridManager.height; z++)
            {
                foreach (CellManager cell in gridManager.GetCellManagersInColumn(x, z))
                {
                    if (cell != null && cell.GetMonstersInside().Contains(monster))
                    {
                        return cell;
                    }
                }
            }
        }

        return null;
    }

    public static CellManager FindPlayerCell(GridManager gridManager)
    {
        if (gridManager == null)
        {
            return null;
        }

        for (int x = 0; x < gridManager.width; x++)
        {
            for (int z = 0; z < gridManager.height; z++)
            {
                foreach (CellManager cell in gridManager.GetCellManagersInColumn(x, z))
                {
                    if (cell != null && cell.IsPlayerInside)
                    {
                        return cell;
                    }
                }
            }
        }

        return null;
    }

    public static Dictionary<MonsterIdentityManager, CellManager> FindAllMonsterCells(
        GridManager gridManager)
    {
        Dictionary<MonsterIdentityManager, CellManager> result =
            new Dictionary<MonsterIdentityManager, CellManager>();

        if (gridManager == null)
        {
            return result;
        }

        for (int x = 0; x < gridManager.width; x++)
        {
            for (int z = 0; z < gridManager.height; z++)
            {
                foreach (CellManager cell in gridManager.GetCellManagersInColumn(x, z))
                {
                    if (cell == null)
                    {
                        continue;
                    }

                    foreach (MonsterIdentityManager monster in cell.GetMonstersInside())
                    {
                        if (monster != null &&
                            monster.gameObject.activeInHierarchy &&
                            !result.ContainsKey(monster))
                        {
                            result.Add(monster, cell);
                        }
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Cells reachable within <paramref name="maxSteps"/> using the actual layered graph.
    /// This is the movement/surveillance range used by MonsterLeaveAction and MoveAction.
    /// </summary>
    public static HashSet<CellManager> CalculateReachableCells(
        GridManager gridManager,
        MonsterIdentityManager movingMonster,
        CellManager startCell,
        int maxSteps,
        bool blockByMonsters,
        bool includePlayerCell)
    {
        HashSet<CellManager> reachable = new HashSet<CellManager>();
        if (gridManager == null || startCell == null || maxSteps < 0)
        {
            return reachable;
        }

        gridManager.EnsureGridSystemInitialized();

        reachable.Add(startCell);
        Queue<(CellManager cell, int steps)> queue = new Queue<(CellManager cell, int steps)>();
        queue.Enqueue((startCell, 0));

        while (queue.Count > 0)
        {
            (CellManager currentCell, int steps) = queue.Dequeue();
            if (steps >= maxSteps)
            {
                continue;
            }

            var (currentX, currentZ) = gridManager.GetCellGridPosition(currentCell);

            foreach (Vector2Int direction in Directions)
            {
                Vector2Int neighborColumn =
                    new Vector2Int(currentX + direction.x, currentZ + direction.y);

                if (!gridManager.IsValidGridPosition(neighborColumn.x, neighborColumn.y))
                {
                    continue;
                }

                foreach (CellManager neighbor in gridManager.GetCellManagersInColumn(
                             neighborColumn.x,
                             neighborColumn.y))
                {
                    if (neighbor == null || reachable.Contains(neighbor))
                    {
                        continue;
                    }

                    if (!RangeSystem.CanConnectAcrossLayers(gridManager, currentCell, neighbor))
                    {
                        continue;
                    }

                    if (!neighbor.CanMonsterTraverse(movingMonster))
                    {
                        continue;
                    }

                    if (!includePlayerCell && neighbor.IsPlayerInside)
                    {
                        continue;
                    }

                    if (blockByMonsters && HasOtherActiveMonster(neighbor, movingMonster))
                    {
                        continue;
                    }

                    reachable.Add(neighbor);
                    queue.Enqueue((neighbor, steps + 1));
                }
            }
        }

        return reachable;
    }

    /// <summary>
    /// A* on individual cells. Layers more than one level apart are not connected.
    /// Occupied start/target cells are allowed so paths can end on a hostile creature's cell.
    /// Set <paramref name="ignoreUnitBlockers"/> to true when planning an ideal route toward
    /// a target: other units no longer block the route, while terrain and layer rules still apply.
    /// </summary>
    public static List<CellManager> FindPath(
        GridManager gridManager,
        MonsterIdentityManager movingMonster,
        CellManager startCell,
        CellManager targetCell,
        MonsterIdentityManager ignoredMonster = null,
        bool ignoreUnitBlockers = false)
    {
        return FindPathInternal(
            gridManager,
            movingMonster,
            startCell,
            targetCell,
            ignoredMonster,
            ignoreUnitBlockers,
            allowPartialPath: false);
    }

    /// <summary>
    /// Finds the shortest route to the target while respecting unit occupancy. When the target
    /// cannot be reached, returns the shortest path to the reachable cell that is closest to it.
    /// This is the movement fallback that prevents a monster from freezing behind a blocker.
    /// </summary>
    public static List<CellManager> FindPathToClosestReachableCell(
        GridManager gridManager,
        MonsterIdentityManager movingMonster,
        CellManager startCell,
        CellManager targetCell,
        MonsterIdentityManager ignoredMonster = null)
    {
        return FindPathInternal(
            gridManager,
            movingMonster,
            startCell,
            targetCell,
            ignoredMonster,
            ignoreUnitBlockers: false,
            allowPartialPath: true);
    }

    private static List<CellManager> FindPathInternal(
        GridManager gridManager,
        MonsterIdentityManager movingMonster,
        CellManager startCell,
        CellManager targetCell,
        MonsterIdentityManager ignoredMonster,
        bool ignoreUnitBlockers,
        bool allowPartialPath)
    {
        if (gridManager == null || startCell == null || targetCell == null)
        {
            return null;
        }

        gridManager.EnsureGridSystemInitialized();

        if (startCell == targetCell)
        {
            return new List<CellManager> { startCell };
        }

        List<PathNode> openSet = new List<PathNode>();
        HashSet<CellManager> closedSet = new HashSet<CellManager>();
        Dictionary<CellManager, PathNode> nodeLookup = new Dictionary<CellManager, PathNode>();

        PathNode startNode = new PathNode(startCell);
        openSet.Add(startNode);
        nodeLookup[startCell] = startNode;

        var (targetX, targetZ) = gridManager.GetCellGridPosition(targetCell);
        startNode.hCost = Heuristic(gridManager, startCell, targetX, targetZ);
        PathNode bestApproachNode = startNode;

        while (openSet.Count > 0)
        {
            PathNode currentNode = openSet[0];
            for (int i = 1; i < openSet.Count; i++)
            {
                if (openSet[i].FCost < currentNode.FCost ||
                    (openSet[i].FCost == currentNode.FCost && openSet[i].hCost < currentNode.hCost))
                {
                    currentNode = openSet[i];
                }
            }

            openSet.Remove(currentNode);
            closedSet.Add(currentNode.cell);

            if (currentNode.cell == targetCell)
            {
                return RetracePath(startNode, currentNode);
            }

            if (allowPartialPath &&
                IsBetterApproachNode(gridManager, currentNode, bestApproachNode))
            {
                bestApproachNode = currentNode;
            }

            var (currentX, currentZ) = gridManager.GetCellGridPosition(currentNode.cell);

            foreach (Vector2Int direction in Directions)
            {
                Vector2Int neighborColumn =
                    new Vector2Int(currentX + direction.x, currentZ + direction.y);

                if (!gridManager.IsValidGridPosition(neighborColumn.x, neighborColumn.y))
                {
                    continue;
                }

                foreach (CellManager neighbor in gridManager.GetCellManagersInColumn(
                             neighborColumn.x,
                             neighborColumn.y))
                {
                    if (neighbor == null || closedSet.Contains(neighbor))
                    {
                        continue;
                    }

                    if (!RangeSystem.CanConnectAcrossLayers(gridManager, currentNode.cell, neighbor))
                    {
                        continue;
                    }

                    if (IsBlockedForPath(
                            movingMonster,
                            neighbor,
                            startCell,
                            targetCell,
                            ignoredMonster,
                            ignoreUnitBlockers))
                    {
                        continue;
                    }

                    int newCostToNeighbor = currentNode.gCost + 1;

                    PathNode neighborNode;
                    if (!nodeLookup.TryGetValue(neighbor, out neighborNode))
                    {
                        neighborNode = new PathNode(neighbor);
                        nodeLookup[neighbor] = neighborNode;
                    }

                    if (newCostToNeighbor >= neighborNode.gCost &&
                        openSet.Contains(neighborNode))
                    {
                        continue;
                    }

                    neighborNode.gCost = newCostToNeighbor;
                    neighborNode.hCost =
                        Heuristic(gridManager, neighbor, targetX, targetZ);
                    neighborNode.parent = currentNode;

                    if (!openSet.Contains(neighborNode))
                    {
                        openSet.Add(neighborNode);
                    }
                }
            }
        }

        if (allowPartialPath &&
            bestApproachNode != null &&
            bestApproachNode.cell != startCell)
        {
            return RetracePath(startNode, bestApproachNode);
        }

        return null;
    }

    /// <summary>
    /// Returns the number of path edges between two cells, or int.MaxValue when unreachable.
    /// </summary>
    public static int GetPathDistance(
        GridManager gridManager,
        MonsterIdentityManager movingMonster,
        CellManager startCell,
        CellManager targetCell,
        MonsterIdentityManager ignoredMonster = null,
        bool ignoreUnitBlockers = false)
    {
        List<CellManager> path = FindPath(
            gridManager,
            movingMonster,
            startCell,
            targetCell,
            ignoredMonster,
            ignoreUnitBlockers);
        return path == null ? int.MaxValue : path.Count - 1;
    }

    /// <summary>
    /// Returns the shortest distance across the layered cell graph while ignoring
    /// terrain traversal rules and unit occupancy. Leave decisions use this to
    /// measure global spacing from threats without requiring those threats to
    /// be able to reach the candidate cell.
    /// </summary>
    public static int GetGlobalPathDistance(
        GridManager gridManager,
        CellManager startCell,
        CellManager targetCell)
    {
        if (gridManager == null || startCell == null || targetCell == null)
        {
            return int.MaxValue;
        }

        gridManager.EnsureGridSystemInitialized();

        if (startCell == targetCell)
        {
            return 0;
        }

        Queue<(CellManager cell, int steps)> queue = new Queue<(CellManager cell, int steps)>();
        HashSet<CellManager> visited = new HashSet<CellManager> { startCell };
        queue.Enqueue((startCell, 0));

        while (queue.Count > 0)
        {
            (CellManager currentCell, int steps) = queue.Dequeue();
            var (currentX, currentZ) = gridManager.GetCellGridPosition(currentCell);

            foreach (Vector2Int direction in Directions)
            {
                Vector2Int neighborColumn =
                    new Vector2Int(currentX + direction.x, currentZ + direction.y);

                if (!gridManager.IsValidGridPosition(neighborColumn.x, neighborColumn.y))
                {
                    continue;
                }

                foreach (CellManager neighbor in gridManager.GetCellManagersInColumn(
                             neighborColumn.x,
                             neighborColumn.y))
                {
                    if (neighbor == null || visited.Contains(neighbor))
                    {
                        continue;
                    }

                    if (!RangeSystem.CanConnectAcrossLayers(gridManager, currentCell, neighbor))
                    {
                        continue;
                    }

                    if (neighbor == targetCell)
                    {
                        return steps + 1;
                    }

                    visited.Add(neighbor);
                    queue.Enqueue((neighbor, steps + 1));
                }
            }
        }

        return int.MaxValue;
    }

    private static bool IsBlockedForPath(
        MonsterIdentityManager movingMonster,
        CellManager cell,
        CellManager startCell,
        CellManager targetCell,
        MonsterIdentityManager ignoredMonster,
        bool ignoreUnitBlockers)
    {
        if (cell == startCell || cell == targetCell)
        {
            return false;
        }

        if (cell == null || !cell.CanMonsterTraverse(movingMonster))
        {
            return true;
        }

        if (ignoreUnitBlockers)
        {
            return false;
        }

        if (cell.IsPlayerInside)
        {
            return true;
        }

        foreach (MonsterIdentityManager monster in cell.GetMonstersInside())
        {
            if (monster == null || monster == movingMonster || monster == ignoredMonster)
            {
                continue;
            }

            if (monster.gameObject.activeInHierarchy)
            {
                return true;
            }
        }

        return false;
    }

    private static int Heuristic(
        GridManager gridManager,
        CellManager cell,
        int targetX,
        int targetZ)
    {
        var (cellX, cellZ) = gridManager.GetCellGridPosition(cell);
        return Mathf.Abs(cellX - targetX) + Mathf.Abs(cellZ - targetZ);
    }

    private static bool IsBetterApproachNode(
        GridManager gridManager,
        PathNode candidate,
        PathNode currentBest)
    {
        if (candidate == null)
        {
            return false;
        }

        if (currentBest == null)
        {
            return true;
        }

        if (candidate.hCost != currentBest.hCost)
        {
            return candidate.hCost < currentBest.hCost;
        }

        if (candidate.gCost != currentBest.gCost)
        {
            return candidate.gCost < currentBest.gCost;
        }

        var (candidateX, candidateZ) =
            gridManager.GetCellGridPosition(candidate.cell);
        var (bestX, bestZ) =
            gridManager.GetCellGridPosition(currentBest.cell);
        if (candidateX != bestX)
        {
            return candidateX < bestX;
        }

        return candidateZ < bestZ;
    }

    private static bool HasOtherActiveMonster(
        CellManager cell,
        MonsterIdentityManager movingMonster)
    {
        foreach (MonsterIdentityManager monster in cell.GetMonstersInside())
        {
            if (monster == null || monster == movingMonster)
            {
                continue;
            }

            if (monster.gameObject.activeInHierarchy)
            {
                return true;
            }
        }

        return false;
    }

    private static List<CellManager> RetracePath(PathNode startNode, PathNode endNode)
    {
        List<CellManager> path = new List<CellManager>();
        PathNode current = endNode;
        while (current != null)
        {
            path.Add(current.cell);
            current = current.parent;
        }

        path.Reverse();
        return path;
    }

    private sealed class PathNode
    {
        public readonly CellManager cell;
        public int gCost;
        public int hCost;
        public PathNode parent;

        public int FCost => gCost + hCost;

        public PathNode(CellManager cell)
        {
            this.cell = cell;
        }
    }
}
