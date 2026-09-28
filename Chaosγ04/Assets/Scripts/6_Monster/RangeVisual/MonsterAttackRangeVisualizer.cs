using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 按住 Alt 时显示所有怪物当前的移动范围，松开后立即清除。
/// </summary>
[DisallowMultipleComponent]
public class MonsterAttackRangeVisualizer : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("格子视觉管理器；未指定时自动从场景中查找。")]
    [SerializeField] private GridVisualManager visualManager;

    [Tooltip("怪物移动范围使用的 GridStyleData 资产。")]
    [SerializeField] private GridStyleData movementStyle;

    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    private readonly HashSet<CellManager> displayedCells = new HashSet<CellManager>();
    private readonly HashSet<CellManager> characterCells = new HashSet<CellManager>();
    private bool isShowing;
    private bool warnedMissingStyle;

    public bool IsShowing => isShowing;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
    }

    private void OnDisable()
    {
        ClearDisplayedRange();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            ClearDisplayedRange();
        }
    }

    private void Update()
    {
        if (!IsAltPressed())
        {
            ClearDisplayedRange();
            return;
        }

        if (!ResolveReferences())
        {
            ClearDisplayedRange();
            return;
        }

        if (movementStyle == null)
        {
            ClearDisplayedRange();
            if (!warnedMissingStyle)
            {
                warnedMissingStyle = true;
                Debug.LogWarning("[MonsterAttackRangeVisualizer] 未指定移动范围样式 GridStyleData。");
            }
            return;
        }

        warnedMissingStyle = false;
        RefreshDisplayedRange(visualManager.GridManager);
    }

    private bool ResolveReferences()
    {
        if (visualManager == null)
        {
            visualManager = FindFirstObjectByType<GridVisualManager>();
        }

        if (visualManager == null || visualManager.GridManager == null)
        {
            return false;
        }

        visualManager.GridManager.EnsureGridSystemInitialized();
        return true;
    }

    private static bool IsAltPressed()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null &&
               (keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed);
    }

    private void RefreshDisplayedRange(GridManager gridManager)
    {
        HashSet<CellManager> nextCells = new HashSet<CellManager>();
        HashSet<CellManager> nextCharacterCells = new HashSet<CellManager>();
        BuildCombinedRange(gridManager, nextCells, nextCharacterCells);

        if (isShowing &&
            nextCells.SetEquals(displayedCells) &&
            nextCharacterCells.SetEquals(characterCells))
        {
            return;
        }

        if (isShowing)
        {
            GridHoverController.Instance?.ClearContext(this);
            visualManager.ClearRangeHighlight(displayedCells);
        }

        displayedCells.Clear();
        displayedCells.UnionWith(nextCells);
        characterCells.Clear();
        characterCells.UnionWith(nextCharacterCells);
        isShowing = displayedCells.Count > 0;

        if (isShowing)
        {
            GridHoverController.Instance?.SetContext(
                displayedCells,
                center: null,
                style: movementStyle,
                isPoint: false,
                validTargets: displayedCells,
                aoeMode: false,
                validEntityTargets: displayedCells,
                quarterCircleMode: false,
                hoverMode: GridHoverInteractionMode.Normal,
                characterCells: characterCells,
                owner: this);

            visualManager.SetReachablePatternColors(
                displayedCells,
                characterCells,
                movementStyle,
                isRuntime: Application.isPlaying);
        }
    }

    private void BuildCombinedRange(
        GridManager gridManager,
        HashSet<CellManager> combinedRange,
        HashSet<CellManager> combinedCharacterCells)
    {
        combinedRange.Clear();
        combinedCharacterCells.Clear();

        MonsterIdentitySystem monsterSystem = MonsterIdentitySystem.Instance;
        if (monsterSystem == null)
        {
            return;
        }

        List<MonsterIdentityManager> monsters =
            new List<MonsterIdentityManager>(monsterSystem.GetAllMonsters());
        if (monsters.Count == 0)
        {
            return;
        }

        Dictionary<MonsterIdentityManager, CellManager> monsterCells =
            FindMonsterCells(gridManager);
        CellManager playerCell = FindPlayerCell(gridManager);

        foreach (MonsterIdentityManager monster in monsters)
        {
            if (monster == null || !monster.gameObject.activeInHierarchy)
            {
                continue;
            }

            MonsterMoveAction moveAction = monster.GetComponent<MonsterMoveAction>();
            if (moveAction == null || moveAction.mobility <= 0)
            {
                continue;
            }

            if (!monsterCells.TryGetValue(monster, out CellManager startCell) || startCell == null)
            {
                continue;
            }

            combinedCharacterCells.Add(startCell);
            combinedRange.UnionWith(
                CalculateReachableCells(
                    gridManager,
                    startCell,
                    moveAction.mobility,
                    monster,
                    playerCell));
        }
    }

    private static Dictionary<MonsterIdentityManager, CellManager> FindMonsterCells(
        GridManager gridManager)
    {
        Dictionary<MonsterIdentityManager, CellManager> result =
            new Dictionary<MonsterIdentityManager, CellManager>();

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
                        if (monster == null || !monster.gameObject.activeInHierarchy)
                        {
                            continue;
                        }

                        if (!result.ContainsKey(monster))
                        {
                            result.Add(monster, cell);
                        }
                    }
                }
            }
        }

        return result;
    }

    private static CellManager FindPlayerCell(GridManager gridManager)
    {
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

    private static HashSet<CellManager> CalculateReachableCells(
        GridManager gridManager,
        CellManager startCell,
        int maxSteps,
        MonsterIdentityManager movingMonster,
        CellManager playerCell)
    {
        HashSet<CellManager> reachable = new HashSet<CellManager> { startCell };
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

                    if (!neighbor.CanMonsterTraverse(movingMonster) || neighbor == playerCell)
                    {
                        continue;
                    }

                    if (!RangeSystem.CanConnectAcrossLayers(gridManager, currentCell, neighbor))
                    {
                        continue;
                    }

                    if (HasOtherActiveMonster(neighbor, movingMonster))
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

    private void ClearDisplayedRange()
    {
        if (isShowing)
        {
            GridHoverController.Instance?.ClearContext(this);
        }

        if (isShowing && visualManager != null && visualManager.GridManager != null)
        {
            visualManager.ClearRangeHighlight(displayedCells);
        }

        displayedCells.Clear();
        characterCells.Clear();
        isShowing = false;
    }
}
