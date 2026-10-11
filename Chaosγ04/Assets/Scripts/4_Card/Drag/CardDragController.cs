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
    [SerializeField] private PlayerMoveController movementController;

    /// <summary>当前被高亮的卡牌范围格集合（供外部查询，格子级含层）。</summary>
    public HashSet<CellManager> CurrentHighlightedGrids { get; private set; }
        = new HashSet<CellManager>();

    private Vector2Int playerGridPos = new Vector2Int(-1, -1);
    /// <summary>玩家所在格（含层，用于卡牌范围起点）。</summary>
    private CellManager playerCell;
    /// <summary>最近一次鼠标射线实际命中的格子（跨层精确，用于落点校验）。</summary>
    private CellManager lastMouseHitCell;

    /// <summary>当前卡牌允许作为最终落点的格子集合，用于移动预览与出牌判定。</summary>
    private readonly HashSet<CellManager> currentValidTargetGrids = new HashSet<CellManager>();

    /// <summary>拖拽开始时按实体要求过滤后的落点快照：拖拽预览可能假设性移动怪物，实时占用会失真，出牌校验改用这个快照。</summary>
    private readonly HashSet<CellManager> currentValidEntityTargets = new HashSet<CellManager>();

    // 额外效果提供的拖拽预览状态（与具体卡牌无关，见 ICardDragPreviewProvider）。
    private static readonly Vector2Int NoEffectPreviewTarget = new Vector2Int(int.MinValue, int.MinValue);
    private Vector2Int effectPreviewTargetGrid = new Vector2Int(int.MinValue, int.MinValue);
    private readonly List<CellManager> effectPreviewCells = new List<CellManager>();
    private GridStyleData effectPreviewStyle;
    private MonsterIdentityManager effectPreviewMonster;
    private Vector3 effectPreviewMonsterOrigin;
    private bool effectPreviewMonsterMoved;

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
        if (movementController == null) movementController = FindFirstObjectByType<PlayerMoveController>();
    }

    private void Update()
    {
        if (IsDragging && currentDraggingCard != null)
        {
            HandleMonsterHoverDuringDrag();
            HandleMovementCardPreviewDuringDrag();
        }
    }

    // ===================================================================
    // 公开 API（由 CardUI 在拖拽事件中调用）
    // ===================================================================

    public void OnCardDragging(CardData data)
    {
        if (data == null || gridManager == null || visualManager == null) return;

        if (movementController == null)
            movementController = FindFirstObjectByType<PlayerMoveController>();

        PlayerMoveController moveController = movementController;
        if (moveController != null && moveController.IsMoving)
        {
            Debug.LogWarning("[CardDragController] 玩家正在移动中，禁止拖拽卡牌！");
            return;
        }

        ClearEffectDragPreview();
        IsDragging = true;
        currentDraggingCard = data;

        if (moveController != null)
        {
            moveController.HideCardMovementPreview(this);
            moveController.IsUsingCard = true;
            moveController.ForceClearHoverState();
        }

        UpdatePlayerGridPosition();

        int effectiveRangeDistance = data.GetEffectiveRangeDistance();
        HashSet<CellManager> targetGrids = (data.rangeType == RangeType.Point)
            ? new HashSet<CellManager> { playerCell }
            : CalculateCardRangeGrids(playerCell, effectiveRangeDistance, data.rangeType);

        HashSet<CellManager> validTargetGrids = targetGrids;
        if (data.targetSelectMode == TargetSelectMode.EdgeOnly)
        {
            validTargetGrids = RangeSystem.GetEdgeCells(
                targetGrids, playerCell, gridManager, data.rangeType, effectiveRangeDistance);
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

        currentValidEntityTargets.Clear();
        currentValidEntityTargets.UnionWith(validEntityTargets);

        ClearRangeHighlight();
        CurrentHighlightedGrids = targetGrids;
        currentValidTargetGrids.Clear();
        currentValidTargetGrids.UnionWith(validTargetGrids);

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

    public void OnCardDragEnd(bool keepMovementPreview = false)
    {
        ClearEffectDragPreview();
        ClearCurrentHoverPreview();
        currentDraggingCard = null;

        if (!keepMovementPreview && movementController != null)
            movementController.HideCardMovementPreview(this);

        if (GridHoverController.Instance != null)
            GridHoverController.Instance.ClearContext();

        IsDragging = false;
        ClearRangeHighlight();

        PlayerMoveController moveController = movementController;
        if (moveController != null)
            moveController.IsUsingCard = false;
    }

    public void OnCardReleased(CardUI cardUI)
    {
        if (cardUI == null || cardUI.CurrentCardData == null) return;
        CardData data = cardUI.CurrentCardData;

        // 先还原拖拽预览可能造成的假设性怪物位移，再做落点校验。
        ClearEffectDragPreview();

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

        switch (data.targetSelectMode)
        {
            case TargetSelectMode.AnyCell:
                isValidDrop = releaseCell != null && CurrentHighlightedGrids.Contains(releaseCell);
                break;

            case TargetSelectMode.EdgeOnly:
                int effectiveRangeDistance = data.GetEffectiveRangeDistance();
                HashSet<CellManager> edgeCells = RangeSystem.GetEdgeCells(
                    CurrentHighlightedGrids,
                    playerCell,
                    gridManager,
                    data.rangeType,
                    effectiveRangeDistance
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

        // 用拖拽开始时的实体快照校验：拖拽预览可能假设性移动了怪物，实时占用会失真。
        if (!currentValidEntityTargets.Contains(releaseCell))
        {
            Debug.Log($"[CardDragController] 落点格子 [{releaseGrid}] 不满足卡牌 [{data.cardName}] 的目标实体要求，取消出牌。");
            OnCardDragEnd();
            cardUI.ResetToOriginalState();
            return;
        }

        if (!CardEffectCore.CanPlayAll(data, releaseGrid))
        {
            Debug.Log($"[CardDragController] 卡牌 [{data.cardName}] 的额外效果不允许在当前状态使用，取消出牌。");
            OnCardDragEnd();
            cardUI.ResetToOriginalState();
            return;
        }

        Debug.Log($"[CardDragController] 卡牌 [{data.cardName}] 落点格子：{releaseGrid}，模式：{data.targetSelectMode}");
        bool hasMovement = currentDraggingCard != null
            && (currentDraggingCard.effectFlags & CardEffectType.Movement) != 0;
        OnCardDragEnd(keepMovementPreview: hasMovement);

        if (CardManager.Instance != null)
        {
            CardManager.Instance.PlayCard(data, cardUI.gameObject, releaseGrid, castTargetGrids);
            if (movementController != null)
                movementController.HideCardMovementPreview(this);
        }
        else
        {
            Debug.LogWarning("[CardDragController] CardManager 实例未找到。");
            cardUI.ResetToOriginalState();
        }
    }

    private CellManager GetMouseHoverCell()
    {
        if (gridManager == null) return null;
        if (Mouse.current == null) return null;

        Camera dragCamera = MainCameraAppoint.Current;
        if (dragCamera == null) return null;

        Ray ray = dragCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
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
        if (cell.IsHardLocked) return false;

        bool hasMovement = (data.effectFlags & CardEffectType.Movement) != 0;
        bool hasAttack = (data.effectFlags & CardEffectType.Attack) != 0;

        if (hasMovement && cell.BlocksPlayer)
            return false;

        if (hasMovement)
            return !cell.IsPlayerInside && !cell.HasMonsterInside;

        if (hasAttack)
            return allowEmptyAttackTarget || cell.HasMonsterInside;

        return !cell.BlocksPlayer && cell.IsPlayerInside;
    }

    // ===================================================================
    // 伤害与血条预览交互逻辑
    // ===================================================================

    private void HandleMovementCardPreviewDuringDrag()
    {
        if (currentDraggingCard == null || movementController == null)
        {
            ClearEffectDragPreview();
            return;
        }

        if (HasEffectDragPreview(currentDraggingCard))
        {
            HandleEffectDragPreviewDuringDrag();
            return;
        }

        ClearEffectDragPreview();

        bool hasMovement =
            (currentDraggingCard.effectFlags & CardEffectType.Movement) != 0;
        if (!hasMovement)
        {
            movementController.HideCardMovementPreview(this);
            return;
        }

        CellManager hoveredCell = GetMouseHoverCell();
        bool isValid =
            hoveredCell != null &&
            currentValidTargetGrids.Contains(hoveredCell) &&
            IsValidTargetCell(hoveredCell, currentDraggingCard);

        if (isValid)
            movementController.ShowCardMovementPreview(hoveredCell, this);
        else
            movementController.HideCardMovementPreview(this);
    }

    /// <summary>
    /// 通用拖拽预览：内容由卡牌上实现了 ICardDragPreviewProvider 的额外效果提供，
    /// CardDragController 只负责渲染（范围高亮 / Player_WhenMove / 假设性怪物位移）。
    /// </summary>
    private void HandleEffectDragPreviewDuringDrag()
    {
        if (currentDraggingCard == null || movementController == null || gridManager == null)
        {
            ClearEffectDragPreview();
            return;
        }

        CellManager hoveredCell = GetMouseHoverCell();
        if (hoveredCell == null)
        {
            ClearEffectDragPreview();
            return;
        }

        var (targetX, targetZ) = gridManager.GetCellGridPosition(hoveredCell);
        Vector2Int targetGrid = new Vector2Int(targetX, targetZ);

        // 效果预览可能假设性移动怪物，每帧重算会和它互相打架，因此只在悬停目标格变化时重新求值。
        if (targetGrid == effectPreviewTargetGrid)
        {
            RefreshEffectPreviewHighlight();
            return;
        }

        ClearEffectDragPreview();

        bool isCandidate =
            currentValidTargetGrids.Contains(hoveredCell) &&
            IsValidTargetCell(hoveredCell, currentDraggingCard);

        if (!isCandidate ||
            !TryBuildEffectDragPreview(currentDraggingCard, targetGrid, out CardDragPreview preview))
        {
            return;
        }

        ApplyEffectDragPreview(preview);
        effectPreviewTargetGrid = targetGrid;
    }

    private void ApplyEffectDragPreview(CardDragPreview preview)
    {
        GridStyleData style = currentDraggingCard != null ? currentDraggingCard.gridStyle : null;
        if (style != null && preview.highlightCells != null)
        {
            foreach (CellManager cell in preview.highlightCells)
            {
                if (cell == null) continue;
                visualManager.ApplyTargetCellStyle(cell, style);
                effectPreviewCells.Add(cell);
            }
            effectPreviewStyle = style;
        }

        effectPreviewMonster = preview.monsterToMove;
        if (effectPreviewMonster != null)
        {
            effectPreviewMonsterOrigin = effectPreviewMonster.transform.position;
            if (preview.monsterDestinationCell != null)
            {
                effectPreviewMonster.transform.position =
                    preview.monsterDestinationCell.transform.position;
                effectPreviewMonsterMoved = true;
            }
        }

        if (preview.playerPreviewCell != null)
        {
            movementController.ShowCardMovementPreview(preview.playerPreviewCell, this);
        }
    }

    private void RefreshEffectPreviewHighlight()
    {
        if (effectPreviewStyle == null || effectPreviewCells.Count == 0 || visualManager == null)
        {
            return;
        }

        foreach (CellManager cell in effectPreviewCells)
        {
            if (cell == null) continue;
            visualManager.ApplyTargetCellStyle(cell, effectPreviewStyle);
        }
    }

    private void ClearEffectDragPreview()
    {
        effectPreviewTargetGrid = NoEffectPreviewTarget;

        if (movementController != null)
        {
            movementController.HideCardMovementPreview(this);
        }

        if (effectPreviewMonster != null && effectPreviewMonsterMoved)
        {
            effectPreviewMonster.transform.position = effectPreviewMonsterOrigin;
        }
        effectPreviewMonsterMoved = false;
        effectPreviewMonster = null;

        if (effectPreviewCells.Count > 0 && visualManager != null)
        {
            foreach (CellManager cell in effectPreviewCells)
            {
                if (cell == null) continue;
                visualManager.RestoreCardRangeCell(
                    cell, CurrentHighlightedGrids, effectPreviewStyle, playerCell);
            }
        }
        effectPreviewCells.Clear();
        effectPreviewStyle = null;
    }

    private static bool HasEffectDragPreview(CardData card)
    {
        if (card == null || card.extraEffects == null)
        {
            return false;
        }

        foreach (ExtraCardEffect extra in card.extraEffects)
        {
            if (string.IsNullOrEmpty(extra.effectTypeName))
            {
                continue;
            }

            System.Type effectType = System.Type.GetType(extra.effectTypeName);
            if (effectType != null && typeof(ICardDragPreviewProvider).IsAssignableFrom(effectType))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryBuildEffectDragPreview(
        CardData card,
        Vector2Int targetGrid,
        out CardDragPreview preview)
    {
        preview = default;

        if (card == null || card.extraEffects == null || gridManager == null)
        {
            return false;
        }

        foreach (ExtraCardEffect extra in card.extraEffects)
        {
            if (string.IsNullOrEmpty(extra.effectTypeName))
            {
                continue;
            }

            System.Type effectType = System.Type.GetType(extra.effectTypeName);
            if (effectType == null || effectType.IsAbstract ||
                !typeof(ICardDragPreviewProvider).IsAssignableFrom(effectType))
            {
                continue;
            }

            ScriptableObject instance = ScriptableObject.CreateInstance(effectType);
            bool found = instance is ICardDragPreviewProvider provider &&
                         provider.TryGetDragPreview(gridManager, card, targetGrid, out preview);
            Destroy(instance);

            if (found)
            {
                return true;
            }
        }

        return false;
    }

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

        return RangeSystem.CalculateReachableCells(
            centerCell,
            distance,
            gridManager,
            type,
            blockByMonster: false,
            blockByWater: false);
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
        currentValidTargetGrids.Clear();

        if (visualManager != null && CurrentHighlightedGrids.Count > 0)
        {
            visualManager.ClearRangeHighlight(CurrentHighlightedGrids);
            CurrentHighlightedGrids.Clear();
        }
    }
}
