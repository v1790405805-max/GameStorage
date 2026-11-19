using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 按住 Alt 时显示所有怪物的移动范围；点击怪物时显示该怪物的移动范围。
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
    private readonly RaycastHit[] pointerRaycastHits = new RaycastHit[64];
    private bool isShowing;
    private bool warnedMissingStyle;
    private MonsterIdentityManager selectedMonster;
    private MonsterIdentityManager pressedMonster;
    private PlayerMoveController playerMoveController;

    public bool IsShowing => isShowing;
    public MonsterIdentityManager SelectedMonster => selectedMonster;

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
        selectedMonster = null;
        pressedMonster = null;
        ClearDisplayedRange();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            selectedMonster = null;
            pressedMonster = null;
            ClearDisplayedRange();
        }
    }

    private void Update()
    {
        if (IsPlayerMovementFlowActive())
        {
            pressedMonster = null;
            selectedMonster = null;
            ClearDisplayedRange();
            return;
        }

        HandleSelectionInput();

        bool showAllMonsters = IsAltPressed();
        if (!showAllMonsters && !IsSelectedMonsterAvailable())
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
        RefreshDisplayedRange(visualManager.GridManager, showAllMonsters);
    }

    private bool ResolveReferences()
    {
        if (visualManager == null)
        {
            visualManager = FindFirstObjectByType<GridVisualManager>();
        }

        if (playerMoveController == null)
        {
            playerMoveController = FindFirstObjectByType<PlayerMoveController>();
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

    private void HandleSelectionInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            pressedMonster = null;
            return;
        }

        if (IsPlayerMovementFlowActive())
        {
            pressedMonster = null;
            return;
        }

        if (mouse.rightButton.wasPressedThisFrame)
        {
            ClearSelectedMonster();
            return;
        }

        if (mouse.leftButton.wasPressedThisFrame)
        {
            if (IsPointerOverUI())
            {
                ClearSelectedMonster();
                return;
            }

            pressedMonster = FindMonsterUnderPointer();
            if (pressedMonster == null)
            {
                ClearSelectedMonster();
            }
        }

        if (!mouse.leftButton.wasReleasedThisFrame)
        {
            return;
        }

        MonsterIdentityManager releaseCandidate = pressedMonster;
        pressedMonster = null;
        if (releaseCandidate == null)
        {
            return;
        }

        MonsterIdentityManager releasedMonster =
            IsPointerOverUI() ? null : FindMonsterUnderPointer();
        if (releasedMonster == releaseCandidate)
        {
            SelectMonster(releaseCandidate);
        }
        else
        {
            ClearSelectedMonster();
        }
    }

    private MonsterIdentityManager FindMonsterUnderPointer()
    {
        if (!ResolveReferences())
        {
            return null;
        }

        Camera interactionCamera = MainCameraAppoint.Current;
        if (interactionCamera == null)
        {
            return null;
        }

        Ray ray = interactionCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        int hitCount = Physics.RaycastNonAlloc(
            ray,
            pointerRaycastHits,
            1000f,
            ~0,
            QueryTriggerInteraction.Collide);

        MonsterIdentityManager closestDirectMonster = null;
        float closestMonsterDistance = float.MaxValue;
        CellManager closestCell = null;
        float closestCellDistance = float.MaxValue;
        Vector3 closestCellPoint = Vector3.zero;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = pointerRaycastHits[i];
            if (hit.collider == null)
            {
                continue;
            }

            MonsterIdentityManager directMonster =
                hit.collider.GetComponentInParent<MonsterIdentityManager>();
            if (IsSelectableMonster(directMonster) &&
                hit.distance < closestMonsterDistance)
            {
                closestMonsterDistance = hit.distance;
                closestDirectMonster = directMonster;
            }

            CellManager hitCell = hit.collider.GetComponentInParent<CellManager>();
            if (hitCell != null && hit.distance < closestCellDistance)
            {
                closestCellDistance = hit.distance;
                closestCell = hitCell;
                closestCellPoint = hit.point;
            }
        }

        // 怪物碰撞体通常位于格子上方；若格子更近，则不能选中被该格遮挡的后方怪物。
        if (closestDirectMonster != null &&
            (closestCell == null || closestMonsterDistance <= closestCellDistance))
        {
            return closestDirectMonster;
        }

        if (closestCell == null)
        {
            closestCell = GridHoverController.Instance?.CurrentHoverCell;
        }

        if (closestCell == null)
        {
            return closestDirectMonster;
        }

        MonsterIdentityManager closestMonster = null;
        float closestDistance = float.MaxValue;
        foreach (MonsterIdentityManager monster in closestCell.GetMonstersInside())
        {
            if (!IsSelectableMonster(monster))
            {
                continue;
            }

            float distance = (monster.transform.position - closestCellPoint).sqrMagnitude;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestMonster = monster;
            }
        }

        return closestMonster;
    }

    private static bool IsSelectableMonster(MonsterIdentityManager monster)
    {
        if (monster == null || !monster.gameObject.activeInHierarchy)
        {
            return false;
        }

        MonsterMoveAction moveAction = monster.GetComponent<MonsterMoveAction>();
        return moveAction != null && moveAction.Mobility > 0;
    }

    private static bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private void SelectMonster(MonsterIdentityManager monster)
    {
        if (monster == null ||
            selectedMonster == monster ||
            IsPlayerMovementFlowActive())
        {
            return;
        }

        ClearDisplayedRange();
        selectedMonster = monster;
    }

    private bool IsPlayerMovementFlowActive()
    {
        if (playerMoveController == null)
        {
            playerMoveController = FindFirstObjectByType<PlayerMoveController>();
        }

        return playerMoveController != null && playerMoveController.IsMovementFlowActive;
    }

    private void ClearSelectedMonster()
    {
        selectedMonster = null;
        pressedMonster = null;

        if (!IsAltPressed())
        {
            ClearDisplayedRange();
        }
    }

    private bool IsSelectedMonsterAvailable()
    {
        if (IsSelectableMonster(selectedMonster))
        {
            return true;
        }

        selectedMonster = null;
        return false;
    }

    private void RefreshDisplayedRange(GridManager gridManager, bool showAllMonsters)
    {
        HashSet<CellManager> nextCells = new HashSet<CellManager>();
        HashSet<CellManager> nextCharacterCells = new HashSet<CellManager>();
        BuildDisplayedRange(gridManager, showAllMonsters, nextCells, nextCharacterCells);

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

    private void BuildDisplayedRange(
        GridManager gridManager,
        bool showAllMonsters,
        HashSet<CellManager> displayedRange,
        HashSet<CellManager> displayedCharacterCells)
    {
        displayedRange.Clear();
        displayedCharacterCells.Clear();

        Dictionary<MonsterIdentityManager, CellManager> monsterCells = FindMonsterCells(gridManager);
        CellManager playerCell = FindPlayerCell(gridManager);

        if (showAllMonsters)
        {
            MonsterIdentitySystem monsterSystem = MonsterIdentitySystem.Instance;
            if (monsterSystem == null)
            {
                return;
            }

            foreach (MonsterIdentityManager monster in monsterSystem.GetAllMonsters())
            {
                if (monsterCells.TryGetValue(monster, out CellManager startCell))
                {
                    AddMonsterRange(
                        gridManager,
                        monster,
                        startCell,
                        playerCell,
                        displayedRange,
                        displayedCharacterCells);
                }
            }
            return;
        }

        if (selectedMonster != null &&
            monsterCells.TryGetValue(selectedMonster, out CellManager selectedStartCell))
        {
            AddMonsterRange(
                gridManager,
                selectedMonster,
                selectedStartCell,
                playerCell,
                displayedRange,
                displayedCharacterCells);
        }
    }

    private static void AddMonsterRange(
        GridManager gridManager,
        MonsterIdentityManager monster,
        CellManager startCell,
        CellManager playerCell,
        HashSet<CellManager> displayedRange,
        HashSet<CellManager> displayedCharacterCells)
    {
        if (!IsSelectableMonster(monster) || startCell == null)
        {
            return;
        }

        MonsterMoveAction moveAction = monster.GetComponent<MonsterMoveAction>();
        displayedCharacterCells.Add(startCell);
        displayedRange.UnionWith(
            CalculateReachableCells(
                gridManager,
                startCell,
                moveAction.Mobility,
                monster,
                playerCell));
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
