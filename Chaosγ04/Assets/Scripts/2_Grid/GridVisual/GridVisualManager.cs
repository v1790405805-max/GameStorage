using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class GridVisualManager : MonoBehaviour
{
    [Header("GridManager 引用")]
    [SerializeField] private GridManager gridManager;

    [Header("通用悬停颜色（范围外格子悬停时使用）")]
    public Color cellSelectedColor = new Color(0f, 0f, 0f, 0.4f);
    public Color lineSelectedColor = Color.white;

    [Header("非法目标颜色（范围内但不是合法目标格时使用）")]
    public Color errorCellColor = new Color(1f, 0f, 0f, 0.5f);
    public Color errorLineColor = new Color(128/255f, 128 / 255f, 128 / 255f, 1f);

    [Header("当前激活样式（只读，实时显示正在使用哪个资产）")]
    [SerializeField] private GridStyleData currentStyle;

    private GridStyleData currentConfig;

    // 对外只读属性
    public GridManager GridManager => gridManager;
    public GridStyleData CurrentConfig => currentConfig;

    // ===================================================================
    // Unity 生命周期
    // ===================================================================

    private void Awake()
    {
        EnsureGridManager();
    }

    private void OnEnable()
    {
        EnsureGridManager();
    }

    private void OnValidate()
    {
        EnsureGridManager();
        // Inspector 中修改 currentStyle 字段时同步应用
        if (currentStyle != null) ApplyStyle(currentStyle);
    }

    private void EnsureGridManager()
    {
        if (gridManager == null)
            gridManager = GetComponent<GridManager>() ?? FindFirstObjectByType<GridManager>();
    }

    // ===================================================================
    // 样式切换 API
    // ===================================================================

    /// <summary>
    /// 切换当前激活样式，同时更新 Inspector 中的 currentStyle 显示。
    /// GridHoverController 在 SetContext 前、各 Highlight 方法内部均会调用此方法。
    /// </summary>
    public void ApplyStyle(GridStyleData style)
    {
        currentConfig = style;
        currentStyle = style;  // 同步到 Inspector 可见的字段
    }

    // ===================================================================
    // 范围高亮 API（纯渲染，不持有悬停状态）
    // ===================================================================

    #region 范围预设高亮接口

    /// <summary>障碍过滤后的范围高亮（格子级，跨层；用于预览等场景）。</summary>
    public void HighlightAreaWithObstacles(CellManager centerCell, int range,
        RangeType patternType, GridStyleData style, bool blockByMonster = true)
    {
        HashSet<CellManager> reachableSet =
            RangeSystem.CalculateReachableCells(centerCell, range, gridManager, patternType, blockByMonster);
        HighlightRangeByStyle(reachableSet, style, centerCell, patternType);
    }

    /// <summary>不过滤障碍的原始范围高亮（列级形状范围，取各列顶层格子）。</summary>
    public void HighlightAreaRaw(Vector2Int center, int range,
        RangeType patternType, GridStyleData style)
    {
        HashSet<Vector2Int> rawRangeSet = RangeSystem.GetRawRange(center, range, patternType);
        HighlightRangeByStyle(rawRangeSet, style, center, patternType);
    }

    #endregion

    /// <summary>
    /// 卡牌范围高亮入口（格子级，跨层）。
    /// </summary>
    public void HighlightCardRange(HashSet<CellManager> rangeSet,
        GridStyleData style, CellManager playerCell, RangeType rangeType = RangeType.Point)
    {
        HighlightRangeByStyle(rangeSet, style, playerCell, rangeType);
    }

    /// <summary>
    /// 通用范围高亮（格子级）：按指定 GridStyleData 渲染 rangeSet 内所有格子。
    /// 当 rangeType 为 Point 时，跳过目标格范围色的渲染，仅保留玩家格初始渲染。
    /// </summary>
    public void HighlightRangeByStyle(HashSet<CellManager> rangeSet,
        GridStyleData style, CellManager playerCell, RangeType rangeType = RangeType.Point)
    {
        if (gridManager == null || rangeSet == null || style == null) return;

        ApplyStyle(style);

        bool isPointType = (rangeType == RangeType.Point);

        foreach (CellManager cell in rangeSet)
        {
            if (cell == null) continue;

            bool isPlayer = (cell == playerCell);

            if (isPointType)
            {
                // Point 类型处理：非玩家格不进入颜色高亮范畴
                if (!isPlayer) continue;

                // 玩家格仅应用玩家格正常样式，避开 cellClickedColor/lineClickedColor
                cell.SetCellColor(style.playerCellColor, Application.isPlaying);
                cell.SetLineColor(style.playerLineColor);
            }
            else
            {
                // 普通卡牌逻辑
                cell.SetCellColor(
                    isPlayer ? style.playerCellColor : style.cellClickedColor,
                    Application.isPlaying);
                UpdateSingleCellBorderColor(cell, rangeSet, style, isPlayer);
            }
        }
    }

    /// <summary>列级兼容重载：将列坐标集合映射为各列顶层格子后渲染。</summary>
    public void HighlightRangeByStyle(HashSet<Vector2Int> rangeSet,
        GridStyleData style, Vector2Int playerGridPos, RangeType rangeType = RangeType.Point)
    {
        if (gridManager == null || rangeSet == null || style == null) return;

        HashSet<CellManager> cells = new HashSet<CellManager>();
        foreach (Vector2Int pos in rangeSet)
        {
            if (!gridManager.IsValidGridPosition(pos.x, pos.y)) continue;
            CellManager cell = gridManager.GetCellManagerAt(pos.x, pos.y);
            if (cell != null) cells.Add(cell);
        }

        CellManager playerCell = gridManager.GetCellManagerAt(playerGridPos.x, playerGridPos.y);
        HighlightRangeByStyle(cells, style, playerCell, rangeType);
    }

    /// <summary>
    /// 清除指定格子集合的高亮，还原为普通外观。
    /// </summary>
    public void ClearRangeHighlight(HashSet<CellManager> rangeSet)
    {
        if (gridManager == null || rangeSet == null) return;

        foreach (CellManager cell in rangeSet)
        {
            if (cell != null)
            {
                cell.SetCellColor(gridManager.cellNormalColor, Application.isPlaying);
                cell.SetLineColor(gridManager.lineNormalColor);
            }
        }
    }

    /// <summary>列级兼容重载：将列坐标集合映射为该列全部层格子后清除高亮。</summary>
    public void ClearRangeHighlight(HashSet<Vector2Int> rangeSet)
    {
        if (gridManager == null || rangeSet == null) return;

        foreach (Vector2Int gridPos in rangeSet)
        {
            if (!gridManager.IsValidGridPosition(gridPos.x, gridPos.y)) continue;
            foreach (CellManager cell in gridManager.GetCellManagersInColumn(gridPos.x, gridPos.y))
            {
                if (cell != null)
                {
                    cell.SetCellColor(gridManager.cellNormalColor, Application.isPlaying);
                    cell.SetLineColor(gridManager.lineNormalColor);
                }
            }
        }
    }

    /// <summary>
    /// 以指定样式渲染可到达范围（格子级，跨层）。
    /// 样式资产由调用方持有（PlayerMoveController 的“玩家移动样式”、PlayerOrientationController 的“玩家朝向样式”）。
    /// </summary>
    public void SetReachablePatternColors(HashSet<CellManager> reachableSet,
        CellManager clickedCell, GridStyleData style, bool isRuntime)
    {
        if (gridManager == null || reachableSet == null || style == null) return;

        ApplyStyle(style);

        foreach (CellManager cell in reachableSet)
        {
            if (cell == null) continue;

            bool isPlayer = (cell == clickedCell);
            cell.SetCellColor(
                isPlayer ? style.playerCellColor : style.cellClickedColor,
                isRuntime);
            UpdateSingleCellBorderColor(cell, reachableSet, style, isPlayer);
        }
    }

    /// <summary>
    /// 将全部格子重置为默认普通外观（切换状态前调用）。
    /// 遍历每列的所有层格子，避免多层地图下层格子残留高亮。
    /// </summary>
    public void ResetAllCellsVisuals()
    {
        if (gridManager == null) return;
        int w = GetGridWidth(), h = GetGridHeight();

        for (int x = 0; x < w; x++)
        {
            for (int z = 0; z < h; z++)
            {
                if (!gridManager.IsValidGridPosition(x, z)) continue;

                foreach (CellManager cell in gridManager.GetCellManagersInColumn(x, z))
                {
                    if (cell == null) continue;
                    cell.SetCellColor(gridManager.cellNormalColor, Application.isPlaying);
                    cell.SetLineColor(gridManager.lineNormalColor);
                }
            }
        }
    }

    // ===================================================================
    // 内部工具方法
    // ===================================================================

    /// <summary>
    /// 更新单个格子的四条边线颜色（内边 vs 外边线区分）。
    /// 格子级版本：根据格子坐标判断四方向邻居列是否在范围内。
    /// </summary>
    private void UpdateSingleCellBorderColor(CellManager cell,
        HashSet<CellManager> reachableSet, GridStyleData style, bool isPlayer)
    {
        if (style == null || cell == null) return;

        var (x, z) = gridManager.GetCellGridPosition(cell);

        bool upOuter = !SetContainsColumn(reachableSet, x, z - 1);
        bool downOuter = !SetContainsColumn(reachableSet, x, z + 1);
        bool leftOuter = !SetContainsColumn(reachableSet, x - 1, z);
        bool rightOuter = !SetContainsColumn(reachableSet, x + 1, z);

        Color innerColor = isPlayer ? style.playerLineColor : style.lineClickedColor;
        cell.SetIndividualLinesColor(
            defaultColor: innerColor,
            outerColor: style.outerLineClickedColor,
            upOuter: upOuter,
            downOuter: downOuter,
            leftOuter: leftOuter,
            rightOuter: rightOuter
        );
    }

    /// <summary>格子集合中是否存在位于指定列 (x, z) 的格子（任意层）。</summary>
    private bool SetContainsColumn(HashSet<CellManager> cellSet, int x, int z)
    {
        if (cellSet == null || cellSet.Count == 0) return false;

        foreach (CellManager cell in cellSet)
        {
            if (cell == null) continue;
            var (cx, cz) = gridManager.GetCellGridPosition(cell);
            if (cx == x && cz == z) return true;
        }
        return false;
    }

    /// <summary>
    /// 判断玩家是否站在指定格子上，直接读取公开属性，不使用反射。
    /// </summary>
    public bool IsPlayerOnCell(CellManager cell)
    {
        if (cell == null) return false;
        return cell.IsPlayerInside;
    }

    private int GetGridWidth()
    {
        var prop = typeof(GridManager).GetProperty("Width") ?? typeof(GridManager).GetProperty("height");
        if (prop != null) return (int)prop.GetValue(gridManager);
        var field = typeof(GridManager).GetField("Width")
                 ?? typeof(GridManager).GetField("width")
                 ?? typeof(GridManager).GetField("gridWidth");
        return field != null ? (int)field.GetValue(gridManager) : 20;
    }

    private int GetGridHeight()
    {
        var prop = typeof(GridManager).GetProperty("Height") ?? typeof(GridManager).GetProperty("height");
        if (prop != null) return (int)prop.GetValue(gridManager);
        var field = typeof(GridManager).GetField("Height")
                 ?? typeof(GridManager).GetField("height")
                 ?? typeof(GridManager).GetField("gridHeight");
        return field != null ? (int)field.GetValue(gridManager) : 20;
    }
}