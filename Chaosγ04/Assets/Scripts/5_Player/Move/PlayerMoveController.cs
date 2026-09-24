using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(LineRenderer))]
public class PlayerMoveController : MonoBehaviour, ITurnStateListener
{
    [Header("视觉管理器引用")]
    [SerializeField] private GridVisualManager visualManager;

    [Header("玩家移动样式")]
    [Tooltip("点击玩家角色显示移动范围时，使用的 Grid 高亮样式资产（GridStyleData）")]
    [SerializeField] private GridStyleData playerMoveStyle;

    [Header("角色与动画")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Animator playerAnimator;
    [Tooltip("每移动一格所需的时间（秒）")]
    [Min(0.01f)] public float timePerCell = 0.5f;

    [Header("路径 LineRenderer 渲染参数")]
    [SerializeField] private LineRenderer pathLineRenderer;
    [Tooltip("路径线段距离格子中心的高度偏移，防止 Z-Fighting")]
    public float pathHeightOffset = 0.01f;
    public float pathWidth = 0.06f;

    [Header("点击射线检测")]
    [Tooltip("点击/悬停使用的射线 LayerMask。默认自动使用 GridManager.cellLayer（格子层），可在 Inspector 中调整")]
    [SerializeField] private LayerMask clickLayerMask = 0;

    private bool isMoving = false;
    public bool IsMoving => isMoving;

    private Vector2Int currentClickedGrid = new Vector2Int(-1, -1);
    private CellManager currentClickedCell;
    private Vector2Int playerGridPos = new Vector2Int(-1, -1);
    private HashSet<CellManager> reachableGridSet = new HashSet<CellManager>();

    // 【长按/短按判定】按下玩家格时先挂起，抬起时区分“短按点击（显示范围）”与“长按（显示朝向按钮）”
    private CellManager pendingPressCell;
    private float pressStartTime = -1f;

    public bool IsUsingCard { get; set; } = false;

    public int CurrentActionPoint
    {
        get
        {
            if (Application.isPlaying && CombatStatsManager.Instance != null)
                return CombatStatsManager.Instance.currentActionPoint;
            return 3;
        }
    }

    /// <summary>
    /// 区分“短按点击（显示范围）”与“长按（显示朝向按钮）”的时长阈值；
    /// 场景中存在 PlayerOrientationController 时跟随其配置，否则用默认值
    /// </summary>
    private float ClickHoldThreshold
    {
        get
        {
            if (PlayerOrientationController.Instance != null)
                return PlayerOrientationController.Instance.LongPressDuration;
            return 0.5f;
        }
    }

    private void Awake()
    {
        if (visualManager == null)
            visualManager = GetComponent<GridVisualManager>() ?? FindFirstObjectByType<GridVisualManager>();
        if (pathLineRenderer == null)
            pathLineRenderer = GetComponent<LineRenderer>();

        if (clickLayerMask.value == 0 && visualManager != null && visualManager.GridManager != null)
            clickLayerMask = 1 << visualManager.GridManager.cellLayer;

        ConfigureLineRenderer();
    }

    private void Start()
    {
        if (visualManager == null || visualManager.GridManager == null)
        {
            Debug.LogError("[PlayerMoveController] 未能正确找到 VisualManager 或 GridManager。");
            return;
        }

        if (Application.isPlaying && playerTransform != null)
        {
            visualManager.GridManager.EnsureGridSystemInitialized();
            Vector3 logicPos = playerTransform.position - visualManager.GridManager.cellOffset;
            var (px, pz) = visualManager.GridManager.GetGridPosition(logicPos);
            playerGridPos = new Vector2Int(px, pz);
        }

        if (TurnManager.Instance != null)
            TurnManager.Instance.RegisterPlayerBehaviour(this);
    }

    private void Update()
    {
        if (TurnManager.Instance != null && TurnManager.Instance.CurrentTurn != TurnState.Player)
        {
            pendingPressCell = null;
            pressStartTime = -1f;
            if (currentClickedCell != null)
                ClearClickedGrid();
            return;
        }

        if (Application.isPlaying && visualManager != null && visualManager.GridManager != null)
            HandleGridInteraction();
    }

    public void OnTurnActivated() { }

    public void OnTurnDeactivated()
    {
        pendingPressCell = null;
        pressStartTime = -1f;
        if (currentClickedCell != null)
            ClearClickedGrid();
        ClearPath();
    }

    public void ForceClearHoverState()
    {
        ClearPath();
        if (GridHoverController.Instance != null)
            GridHoverController.Instance.ForceRestoreHover();
    }

    public void SyncGridPosition(Vector2Int gridPos)
    {
        playerGridPos = gridPos;
    }

    /// <summary>玩家当前所在格坐标（供 PlayerOrientationController 渲染朝向样式使用）。</summary>
    public Vector2Int PlayerGridPos => playerGridPos;

    // ===================================================================
    // 【核心修复】卡牌位移专属入口与协程
    // ===================================================================

    /// <summary>
    /// 由 CardManager 调用的平滑走位（支持卡牌移动，绕过手动点击的集合限制）
    /// </summary>
    public void MoveToTargetGridByCard(Vector2Int targetGrid)
    {
        if (visualManager == null || visualManager.GridManager == null) return;

        CellManager startCell = visualManager.GridManager.GetCellManagerAt(playerGridPos.x, playerGridPos.y);
        CellManager endCell = visualManager.GridManager.GetCellManagerAt(targetGrid.x, targetGrid.y);

        if (startCell != null && endCell != null)
        {
            StartCoroutine(MovePlayerByCardRoutine(startCell, endCell));
        }
        else
        {
            Debug.LogWarning($"[PlayerMoveController] 无法找到起始格 [{playerGridPos}] 或目标格 [{targetGrid}]");
        }
    }

    /// <summary>
    /// 专供卡牌调用的走位协程（不要求 reachableGridSet，且不消耗基础行动点 AP）
    /// </summary>
    private IEnumerator MovePlayerByCardRoutine(CellManager startCell, CellManager endCell)
    {
        if (playerTransform == null) yield break;

        // 【关键改动】将 null 作为 reachableSet 传给 FindPathAStarCells，解除只能在 AP 点击范围内走的限制
        List<CellManager> path = MoveSystem.FindPathAStarCells(startCell, endCell, null, visualManager.GridManager);
        if (path == null || path.Count <= 1)
        {
            Debug.LogWarning("[PlayerMoveController] 卡牌移动寻路失败，未找到有效路径！");
            yield break;
        }

        if (playerAnimator == null)
            playerAnimator = playerTransform.GetComponentInChildren<Animator>();

        ClearPath();
        ClearClickedGrid();
        isMoving = true;

        if (playerAnimator != null) playerAnimator.SetBool("Move", true);

        for (int i = 1; i < path.Count; i++)
        {
            CellManager cur = path[i - 1];
            CellManager next = path[i];
            if (cur == null || next == null) continue;

            if (playerAnimator != null)
            {
                var (curX, curZ) = visualManager.GridManager.GetCellGridPosition(cur);
                var (nextX, nextZ) = visualManager.GridManager.GetCellGridPosition(next);
                int dx = nextX - curX;
                int dy = nextZ - curZ;

                if (dx < 0) { playerAnimator.SetFloat("Horizontal", -1f); playerAnimator.SetFloat("Vertical", -1f); }
                else if (dx > 0) { playerAnimator.SetFloat("Horizontal", 1f); playerAnimator.SetFloat("Vertical", 1f); }
                else if (dy < 0) { playerAnimator.SetFloat("Horizontal", -1f); playerAnimator.SetFloat("Vertical", 1f); }
                else if (dy > 0) { playerAnimator.SetFloat("Horizontal", 1f); playerAnimator.SetFloat("Vertical", -1f); }
            }

            Vector3 targetPos = next.transform.position;
            Vector3 stepStartPos = playerTransform.position;
            float elapsed = 0f;

            while (elapsed < timePerCell)
            {
                elapsed += Time.deltaTime;
                playerTransform.position = Vector3.Lerp(stepStartPos, targetPos, Mathf.Clamp01(elapsed / timePerCell));
                yield return null;
            }

            playerTransform.position = targetPos;
            var (nx, nz) = visualManager.GridManager.GetCellGridPosition(next);
            playerGridPos = new Vector2Int(nx, nz);
        }

        isMoving = false;
        if (playerAnimator != null) playerAnimator.SetBool("Move", false);

        // 移动完成后，自动唤出朝向选择按钮
        TriggerOrientationUI();
    }

    // ===================================================================
    // 普通点击移动交互
    // ===================================================================

    private void HandleGridInteraction()
    {
        if (IsUsingCard || isMoving)
        {
            ClearPath();
            return;
        }

        if (Camera.main == null || Mouse.current == null) return;

        Vector2 mousePosition = Mouse.current.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(mousePosition);

        bool hasHit = Physics.Raycast(ray, out RaycastHit hit, 1000f, clickLayerMask.value);
        int hoverX = -1, hoverZ = -1;

        if (hasHit)
        {
            Vector3 logicPosition = hit.point - visualManager.GridManager.cellOffset;
            visualManager.GridManager.EnsureGridSystemInitialized();
            (hoverX, hoverZ) = visualManager.GridManager.GetGridPosition(logicPosition);
        }

        bool isValidHover = visualManager.GridManager.IsValidGridPosition(hoverX, hoverZ);

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (isValidHover)
            {
                CellManager cellUnderMouse = hasHit && hit.collider != null
                    ? hit.collider.GetComponentInParent<CellManager>()
                    : visualManager.GridManager.GetCellManagerAt(hoverX, hoverZ);

                if (visualManager.IsPlayerOnCell(cellUnderMouse))
                {
                    // 按下玩家格：先挂起，不立即显示范围；等抬起时区分短按点击/长按
                    pendingPressCell = cellUnderMouse;
                    pressStartTime = Time.time;
                }
                else if (cellUnderMouse != null && reachableGridSet.Contains(cellUnderMouse))
                {
                    if (cellUnderMouse.IsLocked)
                        Debug.Log("[PlayerMoveController] 目标格处于状态锁，无法进入！");
                    else if (cellUnderMouse.HasMonsterInside)
                        Debug.Log("[PlayerMoveController] 目标格有怪物，无法进入！");
                    else
                        StartCoroutine(MovePlayerAlongPathRoutine(currentClickedCell, cellUnderMouse));
                }
                else
                {
                    ClearClickedGrid();
                }
            }
            else
            {
                ClearClickedGrid();
            }
        }

        // 抬起判定：短按 + 朝向按钮未显示 + 松开时仍停留在该角色格 → 触发“点击角色显示范围”
        if (Mouse.current.leftButton.wasReleasedThisFrame && pendingPressCell != null)
        {
            bool isShortPress = Time.time - pressStartTime < ClickHoldThreshold;
            bool menuVisible = PlayerOrientationController.Instance != null
                && PlayerOrientationController.Instance.ButtonsVisible;

            CellManager releaseCell = hasHit && hit.collider != null
                ? hit.collider.GetComponentInParent<CellManager>()
                : visualManager.GridManager.GetCellManagerAt(hoverX, hoverZ);
            bool releasedOnPlayerCell = releaseCell != null
                && releaseCell == pendingPressCell
                && visualManager.IsPlayerOnCell(releaseCell);

            if (isShortPress && !menuVisible && releasedOnPlayerCell)
            {
                var (px, pz) = visualManager.GridManager.GetCellGridPosition(pendingPressCell);
                if (currentClickedGrid.x != px || currentClickedGrid.y != pz || pendingPressCell != currentClickedCell)
                    SetClickedGrid(px, pz, pendingPressCell);
            }

            pendingPressCell = null;
            pressStartTime = -1f;
        }

        if (isValidHover)
        {
            CellManager hoverCell = hasHit && hit.collider != null
                ? hit.collider.GetComponentInParent<CellManager>()
                : visualManager.GridManager.GetCellManagerAt(hoverX, hoverZ);
            bool inRange = hoverCell != null && reachableGridSet.Contains(hoverCell);
            if (inRange && hoverCell != currentClickedCell)
                DrawPathToTarget(currentClickedCell, hoverCell);
            else
                ClearPath();
        }
        else
        {
            ClearPath();
        }
    }

