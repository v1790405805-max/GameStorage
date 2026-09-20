using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 统一管理 Grid 格子悬停高亮的控制器。
/// </summary>
public class GridHoverController : MonoBehaviour
{
    public static GridHoverController Instance { get; private set; }

    [Header("依赖引用（留空则自动查找）")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private GridVisualManager visualManager;

    // ------------------------------------------------------------------
    // 当前交互上下文
    // ------------------------------------------------------------------
    private HashSet<CellManager> activeRangeSet = new HashSet<CellManager>();
    /// <summary>
    /// 当前上下文中的有效目标格集合（悬停时才能显示 Target 色的格子）。
    /// 未显式指定时默认等于 activeRangeSet；EdgeOnly 模式仅为边沿格。
    /// </summary>
    private HashSet<CellManager> validTargetSet = new HashSet<CellManager>();
    /// <summary>
    /// 实体合法目标格集合（按 effectFlags 优先级判定，见 CardDragController.IsValidTargetCell）。
    /// 未显式指定时默认等于 activeRangeSet（全部合法，移动模式等场景不受影响）。
    /// </summary>
    private HashSet<CellManager> validEntityTargetSet = new HashSet<CellManager>();
    private CellManager centerCell;
    private GridStyleData activeConfig;
    private bool hasContext = false;
    private bool isPointType = false; // 标识是否为 Point 模式
    private bool isAoeMode = false;   // 标识是否为 AOE 模式（悬停范围内任意格 → 整片范围联动 Target 高亮）
    private bool aoeRangeHighlightActive = false; // AOE 整片 Target 高亮当前是否激活

    // ------------------------------------------------------------------
    // 内部悬停状态
    // ------------------------------------------------------------------
    private Vector2Int currentHoverGrid = new Vector2Int(-1, -1);
    /// <summary>射线实际命中的格子（跨层精确：命中哪个 collider 就是哪个格子）。</summary>
    private CellManager currentHoverCell;

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
        if (!Application.isPlaying) return;
        if (gridManager == null || visualManager == null) return;
        if (Camera.main == null || Mouse.current == null) return;

        HandleHover();
    }

    // ===================================================================
    // 公开 API
    // ===================================================================

    /// <summary>
    /// 注册交互上下文，包含 Point 模式支持。
    /// </summary>
    /// <param name="validTargets">
    /// 有效目标格集合（悬停显示 Target 色的格子），必须是 rangeSet 的子集。
    /// 传 null 时默认等于 rangeSet（整片范围都是有效目标）。
    /// </param>
    /// <param name="aoeMode">
    /// AOE 模式：悬停范围内任意一格时，整片范围联动显示 Target 色（而非单格高亮）。
    /// </param>
    /// <param name="validEntityTargets">
    /// 实体合法目标格集合（悬停显示 Target 色的格子），必须是 rangeSet 的子集。
    /// 范围内但不在此集合的格子悬停时显示 Error 色；传 null 默认全部合法。
    /// </param>
    public void SetContext(HashSet<CellManager> rangeSet, CellManager center, GridStyleData style, bool isPoint = false, HashSet<CellManager> validTargets = null, bool aoeMode = false, HashSet<CellManager> validEntityTargets = null)
    {
        ClearHoverHighlight();

        activeRangeSet = rangeSet ?? new HashSet<CellManager>();
        validTargetSet = validTargets ?? activeRangeSet;
        validEntityTargetSet = validEntityTargets ?? activeRangeSet;
        centerCell = center;
        activeConfig = style;
        isPointType = isPoint;
        isAoeMode = aoeMode;
        aoeRangeHighlightActive = false; // 新上下文从无高亮状态开始
        hasContext = true;
    }

    /// <summary>重载，提供向前兼容性。</summary>
    public void SetContext(HashSet<CellManager> rangeSet, CellManager center, GridStyleData style)
    {
        SetContext(rangeSet, center, style, false);
    }

    /// <summary>
    /// 清除上下文，悬停逻辑停止响应，当前悬停格自动还原颜色。
    /// </summary>
    public void ClearContext()
    {
        ClearHoverHighlight();
        activeRangeSet = new HashSet<CellManager>();
        validTargetSet = new HashSet<CellManager>();
        validEntityTargetSet = new HashSet<CellManager>();
        centerCell = null;
        activeConfig = null;
        isPointType = false;
        isAoeMode = false;
        aoeRangeHighlightActive = false;
        hasContext = false;
    }

