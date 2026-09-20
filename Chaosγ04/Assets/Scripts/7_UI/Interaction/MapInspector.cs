using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections;

public class MapInspector : MonoBehaviour
{
    [Header("--- 1. 交互与目标设置 ---")]
    [Tooltip("右键聚焦功能使用的层级，请勾选地图格子所在的 Layer")]
    public LayerMask mapLayerMask;
    public Key mapShortcutKey = Key.Space;

    [Header("--- 2. UI 联动与动画参数 ---")]
    public RectTransform handArea;
    public Transform handCollapsedTarget;
    public CanvasGroup nonEssentialHUD;
    public float collapsedCardSpacing = -60f;
    [Tooltip("地图模式下手牌缩小的比例 (默认 0.5，即缩小一半)")]
    public float collapsedHandScale = 0.5f; // 【新增】：让你可以在面板自由调节缩小比例

    [Header("--- 3. 相机缩放参数 (正交相机) ---")]
    public float battleCameraSize = 5f;
    public float mapInspectCameraSize = 10f;

    [Header("--- 4. 相机拖拽与边界参数 ---")]
    [Tooltip("如果点击边缘格子没反应，请把下面的 Min 和 Max 调大！或者直接取消勾选此项测试")]
    public bool enableBounds = true;
    public float minX = -30f, maxX = 30f;
    public float minY = -30f, maxY = 30f;

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
    private Camera mainCamera;
    private Vector3 dragOrigin;
    private Vector3 cameraOffset;

    // UI 组件缓存
    private HorizontalLayoutGroup handLayoutGroup;
    private float originalSpacing;
    private CanvasGroup handCanvasGroup;
    private bool uiStateCached = false;

    IEnumerator Start()
    {
        mainCamera = Camera.main;

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
        HandleMapScrollZoom();
        HandleRightClickToFocus();
    }

    /// <summary>
    /// 处理鼠标右键点击，自动聚焦到点击的地块
    /// </summary>
    private void HandleRightClickToFocus()
    {
        if (Mouse.current == null || mainCamera == null) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, mapLayerMask))
            {
                Vector3 focusPoint = hit.point;

                // 只有当你点到的物体身上（或父级）真的有 CellManager（说明是个实打实的小格子）
                // 才去吸附这个小格子的中心点
                CellManager cell = hit.collider.GetComponentInParent<CellManager>();
                if (cell != null)
                {
                    focusPoint = hit.collider.bounds.center;
                }

                Vector3 targetCamPos = focusPoint + cameraOffset;

                if (enableBounds)
                {
                    targetCamPos.x = Mathf.Clamp(targetCamPos.x, minX, maxX);
                    targetCamPos.z = Mathf.Clamp(targetCamPos.z, minY, maxY);
                }

                mainCamera.transform.DOKill();
                mainCamera.transform.DOMove(targetCamPos, 0.4f).SetEase(Ease.OutCubic);
            }
        }
    }

    private void HandleCameraDrag()
    {
        if (Mouse.current == null || mainCamera == null) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        if (Mouse.current.middleButton.wasPressedThisFrame)
        {
            Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (groundPlane.Raycast(ray, out float enter))
            {
                dragOrigin = ray.GetPoint(enter);
            }
        }

        if (Mouse.current.middleButton.isPressed)
        {
            Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (groundPlane.Raycast(ray, out float enter))
            {
                Vector3 currentPos = ray.GetPoint(enter);
                Vector3 difference = dragOrigin - currentPos;
                mainCamera.transform.position += difference;

                if (enableBounds) ClampCameraPosition();
            }
        }
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

                // ==========================================
                // 【修改】：使用面板中可调节的 collapsedHandScale 变量
                // ==========================================
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