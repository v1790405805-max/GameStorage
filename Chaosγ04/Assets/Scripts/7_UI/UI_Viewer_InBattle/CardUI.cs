using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using DG.Tweening;

public class CardUI : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    private static CardUI currentlySelectedCard = null;

    [Header("状态配置")]
    [Tooltip("是否仅用于展示（勾选后无法打出，也不触发费用高亮和位移）")]
    public bool isDisplayOnly = false;
    [Tooltip("是否处于选牌模式：悬停使用 selectingHoverColor，并禁止拖拽出牌")]
    public bool isSelectingMode = false;

    [Header("区域引用配置")]
    [Tooltip("手牌区域的 RectTransform，划出该区域即视为触发预选出牌")]
    public RectTransform handAreaRect;

    [Header("UI 文本组件")]
    public TextMeshProUGUI costText;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descText;

    [Header("视觉交互组件")]
    [Tooltip("拖入卡牌的边框高亮物体上的 Image 组件")]
    public Image highlightBorderImage;
    [Tooltip("费用足够时的边框颜色 (默认绿色)")]
    public Color affordableColor = new Color(0.2f, 1f, 0.2f, 1f);
    [Tooltip("费用不足时的边框颜色 (默认红色)")]
    public Color unaffordableColor = new Color(1f, 0.2f, 0.2f, 1f);
    [Tooltip("回合结束选牌模式下的悬停边框颜色（独立于费用绿/红，默认金黄色）")]
    public Color selectingHoverColor = new Color(1f, 0.85f, 0.2f, 1f);

    [Header("平移与缩放配置")]
    [Tooltip("按住移出 HandArea 后，向上平移的 Y 轴距离")]
    public float offsetY = 60f;
    [Tooltip("移出 HandArea 预选状态时的缩放倍率")]
    public float shiftedScaleMultiplier = 1.2f;
    [Tooltip("动画时长")]
    public float tweenDuration = 0.2f;

    private CardData currentCardData;
    private Vector3 originalLocalPos;
    private Vector3 originalScale;
    private RectTransform rectTransform;
    private bool isDragging = false;
    private bool hasShifted = false;
    private Camera uiCamera;

    public CardData CurrentCardData => currentCardData;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    private void Start()
    {
        originalLocalPos = transform.localPosition;
        originalScale = transform.localScale;

        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = parentCanvas.worldCamera;
        }

        if (handAreaRect == null)
        {
            Transform parentHand = transform.parent;
            if (parentHand != null)
            {
                handAreaRect = parentHand.GetComponent<RectTransform>();
            }
        }

        if (highlightBorderImage != null)
        {
            highlightBorderImage.gameObject.SetActive(false);
        }

        if (CombatStatsManager.Instance != null)
        {
            CombatStatsManager.Instance.OnActionPointCountChanged += HandleActionPointCountChanged;
        }

        RefreshCostDisplay();
    }

    public void Setup(CardData data)
    {
        currentCardData = data;
        RefreshCostDisplay();
        if (nameText != null) nameText.text = data.cardName;
        if (descText != null) descText.text = data.description;
    }

    // --- 1. 纯鼠标悬停效果 ---
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isDisplayOnly || isDragging || (currentlySelectedCard != null && currentlySelectedCard != this))
            return;

        UpdateHighlightState(true);
        transform.DOKill();
        transform.DOScale(originalScale * 1.1f, tweenDuration).SetEase(Ease.OutBack);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isDisplayOnly || isDragging) return;

        UpdateHighlightState(false);
        transform.DOKill();
        transform.DOScale(originalScale, tweenDuration).SetEase(Ease.OutCubic);
    }

    // --- 2. 拖拽交互 ---
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isDisplayOnly || isSelectingMode) return; // 选牌模式：禁止拖拽出牌

        currentlySelectedCard = this;
        isDragging = true;
        hasShifted = false;
        originalLocalPos = transform.localPosition;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isDisplayOnly || !isDragging) return;

        RectTransform targetArea = handAreaRect != null ? handAreaRect : rectTransform;
        bool isInsideHandArea = RectTransformUtility.RectangleContainsScreenPoint(
            targetArea,
            eventData.position,
            uiCamera
        );

        if (!isInsideHandArea && !hasShifted)
        {
            TriggerShiftUp();
        }
        else if (isInsideHandArea && hasShifted)
        {
            CancelShift();
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (isDisplayOnly || !isDragging) return;

        isDragging = false;
        RectTransform targetArea = handAreaRect != null ? handAreaRect : rectTransform;
        bool isOutsideHandArea = !RectTransformUtility.RectangleContainsScreenPoint(
            targetArea,
            eventData.position,
            uiCamera
        );

        // 移出手牌区并触发了预选，通知控制器处理后续逻辑
        if (isOutsideHandArea && hasShifted)
        {
            if (CardDragController.Instance != null)
            {
                CardDragController.Instance.OnCardReleased(this);
            }
            else
            {
                ResetToOriginalState();
            }
        }
        else
        {
            ResetToOriginalState();
        }

        if (currentlySelectedCard == this)
        {
            currentlySelectedCard = null;
        }
    }

    // --- 3. 逻辑控制与动画 ---
    private void TriggerShiftUp()
    {
        hasShifted = true;
        Vector3 targetPos = originalLocalPos + new Vector3(0, offsetY, 0);
        UpdateHighlightState(true);
        transform.DOKill();
        transform.DOLocalMove(targetPos, tweenDuration).SetEase(Ease.OutBack);
        transform.DOScale(originalScale * shiftedScaleMultiplier, tweenDuration).SetEase(Ease.OutBack);

        // 移出手牌区，通知控制器开启地图范围高亮
        if (CardDragController.Instance != null)
        {
            CardDragController.Instance.OnCardDragging(currentCardData);
        }
    }

    private void CancelShift()
    {
        hasShifted = false;
        transform.DOKill();
        transform.DOLocalMove(originalLocalPos, tweenDuration).SetEase(Ease.OutCubic);
        transform.DOScale(originalScale * 1.1f, tweenDuration).SetEase(Ease.OutCubic);

        // 移回手牌区，清空地图高亮
        if (CardDragController.Instance != null)
        {
            CardDragController.Instance.OnCardDragEnd();
        }
    }

    public void ResetToOriginalState()
    {
        hasShifted = false;
        UpdateHighlightState(false);
        transform.DOKill();
        transform.DOLocalMove(originalLocalPos, tweenDuration).SetEase(Ease.OutCubic);
        transform.DOScale(originalScale, tweenDuration).SetEase(Ease.OutCubic);

        // 复位时清空地图高亮
        if (CardDragController.Instance != null)
        {
            CardDragController.Instance.OnCardDragEnd();
        }
    }

    private void UpdateHighlightState(bool active)
    {
        if (highlightBorderImage == null) return;

        if (!active)
        {
            highlightBorderImage.gameObject.SetActive(false);
            return;
        }

        if (currentCardData != null && CombatStatsManager.Instance != null)
        {
            // 选牌模式（回合结束时选牌）：使用独立悬停色，不按费用判绿/红
            if (isSelectingMode)
            {
                highlightBorderImage.color = selectingHoverColor;
            }
            else
            {
                bool canAfford =
                    CombatStatsManager.Instance.currentEnergy >= currentCardData.GetEffectiveCost();
                highlightBorderImage.color = canAfford ? affordableColor : unaffordableColor;
            }
            highlightBorderImage.gameObject.SetActive(true);
        }
    }

    private void HandleActionPointCountChanged(int usedActionPointCount)
    {
        RefreshCostDisplay();

        if (highlightBorderImage != null && highlightBorderImage.gameObject.activeSelf)
        {
            UpdateHighlightState(true);
        }
    }

    private void RefreshCostDisplay()
    {
        if (costText == null || currentCardData == null) return;
        costText.text = currentCardData.GetCostDisplayText();
    }

    private void OnDestroy()
    {
        if (currentlySelectedCard == this)
        {
            currentlySelectedCard = null;
        }

        if (CombatStatsManager.Instance != null)
        {
            CombatStatsManager.Instance.OnActionPointCountChanged -= HandleActionPointCountChanged;
        }

        transform.DOKill();
    }
}