    /// <summary>
    /// 强制还原当前悬停格颜色，但保留上下文。
    /// </summary>
    public void ForceRestoreHover()
    {
        ClearHoverHighlight();
    }

    // ===================================================================
    // 内部悬停处理
    // ===================================================================

    private void HandleHover()
    {
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(mousePos);
        int layerMask = 1 << gridManager.cellLayer;

        CellManager hitCell = null;
        int hoverX = -1, hoverZ = -1;
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, layerMask))
        {
            // 命中哪个 collider 就记录哪个格子（跨层精确）
            hitCell = hit.collider != null ? hit.collider.GetComponentInParent<CellManager>() : null;
            Vector3 logicPosition = hit.point - gridManager.cellOffset;
            gridManager.EnsureGridSystemInitialized();
            (hoverX, hoverZ) = gridManager.GetGridPosition(logicPosition);
        }

        bool isValid = gridManager.IsValidGridPosition(hoverX, hoverZ);
        Vector2Int newHover = isValid ? new Vector2Int(hoverX, hoverZ) : new Vector2Int(-1, -1);

        // 坐标或实际命中格子都没变化则不处理（同一列跨层切换时 hitCell 不同，会触发更新）
        if (newHover == currentHoverGrid && hitCell == currentHoverCell) return;

        // AOE 模式：整片范围联动高亮，悬停迁移单独处理（内部自行还原/高亮）
        if (isAoeMode)
        {
            CellManager oldHoverCell = currentHoverCell;
            currentHoverCell = hitCell;
            HandleAoeHoverTransition(newHover, oldHoverCell);
            currentHoverGrid = newHover;
            return;
        }

        // 还原旧悬停格（此时 currentHoverCell 仍是旧值，跨层切换也能正确还原旧层）
        if (currentHoverGrid.x >= 0)
        {
            if (hasContext)
                RestoreCell(currentHoverGrid);
            else
                RestoreCellDefault(currentHoverGrid);
        }

        currentHoverGrid = newHover;
        currentHoverCell = hitCell;

        // 高亮新悬停格
        if (currentHoverGrid.x >= 0)
        {
            if (hasContext)
                HighlightCell(currentHoverGrid);
            else
                HighlightCellDefault(currentHoverGrid);
        }
    }

    // ------------------------------------------------------------------
    // 常态悬停（无上下文时）
    // ------------------------------------------------------------------

    /// <summary>
    /// 取悬停目标格子：若该格就是当前射线命中的格子则直接返回（跨层精确），
    /// 否则回退到该列顶层（用于范围遍历等非悬停场景）。
    /// </summary>
    private CellManager ResolveHoverCell(Vector2Int grid)
    {
        if (currentHoverCell != null && grid == currentHoverGrid)
            return currentHoverCell;
        return gridManager.GetCellManagerAt(grid.x, grid.y);
    }

    private void HighlightCellDefault(Vector2Int grid)
    {
        if (!gridManager.IsValidGridPosition(grid.x, grid.y)) return;
        CellManager cell = ResolveHoverCell(grid);
        if (cell == null) return;
        if (cell.IsLocked) return; // 状态锁：悬停无效果（保持透明）

        cell.SetCellColor(visualManager.cellSelectedColor, isRuntime: true);
        cell.SetLineColor(visualManager.lineSelectedColor);
    }

    private void RestoreCellDefault(Vector2Int grid)
    {
        if (!gridManager.IsValidGridPosition(grid.x, grid.y)) return;
        CellManager cell = ResolveHoverCell(grid);
        if (cell == null) return;

        cell.SetCellColor(gridManager.cellNormalColor, isRuntime: true);
        cell.SetLineColor(gridManager.lineNormalColor);
    }

    // ------------------------------------------------------------------
    // 高亮 / 还原单个格子
    // ------------------------------------------------------------------

    private void HighlightCell(Vector2Int grid)
    {
        if (!gridManager.IsValidGridPosition(grid.x, grid.y)) return;

        CellManager cell = ResolveHoverCell(grid);
        if (cell == null) return;
        if (cell.IsLocked) return; // 状态锁：悬停无效果（保持透明）

        // 非 Point 模式时，玩家/中心格不响应悬停；Point 模式下允许响应中心格悬停
        if (!isPointType && cell == centerCell) return;

        bool isValidTarget = validTargetSet.Contains(cell);
        bool isInsideRange = activeRangeSet.Contains(cell);
        bool isEntityValid = validEntityTargetSet.Contains(cell);

        if (isValidTarget && activeConfig != null)
        {
            if (isEntityValid)
            {
                // 合法目标格（模式目标 + 实体合法）：变成 Target 目标悬停颜色
                cell.SetCellColor(activeConfig.targetCellColor, isRuntime: true);
                cell.SetLineColor(activeConfig.targetLineColor);
            }
            else
            {
                // 模式目标但实体非法（例如 Movement 卡悬停在有怪物的格子上）：变成 Error 颜色
                cell.SetCellColor(visualManager.errorCellColor, isRuntime: true);
                cell.SetLineColor(visualManager.errorLineColor);
            }
        }
        else if (isInsideRange && activeConfig != null)
        {
            // 范围内但不是有效目标格：不响应悬停，保持拖拽后的范围颜色不变
            return;
        }
        else
        {
            // 范围外格：使用默认悬停色
            cell.SetCellColor(visualManager.cellSelectedColor, isRuntime: true);
            cell.SetLineColor(visualManager.lineSelectedColor);
        }
    }

    private void RestoreCell(Vector2Int grid)
    {
        if (!gridManager.IsValidGridPosition(grid.x, grid.y)) return;

        CellManager cell = ResolveHoverCell(grid);
        if (cell == null) return;

        if (!isPointType && cell == centerCell) return;

        bool isInsideRange = activeRangeSet.Contains(cell);

        if (isInsideRange && activeConfig != null)
        {
            if (isPointType && cell == centerCell)
            {
                // Point 类型下，玩家格离开悬停后还原为 Player 样式，不显示 range 点击框
                cell.SetCellColor(activeConfig.playerCellColor, isRuntime: true);
                cell.SetLineColor(activeConfig.playerLineColor);
            }
            else
            {
                // 普通范围内格子离开悬停，还原为范围色
                cell.SetCellColor(activeConfig.cellClickedColor, isRuntime: true);
                RestoreCellBorder(grid);
            }
        }
        else
        {
            // 范围外格，还原为普通常态色
            cell.SetCellColor(gridManager.cellNormalColor, isRuntime: true);
            cell.SetLineColor(gridManager.lineNormalColor);
        }
    }

    private void RestoreCellBorder(Vector2Int grid)
    {
        if (activeConfig == null) return;

        CellManager cell = ResolveHoverCell(grid);
        if (cell == null) return;

        bool upOuter = !RangeContainsColumn(grid.x, grid.y - 1);
        bool downOuter = !RangeContainsColumn(grid.x, grid.y + 1);
        bool leftOuter = !RangeContainsColumn(grid.x - 1, grid.y);
        bool rightOuter = !RangeContainsColumn(grid.x + 1, grid.y);

        cell.SetIndividualLinesColor(
            defaultColor: activeConfig.lineClickedColor,
            outerColor: activeConfig.outerLineClickedColor,
            upOuter: upOuter,
            downOuter: downOuter,
            leftOuter: leftOuter,
            rightOuter: rightOuter
        );
    }

    /// <summary>当前范围集合中是否存在位于指定列 (x, z) 的格子（任意层）。</summary>
    private bool RangeContainsColumn(int x, int z)
    {
        if (activeRangeSet == null || activeRangeSet.Count == 0) return false;

        foreach (CellManager cell in activeRangeSet)
        {
            if (cell == null) continue;
            var (cx, cz) = gridManager.GetCellGridPosition(cell);
            if (cx == x && cz == z) return true;
        }
        return false;
    }

    /// <summary>
    /// AOE 模式的悬停迁移：悬停格进入/离开范围时，整片范围联动切换 Target 高亮。
    /// 关键：鼠标跨格时会经过格子间的无碰撞体缝隙（射线无命中），
    /// 此时必须保持当前高亮状态不变，否则整片范围会反复翻转造成闪烁。
    /// </summary>
    private void HandleAoeHoverTransition(Vector2Int newHover, CellManager oldHoverCell)
    {
        bool newInRange = currentHoverCell != null && activeRangeSet.Contains(currentHoverCell);
        bool oldInRange = oldHoverCell != null && activeRangeSet.Contains(oldHoverCell);

        // 无命中（格子缝隙/棋盘外）：保持当前高亮状态，只还原范围外旧格的悬停色
        if (newHover.x < 0)
        {
            if (currentHoverGrid.x >= 0 && !oldInRange)
                RestoreCellDefault(currentHoverGrid);
            return;
        }

        if (newInRange)
        {
            if (!aoeRangeHighlightActive)
            {
                // 进入范围：整片范围变为 Target 色（范围内移动时保持，不重刷）
                if (currentHoverGrid.x >= 0 && !oldInRange)
                    RestoreCellDefault(currentHoverGrid);
                HighlightWholeRangeAsTarget();
                aoeRangeHighlightActive = true;
            }
            return;
        }

        // 明确悬停在范围外格子：整片范围还原为范围色
        if (aoeRangeHighlightActive)
        {
            RestoreWholeRange();
            aoeRangeHighlightActive = false;
        }
        if (currentHoverGrid.x >= 0 && !oldInRange)
            RestoreCellDefault(currentHoverGrid);
        HighlightCellDefault(newHover);
    }

    /// <summary>AOE：范围内全部格子联动高亮——实体合法格显示 Target 色，非法格显示 Error 色（玩家格保持 Player 样式）。</summary>
    private void HighlightWholeRangeAsTarget()
    {
        if (activeConfig == null || activeRangeSet.Count == 0) return;

        foreach (CellManager cell in activeRangeSet)
        {
            if (cell == null) continue;
            if (!isPointType && cell == centerCell) continue;

            if (validEntityTargetSet.Contains(cell))
            {
                cell.SetCellColor(activeConfig.targetCellColor, isRuntime: true);
                cell.SetLineColor(activeConfig.targetLineColor);
            }
            else
            {
                cell.SetCellColor(visualManager.errorCellColor, isRuntime: true);
                cell.SetLineColor(visualManager.errorLineColor);
            }
        }
    }

    /// <summary>AOE：范围内全部格子还原为拖拽后的范围色（玩家格保持 Player 样式）。</summary>
    private void RestoreWholeRange()
    {
        if (activeConfig == null || activeRangeSet.Count == 0) return;

        foreach (CellManager cell in activeRangeSet)
        {
            if (cell == null) continue;

            // 非 Point 模式下玩家格不属于范围高亮，保持 Player 样式不参与还原
            if (!isPointType && cell == centerCell) continue;

            if (isPointType && cell == centerCell)
            {
                // Point 类型下玩家格还原为 Player 样式，与单格还原逻辑一致
                cell.SetCellColor(activeConfig.playerCellColor, isRuntime: true);
                cell.SetLineColor(activeConfig.playerLineColor);
                continue;
            }

            cell.SetCellColor(activeConfig.cellClickedColor, isRuntime: true);
            var (cx, cz) = gridManager.GetCellGridPosition(cell);
            RestoreCellBorder(new Vector2Int(cx, cz));
        }
    }

    private void ClearHoverHighlight()
    {
        if (currentHoverGrid.x >= 0)
        {
            if (isAoeMode)
            {
                // AOE：整片高亮激活则联动还原，否则按常态还原悬停格
                if (aoeRangeHighlightActive)
                {
                    RestoreWholeRange();
                    aoeRangeHighlightActive = false;
                }
                else if (currentHoverGrid.x >= 0)
                    RestoreCellDefault(currentHoverGrid);
            }
            else if (hasContext)
            {
                RestoreCell(currentHoverGrid);
            }
            else
            {
                RestoreCellDefault(currentHoverGrid);
            }

            currentHoverGrid = new Vector2Int(-1, -1);
            currentHoverCell = null;
        }
    }
}