    private void SetClickedGrid(int x, int z, CellManager clickedCell)
    {
        visualManager.ResetAllCellsVisuals();
        currentClickedGrid = new Vector2Int(x, z);
        currentClickedCell = clickedCell;
        reachableGridSet = RangeSystem.CalculateReachableCells(
            clickedCell,
            CurrentActionPoint,
            visualManager.GridManager,
            useAbilityTraversal: true);

        if (GridHoverController.Instance != null)
            GridHoverController.Instance.SetContext(
                reachableGridSet, clickedCell, playerMoveStyle);

        visualManager.SetReachablePatternColors(reachableGridSet, clickedCell, playerMoveStyle, isRuntime: true);
    }

    /// <summary>
    /// 清除当前点击状态（范围渲染、悬停上下文、路径指示）。
    /// 供本类内部与 PlayerOrientationController（长按唤出朝向按钮时）调用。
    /// </summary>
    public void ClearClickedGrid()
    {
        // 取消正在进行的按下判定，避免清除后残留的抬起事件再触发点击
        pendingPressCell = null;
        pressStartTime = -1f;

        visualManager.ResetAllCellsVisuals();
        currentClickedGrid = new Vector2Int(-1, -1);
        currentClickedCell = null;
        reachableGridSet.Clear();
        ClearPath();

        if (GridHoverController.Instance != null)
            GridHoverController.Instance.ClearContext();
    }

