using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections;

[DefaultExecutionOrder(100)]
public class MapInspector : MonoBehaviour
{
    public static MapInspector Instance { get; private set; }

    [Header("--- 1. 地图系统与Player引用 ---")]
    [Tooltip("格子管理器，用于查询玩家所在格和格子层级。留空时会在场景中自动查找。")]
    [SerializeField] private GridManager gridManager;
    [Tooltip("玩家移动控制器，用于获取玩家位置、移动状态并处理地图焦点。留空时会在场景中自动查找。")]
    [SerializeField] private PlayerMoveController playerMoveController;


    [Header("--- 2. 手牌缩放动画参数 ---")]
    [Tooltip("地图模式下需要收缩的手牌区域。")]
    public RectTransform handArea;
    [Tooltip("地图模式下手牌区域移动到的目标位置。")]
    public Transform handCollapsedTarget;
    [Tooltip("地图模式下手牌卡片的间距。")]
    public float collapsedCardSpacing = -140f;
    [Tooltip("地图模式下手牌缩小的比例 (默认不缩小，即为 1)")]
    public float collapsedHandScale = 1f;

    [Header("--- 3. 相机缩放参数 (正交相机) ---")]
    [Tooltip("切换地图模式的快捷键，默认为空格键。")]
    public Key mapShortcutKey = Key.Space;
    [Tooltip("战斗模式下相机的正交尺寸，数值越大视野越广。")]
    public float battleCameraSize = 5f;
    [Tooltip("地图模式下的相机正交尺寸，数值越大视野越广。")]
    public float mapInspectCameraSize = 4.5f;

    [Header("--- 4. 相机拖拽边界设置 ---")]
    [Tooltip("是否限制相机在自身 XY 平面内的移动范围。")]
    public bool enableBounds = true;
    [Tooltip("边界中心（可选）。留空时，以相机首次使用时的世界位置作为中心。")]
    [SerializeField] private Transform boundsCenter;
    [Tooltip("相对边界中心，沿相机自身 right 方向（屏幕左右）的最小偏移。")]
    public float minX = -30f;
    [Tooltip("相对边界中心，沿相机自身 right 方向（屏幕左右）的最大偏移。")]
    public float maxX = 30f;
    [Tooltip("相对边界中心，沿相机自身 up 方向（屏幕上下）的最小偏移。")]
    public float minY = -30f;
    [Tooltip("相对边界中心，沿相机自身 up 方向（屏幕上下）的最大偏移。")]
    public float maxY = 30f;

    [Header("--- 5. 地图移动灵敏度及缩放大小限制参数 ---")]
    [Tooltip("滚轮缩放灵敏度，数值越大缩放速度越快。")]
    public float scrollZoomSensitivity = 2f;
    [Tooltip("滚轮缩放允许的最小正交尺寸。")]
    public float minMapOrthoSize = 3f;
    [Tooltip("滚轮缩放允许的最大正交尺寸。")]
    public float maxMapOrthoSize = 15f;
    [Tooltip("WASD 平移视角的速度（每秒等效拖动的屏幕像素数，与中键拖拽使用同一套投影逻辑）")]
    public float keyboardPanSpeed = 600f;

    [Header("--- 6. 镜头焦点参数 ---")]
    [Tooltip("退出地图模式时相机聚焦的目标。留空时只恢复正交尺寸，不改变焦点。")]
    public Transform battleFocusTarget;


    // 内部状态缓存
    private Vector2 originalHandPos;
    private Vector3 originalHandScale;
    private bool isMapMode = false;
    private Vector3 dragOrigin;
    private bool isMoveFollowActive = false;
    private bool wasManualMoveInProgress = false;
    private Transform moveFollowTarget;
    private Vector3 lastMoveFollowTargetPosition;
    private Coroutine delayedFocusRoutine;

    // 边界中心缓存（boundsCenter 为空时使用）
    private Vector3 boundsOriginWorld;
    private bool boundsOriginCached = false;

    /// <summary>当前主相机：场景中名为 "Main Camera" 的相机（见 MainCameraAppoint 静态类）。</summary>
    private Camera mainCamera => MainCameraAppoint.Current;

    // UI 组件缓存
    private HorizontalLayoutGroup handLayoutGroup;
    private float originalSpacing;
    private CanvasGroup handCanvasGroup;
    private Tweener handPositionTween;
    private bool uiStateCached = false;

