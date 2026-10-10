using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems; // 引入 UI 事件系统

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

    [Header("移动悬停预览")]
    [Tooltip("鼠标悬停在可移动格上时显示的玩家预览预制体")]
    [SerializeField] private GameObject movePreviewPrefab;
    private const float MovePreviewHeightOffset = 0.653f;

    private bool isMoving = false;
    public bool IsMoving => isMoving;

    private Vector2Int currentClickedGrid = new Vector2Int(-1, -1);
    private CellManager currentClickedCell;
    private Vector2Int playerGridPos = new Vector2Int(-1, -1);
    private HashSet<CellManager> reachableGridSet = new HashSet<CellManager>();
    private HashSet<CellManager> pendingMoveAllowedCells;
    private List<CellManager> pendingMovePath;
    private HashSet<CellManager> pendingMovePathCells;
    private bool hasPendingMoveFacing = false;
    private bool hasMovePreviewDirectionOverride = false;
    private float movePreviewOverrideHorizontal;
    private float movePreviewOverrideVertical;
    private GameObject movePreviewInstance;
    private Animator movePreviewAnimator;
    private SpriteRenderer movePreviewSpriteRenderer;
    private SpriteRenderer playerSpriteRenderer;
    private CellManager movePreviewCell;

    private static readonly int HorizontalHash = Animator.StringToHash("Horizontal");
    private static readonly int VerticalHash = Animator.StringToHash("Vertical");

    // 【长按/短按判定】按下玩家格时先挂起，抬起时区分“短按点击（显示范围）”与“长按（显示朝向按钮）”
    private CellManager pendingPressCell;
    private float pressStartTime = -1f;

    public bool IsUsingCard { get; set; } = false;

    private PointerEventData cachedPointerEventData;
    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();

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
            HideMovePreview();
            if (currentClickedCell != null)
                ClearClickedGrid();
            return;
        }

        SyncMovePreviewDirection();

        if (Application.isPlaying && visualManager != null && visualManager.GridManager != null)
            HandleGridInteraction();
    }

    public void OnTurnActivated() { }

    public void OnTurnDeactivated()
    {
        pendingPressCell = null;
        pressStartTime = -1f;
        CancelPendingMoveFacing();
        HideMovePreview();
        if (currentClickedCell != null)
            ClearClickedGrid();
        ClearPath();
    }

    public void ForceClearHoverState()
    {
        HideMovePreview();
        ClearPath();
        if (GridHoverController.Instance != null)
            GridHoverController.Instance.ForceRestoreHover();
    }

    private void LateUpdate()
    {
        SyncMovePreviewSprite();
    }

    private void OnDestroy()
    {
        if (movePreviewInstance != null)
            Destroy(movePreviewInstance);
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
    }

    // ===================================================================
    // 普通点击移动交互
    // ===================================================================

    private void HandleGridInteraction()
    {
        if (hasPendingMoveFacing)
            return;

        if (IsUsingCard || isMoving)
        {
            HideMovePreview();
            ClearPath();
            return;
        }

        if (Mouse.current == null) return;

        // 只让屏幕空间 UI 阻断地块交互；怪物血条等 World Space UI 不应挡住移动操作。
        if (IsPointerOverBlockingUI())
        {
            HideMovePreview();
            ClearPath();
            if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                pendingPressCell = null;
                pressStartTime = -1f;
            }
            return;
        }

        GridHoverController hoverController = GridHoverController.Instance;
        CellManager hoverCell = hoverController != null
            ? hoverController.CurrentHoverCell
            : null;
        bool isValidHover = hoverCell != null;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (isValidHover)
            {
                CellManager cellUnderMouse = hoverCell;

                if (visualManager.IsPlayerOnCell(cellUnderMouse))
                {
                    pendingPressCell = cellUnderMouse;
                    pressStartTime = Time.time;
                }
                else if (cellUnderMouse != null && reachableGridSet.Contains(cellUnderMouse))
                {
                    if (cellUnderMouse.BlocksPlayer)
                        Debug.Log("[PlayerMoveController] 目标格为特殊地形或已锁死，无法进入！");
                    else if (cellUnderMouse.HasMonsterInside)
                        Debug.Log("[PlayerMoveController] 目标格有怪物，无法进入！");
                    else
                    {
                        BeginMoveFacingSelection(currentClickedCell, cellUnderMouse);
                        return;
                    }
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

        if (Mouse.current.leftButton.wasReleasedThisFrame && pendingPressCell != null)
        {
            bool isShortPress = Time.time - pressStartTime < ClickHoldThreshold;
            bool menuVisible = PlayerOrientationController.Instance != null
                && PlayerOrientationController.Instance.ButtonsVisible;

            CellManager releaseCell = hoverController != null
                ? hoverController.CurrentHoverCell
                : null;
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
            bool inRange = reachableGridSet.Contains(hoverCell);
            if (inRange && hoverCell != currentClickedCell)
            {
                DrawPathToTarget(currentClickedCell, hoverCell);
                ShowMovePreview(hoverCell);
            }
            else
            {
                ClearPath();
                HideMovePreview();
            }
        }
        else
        {
            ClearPath();
            HideMovePreview();
        }
    }

    private void BeginMoveFacingSelection(CellManager startCell, CellManager endCell)
    {
        if (startCell == null || endCell == null)
            return;

        if (PlayerOrientationController.Instance != null)
        {
            List<CellManager> path = MoveSystem.FindPathAStarCells(
                startCell,
                endCell,
                reachableGridSet,
                visualManager.GridManager);
            if (path == null || path.Count <= 1)
                return;

            pendingMoveAllowedCells = new HashSet<CellManager>(reachableGridSet);
            pendingMovePath = path;
            pendingMovePathCells = new HashSet<CellManager>(path);
            hasPendingMoveFacing = true;
            PlayerOrientationController.Instance.EnterMoveFacingMode(startCell, endCell);
            return;
        }

        StartCoroutine(MovePlayerAlongPathRoutine(startCell, endCell, reachableGridSet));
    }

    private bool IsPointerOverBlockingUI()
    {
        if (EventSystem.current == null || Mouse.current == null)
        {
            return false;
        }

        if (cachedPointerEventData == null)
        {
            cachedPointerEventData = new PointerEventData(EventSystem.current);
        }

        cachedPointerEventData.position = Mouse.current.position.ReadValue();
        uiRaycastResults.Clear();
        EventSystem.current.RaycastAll(cachedPointerEventData, uiRaycastResults);

        foreach (RaycastResult result in uiRaycastResults)
        {
            GameObject hitObject = result.gameObject;
            if (hitObject == null)
            {
                continue;
            }

            Canvas canvas = hitObject.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private void SetClickedGrid(int x, int z, CellManager clickedCell)
    {
        HideMovePreview();
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

    public void ClearClickedGrid()
    {
        pendingPressCell = null;
        pressStartTime = -1f;
        HideMovePreview();

        visualManager.ResetAllCellsVisuals();
        currentClickedGrid = new Vector2Int(-1, -1);
        currentClickedCell = null;
        reachableGridSet.Clear();
        ClearPath();

        if (GridHoverController.Instance != null)
        {
            GridHoverController.Instance.ClearHoverLockedCells();
            GridHoverController.Instance.ClearContext();
        }
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

    private void ShowMovePreview(CellManager targetCell)
    {
        if (movePreviewPrefab == null || targetCell == null)
        {
            HideMovePreview();
            return;
        }

        if (movePreviewInstance == null)
        {
            movePreviewInstance = Instantiate(movePreviewPrefab);
            movePreviewInstance.name = $"{movePreviewPrefab.name} (Runtime Preview)";
            movePreviewAnimator = movePreviewInstance.GetComponentInChildren<Animator>(true);
            movePreviewSpriteRenderer = movePreviewInstance.GetComponentInChildren<SpriteRenderer>(true);
            movePreviewInstance.SetActive(false);
        }

        if (movePreviewCell == targetCell && movePreviewInstance.activeSelf)
        {
            SyncMovePreviewDirection();
            return;
        }

        bool wasActive = movePreviewInstance.activeSelf;
        movePreviewCell = targetCell;
        Vector3 previewPosition = targetCell.transform.position;
        previewPosition.y = targetCell.transform.position.y + MovePreviewHeightOffset;
        movePreviewInstance.transform.position = previewPosition;
        movePreviewInstance.SetActive(true);

        if (!wasActive && movePreviewAnimator != null)
        {
            movePreviewAnimator.Rebind();
            movePreviewAnimator.Update(0f);
        }

        SyncMovePreviewDirection();
    }

    private void SyncMovePreviewDirection()
    {
        if (movePreviewInstance == null || !movePreviewInstance.activeSelf || movePreviewAnimator == null)
            return;

        if (hasMovePreviewDirectionOverride)
        {
            ApplyMovePreviewDirection(movePreviewOverrideHorizontal, movePreviewOverrideVertical);
            return;
        }

        if (playerAnimator == null && playerTransform != null)
            playerAnimator = playerTransform.GetComponentInChildren<Animator>();
        if (playerAnimator == null)
            return;

        ApplyMovePreviewDirection(
            playerAnimator.GetFloat(HorizontalHash),
            playerAnimator.GetFloat(VerticalHash));
    }

    public void PreviewMoveFacingDirection(float horizontal, float vertical)
    {
        if (!hasPendingMoveFacing || movePreviewAnimator == null || !movePreviewInstance.activeSelf)
            return;

        hasMovePreviewDirectionOverride = true;
        movePreviewOverrideHorizontal = horizontal;
        movePreviewOverrideVertical = vertical;
        ApplyMovePreviewDirection(horizontal, vertical);
    }

    public void RestoreMovePreviewDirection()
    {
        if (!hasMovePreviewDirectionOverride)
            return;

        hasMovePreviewDirectionOverride = false;
        SyncMovePreviewDirection();
        SyncMovePreviewSprite();
    }

    private void ApplyMovePreviewDirection(float horizontal, float vertical)
    {
        movePreviewAnimator.SetFloat(HorizontalHash, horizontal);
        movePreviewAnimator.SetFloat(VerticalHash, vertical);
        movePreviewAnimator.Update(0f);
        SyncMovePreviewSprite();
    }

    private void SyncMovePreviewSprite()
    {
        if (movePreviewInstance == null || !movePreviewInstance.activeSelf || movePreviewSpriteRenderer == null)
            return;
        if (hasMovePreviewDirectionOverride)
            return;

        if (playerSpriteRenderer == null)
        {
            if (playerAnimator != null)
                playerSpriteRenderer = playerAnimator.GetComponent<SpriteRenderer>()
                    ?? playerAnimator.GetComponentInChildren<SpriteRenderer>(true);
            if (playerSpriteRenderer == null && playerTransform != null)
                playerSpriteRenderer = playerTransform.GetComponentInChildren<SpriteRenderer>(true);
        }

        if (playerSpriteRenderer == null)
            return;

        movePreviewSpriteRenderer.sprite = playerSpriteRenderer.sprite;
        movePreviewSpriteRenderer.flipX = playerSpriteRenderer.flipX;
        movePreviewSpriteRenderer.flipY = playerSpriteRenderer.flipY;
    }

    private void HideMovePreview()
    {
        movePreviewCell = null;
        hasMovePreviewDirectionOverride = false;
        if (movePreviewInstance != null)
            movePreviewInstance.SetActive(false);
    }

    private void DrawPathToTarget(CellManager startCell, CellManager endCell)
    {
        List<CellManager> path = MoveSystem.FindPathAStarCells(startCell, endCell, reachableGridSet, visualManager.GridManager);
        DrawPath(path);
    }

    private void DrawPath(List<CellManager> path)
    {
        if (pathLineRenderer == null) return;
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
    /// 位移朝向选择完成后执行移动，并通过回调通知 PlayerOrientationController 应用最终朝向。
    /// </summary>
    public void MovePlayerAlongPathAfterOrientation(
        CellManager startCell,
        CellManager endCell,
        Action<bool> onFinished)
    {
        if (!hasPendingMoveFacing)
        {
            onFinished?.Invoke(false);
            return;
        }

        HashSet<CellManager> allowedCells = pendingMoveAllowedCells;
        ClearPendingMoveFacing();
        StartCoroutine(MovePlayerAlongPathRoutine(startCell, endCell, allowedCells, onFinished));
    }

    public void CancelPendingMoveFacing()
    {
        bool hadPendingMove = hasPendingMoveFacing;
        ClearPendingMoveFacing();
        if (hadPendingMove)
            ClearClickedGrid();
    }

    public void PrepareMoveFacingPreview(CellManager targetCell)
    {
        if (targetCell == null)
            return;

        ClearClickedGrid();
        DrawPath(pendingMovePath);

        if (pendingMovePathCells != null && pendingMovePathCells.Count > 0)
        {
            CellManager startCell = pendingMovePath != null && pendingMovePath.Count > 0
                ? pendingMovePath[0]
                : null;
            visualManager.SetReachablePatternColors(
                pendingMovePathCells,
                startCell,
                playerMoveStyle,
                isRuntime: true);

            if (GridHoverController.Instance != null)
                GridHoverController.Instance.SetHoverLockedCells(pendingMovePathCells);
        }

        ShowMovePreview(targetCell);
    }

    private void ClearPendingMoveFacing()
    {
        pendingMoveAllowedCells = null;
        pendingMovePath = null;
        pendingMovePathCells = null;
        hasPendingMoveFacing = false;
        hasMovePreviewDirectionOverride = false;

        if (GridHoverController.Instance != null)
            GridHoverController.Instance.ClearHoverLockedCells();
    }

    private IEnumerator MovePlayerAlongPathRoutine(
        CellManager startCell,
        CellManager endCell,
        HashSet<CellManager> allowedCells,
        Action<bool> onFinished = null)
    {
        if (playerTransform == null)
        {
            onFinished?.Invoke(false);
            yield break;
        }

        List<CellManager> path = MoveSystem.FindPathAStarCells(startCell, endCell, allowedCells, visualManager.GridManager);
        if (path == null || path.Count <= 1)
        {
            onFinished?.Invoke(false);
            yield break;
        }

        int stepCount = path.Count - 1;

        if (CombatStatsManager.Instance != null && !CombatStatsManager.Instance.HasEnoughActionPoint(stepCount))
        {
            Debug.LogWarning($"[PlayerMoveController] ActionPoint 不足！需要 {stepCount} 点，当前只有 {CombatStatsManager.Instance.currentActionPoint} 点！");
            onFinished?.Invoke(false);
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

        onFinished?.Invoke(true);
    }
}