    private void ConfigureLineRenderer()
    {
        if (pathLineRenderer == null) return;
        pathLineRenderer.startWidth = pathWidth;
        pathLineRenderer.endWidth = pathWidth;
        pathLineRenderer.positionCount = 0;
        pathLineRenderer.useWorldSpace = true;
        if (pathLineRenderer.sharedMaterial == null)
        {
            pathLineRenderer.sharedMaterial = new Material(Shader.Find("Unlit/Color"));
            pathLineRenderer.sharedMaterial.color = Color.green;
        }
    }

    private void ClearPath()
    {
        if (pathLineRenderer != null) pathLineRenderer.positionCount = 0;
    }

    private void DrawPathToTarget(CellManager startCell, CellManager endCell)
    {
        if (pathLineRenderer == null) return;
        List<CellManager> path = MoveSystem.FindPathAStarCells(startCell, endCell, reachableGridSet, visualManager.GridManager);
        if (path == null || path.Count == 0) { ClearPath(); return; }

        pathLineRenderer.positionCount = path.Count;
        for (int i = 0; i < path.Count; i++)
        {
            CellManager cell = path[i];
            if (cell != null)
            {
                Vector3 worldPos = cell.transform.position;
                worldPos.y += pathHeightOffset;
                pathLineRenderer.SetPosition(i, worldPos);
            }
        }
    }