    private void Awake()
    {
        Instance = this;

        if (gridManager == null)
        {
            gridManager = FindFirstObjectByType<GridManager>();
        }

        if (playerMoveController == null)
        {
            playerMoveController = FindFirstObjectByType<PlayerMoveController>();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    IEnumerator Start()
    {
        EnsureBoundsOrigin();
        yield return new WaitForEndOfFrame();
        EnsureUICached();
        EnsureBoundsOrigin();
    }

    private void EnsureUICached()
    {
        if (uiStateCached) return;

        if (handArea != null)
        {
            originalHandPos = handArea.anchoredPosition;
            originalHandScale = handArea.localScale;

            handLayoutGroup = handArea.GetComponent<HorizontalLayoutGroup>();
            if (handLayoutGroup != null) originalSpacing = handLayoutGroup.spacing;

            handCanvasGroup = handArea.GetComponent<CanvasGroup>();
            if (handCanvasGroup == null) handCanvasGroup = handArea.gameObject.AddComponent<CanvasGroup>();
        }

        uiStateCached = true;
    }

    void Update()
    {
        if (IsCounterCameraLocked())
        {
            return;
        }

        bool canUseManualMapToggle = CanUseManualMapToggle();
        if (canUseManualMapToggle &&
            Keyboard.current != null &&
            Keyboard.current[mapShortcutKey].wasPressedThisFrame)
        {
            ToggleMapMode();
            return;
        }

        if (canUseManualMapToggle && TryRestoreBattleModeFromHandAreaClick())
        {
            return;
        }

        HandleCameraDrag();
        HandleKeyboardPan();
        HandleMapScrollZoom();
        HandleLeftClickToFocus();
    }

    private static bool CanUseManualMapToggle()
    {
        return TurnManager.Instance == null ||
               TurnManager.Instance.CurrentTurn == TurnState.Player;
    }

    private bool TryRestoreBattleModeFromHandAreaClick()
    {
        if (!isMapMode || Mouse.current == null || handArea == null)
        {
            return false;
        }

        if (!Mouse.current.leftButton.wasReleasedThisFrame ||
            !IsPointerOverHandArea(Mouse.current.position.ReadValue()))
        {
            return false;
        }

        SetMapMode(false);
        return true;
    }

    private bool IsPointerOverHandArea(Vector2 screenPosition)
    {
        Canvas canvas = handArea.GetComponentInParent<Canvas>();
        Camera uiCamera = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = canvas.worldCamera != null ? canvas.worldCamera : mainCamera;
        }

        if (RectTransformUtility.RectangleContainsScreenPoint(
                handArea,
                screenPosition,
                uiCamera))
        {
            return true;
        }

        // HandArea 的横向 sizeDelta 为 0，实际可点击范围由卡牌子节点撑开。
        foreach (Transform child in handArea)
        {
            RectTransform childRect = child as RectTransform;
            if (childRect == null || !childRect.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (RectTransformUtility.RectangleContainsScreenPoint(
                    childRect,
                    screenPosition,
                    uiCamera))
            {
                return true;
            }
        }

        return false;
    }

    private void LateUpdate()
    {
        if (IsCounterCameraLocked())
        {
            return;
        }

        if (!isMoveFollowActive ||
            moveFollowTarget == null ||
            playerMoveController == null ||
            mainCamera == null)
        {
            return;
        }

        Vector3 currentTargetPosition = moveFollowTarget.position;
        bool isManualMoving = playerMoveController.IsManualMoveInProgress;

        if (!isManualMoving)
        {
            Vector3 nonMoveDelta = currentTargetPosition - lastMoveFollowTargetPosition;

            if (wasManualMoveInProgress)
            {
                if (nonMoveDelta.sqrMagnitude > 0.000001f)
                {
                    TranslateCameraInPlane(nonMoveDelta);
                }
            }
            else if (nonMoveDelta.sqrMagnitude > 0.000001f)
            {
                ClearMoveFollowFocus();
                return;
            }

            lastMoveFollowTargetPosition = currentTargetPosition;
            wasManualMoveInProgress = false;
            return;
        }

        if (!wasManualMoveInProgress)
        {
            mainCamera.transform.DOKill();
            wasManualMoveInProgress = true;
            return;
        }

        Vector3 targetDelta = currentTargetPosition - lastMoveFollowTargetPosition;
        lastMoveFollowTargetPosition = currentTargetPosition;

        if (targetDelta.sqrMagnitude > 0.000001f)
        {
            TranslateCameraInPlane(targetDelta);
        }
    }

    private static bool IsCounterCameraLocked()
    {
        CounterTimeManager counterTimeManager = CounterTimeManager.Instance;
        return counterTimeManager != null && counterTimeManager.IsCounterStageActive;
    }

    /// <summary>
    /// 处理鼠标左键点击，仅在被角色占据的地格上自动聚焦
    /// </summary>
    private void HandleLeftClickToFocus()
    {
        if (Mouse.current == null || mainCamera == null || gridManager == null) return;

        // PlayerMoveController confirms player-cell clicks on release. Focus after
        // that frame's movement handling so moving the camera cannot change the release cell.
        if (!Mouse.current.leftButton.wasReleasedThisFrame) return;

        if (playerMoveController != null && playerMoveController.ConsumeMapFocusSuppression())
        {
            return;
        }

        if (playerMoveController != null && playerMoveController.IsUsingCard)
        {
            return;
        }

        CardDragController cardDragController = CardDragController.Instance;
        if (cardDragController != null &&
            (cardDragController.IsDragging || cardDragController.EndedCardDragThisFrame))
        {
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        if (playerMoveController != null && !playerMoveController.ShouldAllowMapFocus)
        {
            return;
        }

        Ray clickRay = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        int focusLayerMask = 1 << gridManager.cellLayer;

        if (!Physics.Raycast(clickRay, out RaycastHit hit, 1000f, focusLayerMask))
        {
            Debug.Log("[MapInspector] Left click ignored: ray did not hit a cell.");
            return;
        }

        CellManager hitCell = hit.collider != null
            ? hit.collider.GetComponentInParent<CellManager>()
            : null;

        if (hitCell == null)
        {
            Debug.Log("[MapInspector] Left click ignored: hit collider is not a grid cell.");
            return;
        }

        if (!hitCell.IsPlayerInside && !hitCell.HasMonsterInside)
        {
            Debug.Log("[MapInspector] Left click ignored: hit cell has no character inside.");
            return;
        }

        SetMoveFollowFocus(hitCell.IsPlayerInside && playerMoveController != null
            ? playerMoveController.PlayerTransform
            : null);

        FocusCameraOnWorldPosition(hit.point, 0.4f);
    }

    /// <summary>
    /// 立即聚焦到玩家所在格。
    /// </summary>
    public void FocusOnPlayerCell(float duration = 0.4f)
    {
        if (mainCamera == null || playerMoveController == null)
        {
            return;
        }

        if (gridManager == null)
        {
            return;
        }

        CellManager playerCell = ResolvePlayerCellFromGrid();
        if (playerCell == null)
        {
            return;
        }

        SetMoveFollowFocus(playerMoveController.PlayerTransform);
        FocusCameraOnWorldPosition(GetCellFocusPoint(playerCell), duration);
    }

    /// <summary>
    /// 推荐在“回合切换”时调用：等到本帧结束（其他脚本对相机/玩家的修改都已完成）后再聚焦，
    /// 避免敌方回合的收尾动画与聚焦互相覆盖。
    /// </summary>
    public void FocusOnPlayerCellDelayed(float duration = 0.4f, int waitFrames = 1)
    {
        if (delayedFocusRoutine != null) StopCoroutine(delayedFocusRoutine);
        delayedFocusRoutine = StartCoroutine(FocusOnPlayerCellRoutine(duration, waitFrames));
    }

    private IEnumerator FocusOnPlayerCellRoutine(float duration, int waitFrames)
    {
        for (int i = 0; i < Mathf.Max(1, waitFrames); i++)
        {
            yield return null;
        }

        yield return new WaitForEndOfFrame();
        delayedFocusRoutine = null;
        FocusOnPlayerCell(duration);
    }

    /// <summary>
    /// 取格子碰撞体顶面中心，与“左键点击时射线命中点”的高度保持一致；
    /// 没有碰撞体时退回到格子的 transform.position。
    /// </summary>
    private Vector3 GetCellFocusPoint(CellManager cell)
    {
        Collider col = cell.GetComponentInChildren<Collider>();
        if (col != null)
        {
            Bounds b = col.bounds;
            return new Vector3(b.center.x, b.max.y, b.center.z);
        }

        return cell.transform.position;
    }

    private CellManager ResolvePlayerCellFromGrid()
    {
        if (gridManager == null || playerMoveController == null)
        {
            return null;
        }

        Vector2Int playerGrid = playerMoveController.PlayerGridPos;
        CellManager trackedCell = playerMoveController.PlayerOccupiedCell;
        if (trackedCell != null)
        {
            var (trackedX, trackedZ) = gridManager.GetCellGridPosition(trackedCell);
            if (trackedX == playerGrid.x && trackedZ == playerGrid.y)
            {
                return trackedCell;
            }
        }

        int playerLayer = gridManager.GetCellLayer(trackedCell);
        return gridManager.GetCellManagerAt(playerGrid.x, playerGrid.y, playerLayer)
            ?? gridManager.GetCellManagerAt(playerGrid.x, playerGrid.y);
    }

    private void FocusCameraOnWorldPosition(Vector3 worldPosition, float duration)
    {
        Ray centerRay = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Plane focusPlane = new Plane(Vector3.up, new Vector3(0f, worldPosition.y, 0f));
        if (!focusPlane.Raycast(centerRay, out float centerEnter))
        {
            return;
        }

        Vector3 centerWorldPos = centerRay.GetPoint(centerEnter);
        Vector3 delta = worldPosition - centerWorldPos;
        delta.y = 0f;

        Transform cam = mainCamera.transform;
        Vector3 targetWorldPos = GetClampedCameraWorldPosition(cam.position + GetCameraPlaneDelta(delta));
        Vector3 targetLocalPos = cam.parent != null
            ? cam.parent.InverseTransformPoint(targetWorldPos)
            : targetWorldPos;

        cam.DOKill();
        cam.DOLocalMove(targetLocalPos, duration).SetEase(Ease.OutCubic);
    }

    private void SetMoveFollowFocus(Transform followTarget)
    {
        isMoveFollowActive = followTarget != null;
        moveFollowTarget = followTarget;
        wasManualMoveInProgress = false;
        lastMoveFollowTargetPosition = followTarget != null
            ? followTarget.position
            : Vector3.zero;
    }

    private void ClearMoveFollowFocus()
    {
        isMoveFollowActive = false;
        moveFollowTarget = null;
        wasManualMoveInProgress = false;
    }

    private void HandleCameraDrag()
    {
        if (Mouse.current == null || mainCamera == null) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        // 注意：目前拖拽使用的是中键 (middleButton)。如果你希望改成鼠标左键拖拽，可以顺便在这里调整。
        if (Mouse.current.middleButton.wasPressedThisFrame)
        {
            ClearMoveFollowFocus();
            mainCamera.transform.DOKill();
            if (TryGetGroundPoint(Mouse.current.position.ReadValue(), out Vector3 pressPoint))
            {
                dragOrigin = pressPoint;
            }
        }

        if (Mouse.current.middleButton.isPressed)
        {
            if (TryGetGroundPoint(Mouse.current.position.ReadValue(), out Vector3 currentPos))
            {
                Vector3 difference = dragOrigin - currentPos;
                TranslateCameraInPlane(difference);
            }
        }
    }

    /// <summary>
    /// 处理键盘 WASD 平移视角：与中键拖拽保持完全一致的逻辑（同一套地面投影换算 + 相同的边界限制）。
    /// </summary>
    private void HandleKeyboardPan()
    {
        if (Keyboard.current == null || mainCamera == null) return;

        float horizontal = 0f;
        float vertical = 0f;
        if (Keyboard.current.aKey.isPressed) horizontal -= 1f;
        if (Keyboard.current.dKey.isPressed) horizontal += 1f;
        if (Keyboard.current.sKey.isPressed) vertical -= 1f;
        if (Keyboard.current.wKey.isPressed) vertical += 1f;

        if (Mathf.Approximately(horizontal, 0f) && Mathf.Approximately(vertical, 0f)) return;

        ClearMoveFollowFocus();
        mainCamera.transform.DOKill();

        Vector2 screenDelta = new Vector2(horizontal, vertical).normalized
            * keyboardPanSpeed * Time.unscaledDeltaTime;

        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        if (TryGetGroundPoint(screenCenter, out Vector3 before) &&
            TryGetGroundPoint(screenCenter + screenDelta, out Vector3 after))
        {
            TranslateCameraInPlane(after - before);
        }
    }

    /// <summary>
    /// 将屏幕坐标投影到 y = 0 的地面平面上，成功时返回对应的世界坐标。
    /// </summary>
    private bool TryGetGroundPoint(Vector2 screenPosition, out Vector3 groundPoint)
    {
        groundPoint = Vector3.zero;
        if (mainCamera == null) return false;

        Ray ray = mainCamera.ScreenPointToRay(screenPosition);
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        if (!groundPlane.Raycast(ray, out float enter)) return false;

        groundPoint = ray.GetPoint(enter);
        return true;
    }

    private void HandleMapScrollZoom()
    {
        if (Mouse.current == null || mainCamera == null) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        float scrollValue = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scrollValue) > 0.01f)
        {
            ClearMoveFollowFocus();
            mainCamera.transform.DOKill();

            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            Ray rayBefore = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (groundPlane.Raycast(rayBefore, out float enterBefore))
            {
                Vector3 worldPosBefore = rayBefore.GetPoint(enterBefore);

                float targetSize = mainCamera.orthographicSize - (scrollValue * 0.001f * scrollZoomSensitivity);
                mainCamera.orthographicSize = Mathf.Clamp(targetSize, minMapOrthoSize, maxMapOrthoSize);

                Ray rayAfter = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
                if (groundPlane.Raycast(rayAfter, out float enterAfter))
                {
                    Vector3 worldPosAfter = rayAfter.GetPoint(enterAfter);
                    TranslateCameraInPlane(worldPosBefore - worldPosAfter);
                }
            }

            if (enableBounds) ClampCameraPosition();
        }
    }

    // ------------------------------------------------------------------
    // 相机平面约束：相机始终停留在"当前自身 XY 平面"内（法线 = camera.forward）。
    // 边界 minX/maxX 对应相机自身 right 方向，minY/maxY 对应相机自身 up 方向，
    // 均相对于边界中心（boundsCenter 或首次使用时的相机位置）。
    // ------------------------------------------------------------------

    /// <summary>
    /// 把世界位移投影到相机自身 XY 平面（去掉沿 forward 的分量），返回世界空间位移。
    /// </summary>
    private Vector3 GetCameraPlaneDelta(Vector3 worldDelta)
    {
        Vector3 f = mainCamera.transform.forward;
        return worldDelta - f * Vector3.Dot(worldDelta, f);
    }

    private void EnsureBoundsOrigin()
    {
        if (boundsOriginCached || mainCamera == null) return;
        boundsOriginWorld = mainCamera.transform.position;
        boundsOriginCached = true;
    }

    private Vector3 GetBoundsOrigin()
    {
        if (boundsCenter != null) return boundsCenter.position;
        EnsureBoundsOrigin();
        return boundsOriginWorld;
    }

    /// <summary>需要重新以当前相机位置作为边界中心时可手动调用（仅在 boundsCenter 为空时有效）。</summary>
    public void ResetBoundsOrigin()
    {
        boundsOriginCached = false;
        EnsureBoundsOrigin();
    }

    private void TranslateCameraInPlane(Vector3 worldDelta)
    {
        Transform cam = mainCamera.transform;
        cam.position = GetClampedCameraWorldPosition(cam.position + GetCameraPlaneDelta(worldDelta));
    }

    /// <summary>
    /// 在相机自身 right/up 方向上按 min/max 限制偏移；
    /// forward 方向的分量保持为"当前相机所在 XY 平面"，不做任何限制。
    /// </summary>
    private Vector3 GetClampedCameraWorldPosition(Vector3 worldPos)
    {
        if (!enableBounds) return worldPos;

        Transform cam = mainCamera.transform;
        Vector3 origin = GetBoundsOrigin();
        Vector3 r = cam.right;
        Vector3 u = cam.up;
        Vector3 f = cam.forward;

        Vector3 offset = worldPos - origin;
        float x = Mathf.Clamp(Vector3.Dot(offset, r), minX, maxX);
        float y = Mathf.Clamp(Vector3.Dot(offset, u), minY, maxY);
        float z = Vector3.Dot(cam.position - origin, f); // 保持在当前平面内

        return origin + r * x + u * y + f * z;
    }

    private void ClampCameraPosition()
    {
        Transform cam = mainCamera.transform;
        cam.position = GetClampedCameraWorldPosition(cam.position);
    }

    // ------------------------------------------------------------------
    // 地图模式切换
    // ------------------------------------------------------------------

    public void ToggleMapMode()
    {
        if (!CanUseManualMapToggle())
        {
            return;
        }

        SetMapMode(!isMapMode);
    }

    public void SetMapMode(bool enableMap)
    {
        if (isMapMode == enableMap) return;
        isMapMode = enableMap;
        ClearMoveFollowFocus();

        EnsureUICached();

        if (handArea != null) DOTween.Kill(handArea);
        if (handLayoutGroup != null) DOTween.Kill(handLayoutGroup);
        handPositionTween = null;
        if (mainCamera != null) { mainCamera.DOKill(); mainCamera.transform.DOKill(); }

        if (isMapMode)
        {
            if (handArea != null && handCollapsedTarget != null)
            {
                handPositionTween = handArea
                    .DOAnchorPos(GetCollapsedHandAnchoredPosition(), 0.4f)
                    .SetEase(Ease.OutCubic);
                handArea.DOScale(collapsedHandScale, 0.4f);

                if (handCanvasGroup != null) handCanvasGroup.blocksRaycasts = false;
                if (handLayoutGroup != null)
                {
                    DOTween.To(() => handLayoutGroup.spacing, x => handLayoutGroup.spacing = x, collapsedCardSpacing, 0.4f).SetEase(Ease.OutCubic);
                }
            }

            if (mainCamera != null) mainCamera.DOOrthoSize(mapInspectCameraSize, 0.4f).SetEase(Ease.OutCubic);
        }
        else
        {
            if (handArea != null)
            {
                handPositionTween = handArea
                    .DOAnchorPos(originalHandPos, 0.4f)
                    .SetEase(Ease.OutCubic);
                handArea.DOScale(originalHandScale, 0.4f);
                if (handCanvasGroup != null) handCanvasGroup.blocksRaycasts = true;
                if (handLayoutGroup != null)
                {
                    DOTween.To(() => handLayoutGroup.spacing, x => handLayoutGroup.spacing = x, originalSpacing, 0.4f).SetEase(Ease.OutCubic);
                }
            }

            if (mainCamera != null)
            {
                mainCamera.DOOrthoSize(battleCameraSize, 0.4f).SetEase(Ease.OutCubic);

                if (battleFocusTarget != null)
                {
                    Collider playerCol = battleFocusTarget.GetComponentInChildren<Collider>();
                    Vector3 focusPoint = playerCol != null ? playerCol.bounds.center : battleFocusTarget.position;

                    // 与左键聚焦共用同一套"屏幕中心 -> 目标点"的位移计算。
                    FocusCameraOnWorldPosition(focusPoint, 0.4f);
                }
            }
        }
    }

    /// <summary>
    /// 确保手牌区处于战斗模式，并等待其位置动画完成。
    /// 供回合换牌动画在启动前调用，避免动画层使用地图模式下的旧坐标。
    /// </summary>
    public IEnumerator RestoreHandToBattleMode()
    {
        SetMapMode(false);

        Tweener activePositionTween = handPositionTween;
        if (activePositionTween != null && activePositionTween.IsActive())
        {
            yield return activePositionTween.WaitForCompletion();
        }
    }

    /// <summary>
    /// 将地图模式目标点的世界坐标转换为手牌区域在父 Canvas 下的锚点坐标。
    /// 避免敌方回合移动相机时，世界坐标补间导致手牌偏移到屏幕外。
    /// </summary>
    private Vector2 GetCollapsedHandAnchoredPosition()
    {
        RectTransform parentRect = handArea.parent as RectTransform;
        if (parentRect == null)
        {
            return handArea.anchoredPosition;
        }

        Vector3 targetLocalPosition =
            parentRect.InverseTransformPoint(handCollapsedTarget.position);
        Vector2 anchorReference = new Vector2(
            Mathf.Lerp(parentRect.rect.xMin, parentRect.rect.xMax, handArea.anchorMin.x),
            Mathf.Lerp(parentRect.rect.yMin, parentRect.rect.yMax, handArea.anchorMin.y));

        return new Vector2(targetLocalPosition.x, targetLocalPosition.y) - anchorReference;
    }
}
