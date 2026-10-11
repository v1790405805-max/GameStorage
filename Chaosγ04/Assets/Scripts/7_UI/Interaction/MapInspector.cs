using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections;

[DefaultExecutionOrder(100)]
public class MapInspector : MonoBehaviour
{
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
    private Vector3 cameraOffset;

    /// <summary>当前主相机：场景中名为 "Main Camera" 的相机（见 MainCameraAppoint 静态类）。</summary>
    private Camera mainCamera => MainCameraAppoint.Current;

    // UI 组件缓存
    private HorizontalLayoutGroup handLayoutGroup;
    private float originalSpacing;
    private CanvasGroup handCanvasGroup;
    private bool uiStateCached = false;

    private void Awake()
    {
        if (gridManager == null)
        {
            gridManager = FindFirstObjectByType<GridManager>();
        }

        if (playerMoveController == null)
        {
            playerMoveController = FindFirstObjectByType<PlayerMoveController>();
        }
    }

    IEnumerator Start()
    {
        if (mainCamera != null)
        {
            Ray centerRay = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (groundPlane.Raycast(centerRay, out float enter))
            {
                Vector3 screenCenterGroundPos = centerRay.GetPoint(enter);
                cameraOffset = mainCamera.transform.position - screenCenterGroundPos;
            }
        }

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

    /// <summary>
    /// 处理鼠标左键点击，自动聚焦到点击的地块
    /// </summary>
    private void HandleLeftClickToFocus()
    {
        if (Mouse.current == null || mainCamera == null) return;

        // PlayerMoveController confirms player-cell clicks on release. Focus after
        // that frame's movement handling so moving the camera cannot change the release cell.
        if (!Mouse.current.leftButton.wasReleasedThisFrame) return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        if (playerMoveController != null && !playerMoveController.ShouldAllowMapFocus)
        {
            return;
        }

        Ray clickRay = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        Ray centerRay = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

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

        Vector3 clickWorldPos = hit.point;
        float targetHeight = hit.point.y;

        Plane focusPlane = new Plane(Vector3.up, new Vector3(0f, targetHeight, 0f));
        if (!focusPlane.Raycast(centerRay, out float centerEnter)) return;

        Vector3 centerWorldPos = centerRay.GetPoint(centerEnter);
        Vector3 delta = clickWorldPos - centerWorldPos;
        delta.y = 0f;

        Vector3 targetCamPos = mainCamera.transform.position + delta;

        if (enableBounds)
        {
            targetCamPos.x = Mathf.Clamp(targetCamPos.x, minX, maxX);
            targetCamPos.z = Mathf.Clamp(targetCamPos.z, minY, maxY);
        }

        mainCamera.transform.DOKill();
        mainCamera.transform.DOMove(targetCamPos, 0.4f).SetEase(Ease.OutCubic);
    }

    private void HandleCameraDrag()
    {
        if (Mouse.current == null || mainCamera == null) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        // 注意：目前拖拽使用的是中键 (middleButton)。如果你希望改成鼠标左键拖拽，可以顺便在这里调整。
        if (Mouse.current.middleButton.wasPressedThisFrame)
        {
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
                mainCamera.transform.position += difference;

                if (enableBounds) ClampCameraPosition();
            }
        }
    }

    /// <summary>
    /// 处理键盘 WASD 平移视角：与中键拖拽保持完全一致的逻辑（同一套地面投影换算 + 相同的边界限制）。
    /// 区别只在于输入来源：把按键产生的屏幕位移当作虚拟拖拽位移，再投影到地面平面上。
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

        Vector2 screenDelta = new Vector2(horizontal, vertical).normalized
            * keyboardPanSpeed * Time.unscaledDeltaTime;

        // 以屏幕中心为参考点，把屏幕位移换算成地面平面上的世界位移，换算方式与中键拖拽一致。
        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        if (TryGetGroundPoint(screenCenter, out Vector3 before) &&
            TryGetGroundPoint(screenCenter + screenDelta, out Vector3 after))
        {
            mainCamera.transform.position += after - before;

            if (enableBounds) ClampCameraPosition();
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
                    mainCamera.transform.position += (worldPosBefore - worldPosAfter);
                }
            }

            if (enableBounds) ClampCameraPosition();
        }
    }

    private void ClampCameraPosition()
    {
        Vector3 pos = mainCamera.transform.position;
        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.z = Mathf.Clamp(pos.z, minY, maxY);
        mainCamera.transform.position = pos;
    }

    public void ToggleMapMode()
    {
        SetMapMode(!isMapMode);
    }

    public void SetMapMode(bool enableMap)
    {
        if (isMapMode == enableMap) return;
        isMapMode = enableMap;

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

                    Vector3 targetCamPos = focusPoint + cameraOffset;
                    mainCamera.transform.DOMove(targetCamPos, 0.4f).SetEase(Ease.OutCubic);
                }
            }
        }
    }
}