    /// <summary>
    /// 玩家自主点击地块移动的常规协程（需要扣除 AP）
    /// </summary>
    private IEnumerator MovePlayerAlongPathRoutine(CellManager startCell, CellManager endCell)
    {
        if (playerTransform == null) yield break;

        List<CellManager> path = MoveSystem.FindPathAStarCells(startCell, endCell, reachableGridSet, visualManager.GridManager);
        if (path == null || path.Count <= 1) yield break;

        int stepCount = path.Count - 1;

        if (CombatStatsManager.Instance != null && !CombatStatsManager.Instance.HasEnoughActionPoint(stepCount))
        {
            Debug.LogWarning($"[PlayerMoveController] ActionPoint 不足！需要 {stepCount} 点，当前只有 {CombatStatsManager.Instance.currentActionPoint} 点！");
            yield break;
        }

        if (CombatStatsManager.Instance != null)
            CombatStatsManager.Instance.ConsumeActionPoint(stepCount);

        if (playerAnimator == null)
            playerAnimator = playerTransform.GetComponentInChildren<Animator>();

        ClearPath();
        ClearClickedGrid();
        isMoving = true;

        if (playerAnimator != null) playerAnimator.SetBool("Move", true);

        for (int i = 1; i < path.Count; i++)
        {
            CellManager cur = path[i - 1];
            CellManager next = path[i];
            if (cur == null || next == null) continue;

            if (playerAnimator != null)
            {
                var (curX, curZ) = visualManager.GridManager.GetCellGridPosition(cur);
                var (nextX, nextZ) = visualManager.GridManager.GetCellGridPosition(next);
                int dx = nextX - curX;
                int dy = nextZ - curZ;

                if (dx < 0) { playerAnimator.SetFloat("Horizontal", -1f); playerAnimator.SetFloat("Vertical", -1f); }
                else if (dx > 0) { playerAnimator.SetFloat("Horizontal", 1f); playerAnimator.SetFloat("Vertical", 1f); }
                else if (dy < 0) { playerAnimator.SetFloat("Horizontal", -1f); playerAnimator.SetFloat("Vertical", 1f); }
                else if (dy > 0) { playerAnimator.SetFloat("Horizontal", 1f); playerAnimator.SetFloat("Vertical", -1f); }
            }

            Vector3 targetPos = next.transform.position;
            Vector3 stepStartPos = playerTransform.position;
            float elapsed = 0f;

            while (elapsed < timePerCell)
            {
                elapsed += Time.deltaTime;
                playerTransform.position = Vector3.Lerp(stepStartPos, targetPos, Mathf.Clamp01(elapsed / timePerCell));
                yield return null;
            }

            playerTransform.position = targetPos;
            var (nx, nz) = visualManager.GridManager.GetCellGridPosition(next);
            playerGridPos = new Vector2Int(nx, nz);
        }

        isMoving = false;
        if (playerAnimator != null) playerAnimator.SetBool("Move", false);

        // 移动完成后，自动唤出朝向选择按钮
        TriggerOrientationUI();
    }

    /// <summary>
    /// 安全调用 PlayerOrientationController 弹出朝向 UI 界面
    /// </summary>
    private void TriggerOrientationUI()
    {
        if (PlayerOrientationController.Instance != null)
        {
            PlayerOrientationController.Instance.ShowButtons();
        }
    }
}
