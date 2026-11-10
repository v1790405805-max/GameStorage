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

    [Header("--- 1. 交互与目标设置 ---")]
    [Tooltip("左键聚焦功能使用的层级，请勾选地图格子所在的 Layer")]
    public LayerMask mapLayerMask;
    [SerializeField] private GridManager gridManager;
    [SerializeField] private PlayerMoveController playerMoveController;
    public Key mapShortcutKey = Key.Space;

    [Header("--- 2. UI 联动与动画参数 ---")]
    public RectTransform handArea;
    public Transform handCollapsedTarget;
    public CanvasGroup nonEssentialHUD;
    public float collapsedCardSpacing = -60f;
    [Tooltip("地图模式下手牌缩小的比例 (默认 0.5，即缩小一半)")]
    public float collapsedHandScale = 0.5f;

    [Header("--- 3. 相机缩放参数 (正交相机) ---")]
    public float battleCameraSize = 5f;
    public float mapInspectCameraSize = 10f;

    [Header("--- 4. 相机拖拽与边界参数 ---")]
    [Tooltip("如果点击边缘格子没反应，请把下面的 Min 和 Max 调大！或者直接取消勾选此项测试")]
    public bool enableBounds = true;
    public float minX = -30f, maxX = 30f;
    public float minY = -30f, maxY = 30f;
    [Tooltip("WASD 平移视角的速度（每秒等效拖动的屏幕像素数，与中键拖拽使用同一套投影逻辑）")]
    public float keyboardPanSpeed = 600f;

    [Header("--- 5. 镜头焦点参数 ---")]
    public Transform battleFocusTarget;

    [Header("--- 6. 地图滚轮缩放参数 ---")]
    public float minMapOrthoSize = 3f;
    public float maxMapOrthoSize = 15f;
    public float scrollZoomSensitivity = 2f;

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

    /// <summary>当前主相机：场景中名为 "Main Camera" 的相机（见 MainCameraAppoint 静态类）。</summary>
    private Camera mainCamera => MainCameraAppoint.Current;

    // UI 组件缓存
    private HorizontalLayoutGroup handLayoutGroup;
    private float originalSpacing;
    private CanvasGroup handCanvasGroup;
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
        yield return new WaitForEndOfFrame();
        EnsureUICached();
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

        if (Keyboard.current != null && Keyboard.current[mapShortcutKey].wasPressedThisFrame)
        {
            ToggleMapMode();
            return;
        }

        HandleCameraDrag();
        HandleKeyboardPan();
        HandleMapScrollZoom();
        HandleLeftClickToFocus();
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
                    TranslateCameraInLocalXY(nonMoveDelta);
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
            TranslateCameraInLocalXY(targetDelta);
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
        if (Mouse.current == null || mainCamera == null) return;

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

        int focusLayerMask = gridManager != null
            ? 1 << gridManager.cellLayer
            : mapLayerMask.value;

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

        Vector3 targetLocalPos = mainCamera.transform.localPosition + GetCameraLocalDelta(delta);
        targetLocalPos = GetClampedCameraLocalPosition(targetLocalPos);

        mainCamera.transform.DOKill();
        mainCamera.transform.DOLocalMove(targetLocalPos, duration).SetEase(Ease.OutCubic);
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
                TranslateCameraInLocalXY(difference);
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
            TranslateCameraInLocalXY(after - before);
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
                    TranslateCameraInLocalXY(worldPosBefore - worldPosAfter);
                }
            }

            if (enableBounds) ClampCameraLocalPosition();
        }
    }

    // ------------------------------------------------------------------
    // 相机平面约束：相机始终停留在"当前自身 XY 平面"内（法线 = camera.forward）
    //
    // 【修复说明】
    // 旧版本在启动时把相机位置缓存为 mapPlaneOrigin，之后所有移动都被"拉回"这个固定平面。
    // 一旦敌方回合中有任何东西改动了相机/父物体的位置或朝向，固定平面就过期了，
    // 补偿量 t 不再为 0，会变成屏幕上的偏移（聚焦位置偏离）。
    // 现在改为：每次移动都以"移动前相机的当前位置"作为平面基准，
    // 因此无论外部如何改动相机，都不会产生残留的偏移。
    // ------------------------------------------------------------------

    /// <summary>
    /// 把地面上的世界位移，投影到相机自身 XY 平面（垂直于 camera.forward）上，
    /// 再转换为父空间下的位移。正交相机下，这等价于屏幕上的对应位移。
    /// </summary>
    private Vector3 GetCameraLocalDelta(Vector3 worldDelta)
    {
        Transform cam = mainCamera.transform;
        Vector3 f = cam.forward;
        Vector3 planeDelta = worldDelta - f * Vector3.Dot(worldDelta, f);

        return cam.parent != null
            ? cam.parent.InverseTransformVector(planeDelta)
            : planeDelta;
    }

    private void TranslateCameraInLocalXY(Vector3 worldDelta)
    {
        Transform cam = mainCamera.transform;
        Vector3 targetLocalPos = cam.localPosition + GetCameraLocalDelta(worldDelta);
        cam.localPosition = GetClampedCameraLocalPosition(targetLocalPos);
    }

    /// <summary>
    /// 先按 minX/maxX/minY/maxY 限制 X、Y，
    /// 再沿父物体的本地 Z 轴调整，使结果落在"当前相机位置所在的 XY 平面"内。
    /// </summary>
    private Vector3 GetClampedCameraLocalPosition(Vector3 localPosition)
    {
        Transform cam = mainCamera.transform;
        Transform parent = cam.parent;

        // 以调用时相机的实时位置作为平面基准，而不是启动时缓存的位置
        Vector3 planeOrigin = cam.position;
        Vector3 f = cam.forward;

        if (enableBounds)
        {
            localPosition.x = Mathf.Clamp(localPosition.x, minX, maxX);
            localPosition.y = Mathf.Clamp(localPosition.y, minY, maxY);
        }

        Vector3 worldPos = parent != null ? parent.TransformPoint(localPosition) : localPosition;
        Vector3 zAxisWorld = parent != null ? parent.TransformVector(Vector3.forward) : Vector3.forward;

        float denom = Vector3.Dot(zAxisWorld, f);
        if (Mathf.Abs(denom) < 0.0001f) return localPosition; // 退化情况：无法补偿

        // 沿父物体 Z 轴移动 t，使 (worldPos + t * zAxis - planeOrigin) · f = 0
        float t = -Vector3.Dot(worldPos - planeOrigin, f) / denom;
        worldPos += zAxisWorld * t;

        return parent != null ? parent.InverseTransformPoint(worldPos) : worldPos;
    }

    private void ClampCameraLocalPosition()
    {
        Transform cam = mainCamera.transform;
        cam.localPosition = GetClampedCameraLocalPosition(cam.localPosition);
    }

    // ------------------------------------------------------------------
    // 地图模式切换
    // ------------------------------------------------------------------

    public void ToggleMapMode()
    {
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
        if (mainCamera != null) { mainCamera.DOKill(); mainCamera.transform.DOKill(); }
        if (nonEssentialHUD != null) DOTween.Kill(nonEssentialHUD);

        if (isMapMode)
        {
            if (handArea != null && handCollapsedTarget != null)
            {
                handArea.DOMove(handCollapsedTarget.position, 0.4f).SetEase(Ease.OutCubic);
                handArea.DOScale(collapsedHandScale, 0.4f);

                if (handCanvasGroup != null) handCanvasGroup.blocksRaycasts = false;
                if (handLayoutGroup != null)
                {
                    DOTween.To(() => handLayoutGroup.spacing, x => handLayoutGroup.spacing = x, collapsedCardSpacing, 0.4f).SetEase(Ease.OutCubic);
                }
            }

            if (nonEssentialHUD != null)
            {
                nonEssentialHUD.DOFade(0f, 0.3f);
                nonEssentialHUD.blocksRaycasts = false;
            }

            if (mainCamera != null) mainCamera.DOOrthoSize(mapInspectCameraSize, 0.4f).SetEase(Ease.OutCubic);
        }
        else
        {
            if (handArea != null)
            {
                handArea.DOAnchorPos(originalHandPos, 0.4f).SetEase(Ease.OutCubic);
                handArea.DOScale(originalHandScale, 0.4f);
                if (handCanvasGroup != null) handCanvasGroup.blocksRaycasts = true;
                if (handLayoutGroup != null)
                {
                    DOTween.To(() => handLayoutGroup.spacing, x => handLayoutGroup.spacing = x, originalSpacing, 0.4f).SetEase(Ease.OutCubic);
                }
            }

            if (nonEssentialHUD != null)
            {
                nonEssentialHUD.DOFade(1f, 0.3f);
                nonEssentialHUD.blocksRaycasts = true;
            }

            if (mainCamera != null)
            {
                mainCamera.DOOrthoSize(battleCameraSize, 0.4f).SetEase(Ease.OutCubic);

                if (battleFocusTarget != null)
                {
                    Collider playerCol = battleFocusTarget.GetComponentInChildren<Collider>();
                    Vector3 focusPoint = playerCol != null ? playerCol.bounds.center : battleFocusTarget.position;

                    // 与左键聚焦共用同一套"屏幕中心 -> 目标点"的位移计算，
                    // 不再依赖启动时缓存的 cameraOffset（它同样会在相机被外部改动后过期）。
                    FocusCameraOnWorldPosition(focusPoint, 0.4f);
                }
            }
        }
    }
}
