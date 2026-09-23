using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CardDragController : MonoBehaviour
{
    public static CardDragController Instance { get; private set; }

    public bool IsDragging { get; private set; } = false;

    [Header("依赖引用（留空则自动查找）")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private GridVisualManager visualManager;
    [SerializeField] private Transform playerTransform;

    /// <summary>当前被高亮的卡牌范围格集合（供外部查询，格子级含层）。</summary>
    public HashSet<CellManager> CurrentHighlightedGrids { get; private set; }
        = new HashSet<CellManager>();

    private Vector2Int playerGridPos = new Vector2Int(-1, -1);
    /// <summary>玩家所在格（含层，用于卡牌范围起点）。</summary>
    private CellManager playerCell;
    /// <summary>最近一次鼠标射线实际命中的格子（跨层精确，用于落点校验）。</summary>
    private CellManager lastMouseHitCell;

    // ==================== 伤害悬停预览新增字段 ====================
    private CardData currentDraggingCard = null;
    private MonsterInfoUI currentHoveredMonsterUI = null;

    // ===================================================================
    // Unity 生命周期
    // ===================================================================

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (gridManager == null) gridManager = FindFirstObjectByType<GridManager>();
        if (visualManager == null) visualManager = FindFirstObjectByType<GridVisualManager>();
    }

    private void Update()
    {
        if (IsDragging && currentDraggingCard != null)
        {
            HandleMonsterHoverDuringDrag();
        }
    }

    // ===================================================================
    // 公开 API（由 CardUI 在拖拽事件中调用）
    // ===================================================================

    public void OnCardDragging(CardData data)
    {
        if (data == null || gridManager == null || visualManager == null) return;

        PlayerMoveController moveController = FindFirstObjectByType<PlayerMoveController>();
        if (moveController != null && moveController.IsMoving)
        {
            Debug.LogWarning("[CardDragController] 玩家正在移动中，禁止拖拽卡牌！");
            return;
        }

        IsDragging = true;
        currentDraggingCard = data;

        if (moveController != null)
        {
            moveController.IsUsingCard = true;
            moveController.ForceClearHoverState();
        }

        UpdatePlayerGridPosition();

        HashSet<CellManager> targetGrids = (data.rangeType == RangeType.Point)
            ? new HashSet<CellManager> { playerCell }
            : CalculateCardRangeGrids(playerCell, data.rangeDistance, data.rangeType);

        HashSet<CellManager> validTargetGrids = targetGrids;
        if (data.targetSelectMode == TargetSelectMode.EdgeOnly)
        {
            validTargetGrids = RangeSystem.GetEdgeCells(
                targetGrids, playerCell, gridManager, data.rangeType, data.rangeDistance);
        }

        bool isQuarterCircleMode = data.targetSelectMode == TargetSelectMode.AQuarterCircle;
        bool allowEmptyAttackTarget =
            data.targetSelectMode == TargetSelectMode.Aoe || isQuarterCircleMode;
        HashSet<CellManager> validEntityTargets = new HashSet<CellManager>();
        foreach (var cell in targetGrids)
        {
            if (IsValidTargetCell(cell, data, allowEmptyAttackTarget))
                validEntityTargets.Add(cell);
        }

        ClearRangeHighlight();
        CurrentHighlightedGrids = targetGrids;

        if (data.gridStyle != null)
        {
            bool isPointType = (data.rangeType == RangeType.Point);
            visualManager.HighlightCardRange(CurrentHighlightedGrids, data.gridStyle, playerCell, data.rangeType);

            if (GridHoverController.Instance != null)
                GridHoverController.Instance.SetContext(
                    CurrentHighlightedGrids, playerCell, data.gridStyle, isPointType, validTargetGrids,
                    data.targetSelectMode == TargetSelectMode.Aoe, validEntityTargets,
                    isQuarterCircleMode, hoverMode: GridHoverInteractionMode.CardTargeting);
        }
        else
        {
            Debug.LogWarning($"[CardDragController] 卡牌 [{data.cardName}] 未配置 gridStyle，跳过范围高亮。");
        }
    }

    public void OnCardDragEnd()
    {
        ClearCurrentHoverPreview();
        currentDraggingCard = null;

        if (GridHoverController.Instance != null)
            GridHoverController.Instance.ClearContext();

        IsDragging = false;
        ClearRangeHighlight();

        PlayerMoveController moveController = FindFirstObjectByType<PlayerMoveController>();
        if (moveController != null)
            moveController.IsUsingCard = false;
    }

    public void OnCardReleased(CardUI cardUI)
    {
        if (cardUI == null || cardUI.CurrentCardData == null) return;
        CardData data = cardUI.CurrentCardData;

        bool canAfford = CombatStatsManager.Instance != null
            && CombatStatsManager.Instance.currentEnergy >= data.GetEffectiveCost();

        if (!canAfford)
        {
            Debug.Log("[CardDragController] 能量不足，无法使用卡牌。");
            OnCardDragEnd();
            cardUI.ResetToOriginalState();
            return;
        }

        CellManager releaseCell = GetMouseHoverCell();
        Vector2Int releaseGrid = releaseCell != null
            ? new Vector2Int(gridManager.GetCellGridPosition(releaseCell).x, gridManager.GetCellGridPosition(releaseCell).z)
            : new Vector2Int(-1, -1);
        bool isValidDrop = false;
        HashSet<Vector2Int> castTargetGrids = null;
        bool allowEmptyAttackTarget =
            data.targetSelectMode == TargetSelectMode.Aoe
            || data.targetSelectMode == TargetSelectMode.AQuarterCircle;

        switch (data.targetSelectMode)
        {
            case TargetSelectMode.AnyCell:
                isValidDrop = releaseCell != null && CurrentHighlightedGrids.Contains(releaseCell);
                break;

            case TargetSelectMode.EdgeOnly:
                HashSet<CellManager> edgeCells = RangeSystem.GetEdgeCells(
                    CurrentHighlightedGrids,
                    playerCell,
                    gridManager,
                    data.rangeType,
                    data.rangeDistance
                );
                isValidDrop = releaseCell != null && edgeCells.Contains(releaseCell);
                break;

            case TargetSelectMode.Aoe:
                castTargetGrids = GetCurrentHighlightedTargetGrids();
                isValidDrop = releaseCell != null && CurrentHighlightedGrids.Contains(releaseCell);
                break;

            case TargetSelectMode.AQuarterCircle:
                castTargetGrids = GetQuarterCircleTargetGrids(releaseGrid);
                isValidDrop = releaseCell != null
                    && CurrentHighlightedGrids.Contains(releaseCell)
                    && castTargetGrids.Contains(releaseGrid);
                break;
        }

        if (!isValidDrop)
        {
            Debug.Log($"[CardDragController] 落点不符合 {data.targetSelectMode} 模式要求，取消出牌。");
            OnCardDragEnd();
            cardUI.ResetToOriginalState();
            return;
        }

        if (!IsValidTargetCell(releaseCell, data, allowEmptyAttackTarget))
        {
            Debug.Log($"[CardDragController] 落点格子 [{releaseGrid}] 不满足卡牌 [{data.cardName}] 的目标实体要求，取消出牌。");
            OnCardDragEnd();
            cardUI.ResetToOriginalState();
            return;
        }

        Debug.Log($"[CardDragController] 卡牌 [{data.cardName}] 落点格子：{releaseGrid}，模式：{data.targetSelectMode}");
        OnCardDragEnd();

        if (CardManager.Instance != null)
            CardManager.Instance.PlayCard(data, cardUI.gameObject, releaseGrid, castTargetGrids);
        else
        {
            Debug.LogWarning("[CardDragController] CardManager 实例未找到。");
            cardUI.ResetToOriginalState();
        }
    }

    private CellManager GetMouseHoverCell()
    {
        if (gridManager == null || Camera.main == null) return null;
        if (Mouse.current == null) return null;

        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        int layerMask = 1 << gridManager.cellLayer;

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, layerMask))
        {
            lastMouseHitCell = hit.collider != null ? hit.collider.GetComponentInParent<CellManager>() : null;
            return lastMouseHitCell;
        }

        lastMouseHitCell = null;
        return null;
    }

    private bool IsValidTargetCell(CellManager cell, CardData data, bool allowEmptyAttackTarget = false)
    {
        if (cell == null) return false;
        if (cell.IsLocked) return false;

        bool hasMovement = (data.effectFlags & CardEffectType.Movement) != 0;
        bool hasAttack = (data.effectFlags & CardEffectType.Attack) != 0;

        if (hasMovement)
            return !cell.IsPlayerInside && !cell.HasMonsterInside;

        if (hasAttack)
            return allowEmptyAttackTarget || cell.HasMonsterInside;

        return cell.IsPlayerInside;
    }

    // ===================================================================
    // 伤害与血条预览交互逻辑
    // ===================================================================

    private void HandleMonsterHoverDuringDrag()
    {
        bool isAttackCard = (currentDraggingCard.effectFlags & CardEffectType.Attack) != 0;
        if (!isAttackCard)
        {
            ClearCurrentHoverPreview();
            return;
        }

        CellManager hoveredCell = GetMouseHoverCell();

        if (hoveredCell != null && CurrentHighlightedGrids.Contains(hoveredCell))
        {
            if (hoveredCell.HasMonsterInside)
            {
                MonsterStats monster = FindMonsterAtCell(hoveredCell);
                if (monster != null)
                {
                    MonsterInfoUI infoUI = monster.GetComponentInChildren<MonsterInfoUI>();
                    if (infoUI != null)
                    {
                        if (currentHoveredMonsterUI != infoUI)
                        {
                            ClearCurrentHoverPreview();
                            currentHoveredMonsterUI = infoUI;
                            currentHoveredMonsterUI.ShowDamagePreview(currentDraggingCard.damage);
                        }
                        return;
                    }
                }
            }
        }

        ClearCurrentHoverPreview();
    }

    private void ClearCurrentHoverPreview()
    {
        if (currentHoveredMonsterUI != null)
        {
            currentHoveredMonsterUI.HideDamagePreview();
            currentHoveredMonsterUI = null;
        }
    }

    private MonsterStats FindMonsterAtCell(CellManager targetCell)
    {
        if (targetCell == null) return null;

        MonsterStats[] monsters = FindObjectsByType<MonsterStats>(FindObjectsSortMode.None);
        foreach (var monster in monsters)
        {
            if (monster == null || !monster.gameObject.activeInHierarchy) continue;

            float dist = Vector3.Distance(monster.transform.position, targetCell.transform.position);
            if (dist < 1.0f)
            {
                return monster;
            }
        }
        return null;
    }

    // ===================================================================
    // 内部工具方法
    // ===================================================================

    private void UpdatePlayerGridPosition()
    {
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
        }

        if (playerTransform != null && gridManager != null)
        {
            gridManager.EnsureGridSystemInitialized();
            Vector3 logicPos = playerTransform.position - gridManager.cellOffset;
            var (px, pz) = gridManager.GetGridPosition(logicPos);
            playerGridPos = new Vector2Int(px, pz);

            playerCell = null;
            float bestDist = float.MaxValue;
            foreach (CellManager cell in gridManager.GetCellManagersInColumn(px, pz))
            {
                if (cell == null) continue;
                float dist = Mathf.Abs(cell.transform.position.y - playerTransform.position.y);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    playerCell = cell;
                }
            }
        }
    }

    private HashSet<CellManager> CalculateCardRangeGrids(CellManager centerCell, int distance, RangeType type)
    {
        HashSet<CellManager> rangeCells = new HashSet<CellManager>();
        if (gridManager == null || centerCell == null) return rangeCells;

        return RangeSystem.CalculateReachableCells(centerCell, distance, gridManager, type, blockByMonster: false);
    }

    private HashSet<Vector2Int> GetQuarterCircleTargetGrids(Vector2Int hoverGrid)
    {
        HashSet<Vector2Int> result = new HashSet<Vector2Int>();
        if (gridManager == null || hoverGrid.x < 0 || hoverGrid.y < 0) return result;

        HashSet<CellManager> quarterCircleCells = RangeSystem.GetQuarterCircleTargetCells(
            CurrentHighlightedGrids,
            playerGridPos,
            hoverGrid,
            gridManager);

        foreach (CellManager cell in quarterCircleCells)
        {
            if (cell == null) continue;

            var (cellX, cellZ) = gridManager.GetCellGridPosition(cell);
            result.Add(new Vector2Int(cellX, cellZ));
        }

        return result;
    }

    private HashSet<Vector2Int> GetCurrentHighlightedTargetGrids()
    {
        HashSet<Vector2Int> result = new HashSet<Vector2Int>();
        if (gridManager == null) return result;

        foreach (CellManager cell in CurrentHighlightedGrids)
        {
            if (cell == null) continue;

            var (cellX, cellZ) = gridManager.GetCellGridPosition(cell);
            result.Add(new Vector2Int(cellX, cellZ));
        }

        return result;
    }

    public void ClearRangeHighlight()
    {
        if (visualManager != null && CurrentHighlightedGrids.Count > 0)
        {
            visualManager.ClearRangeHighlight(CurrentHighlightedGrids);
            CurrentHighlightedGrids.Clear();
        }
    }
}
