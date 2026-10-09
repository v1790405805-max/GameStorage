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

    [Header("卡牌类型视觉配置")]
    [Tooltip("随卡牌类型改变颜色的底图/边框/标头 Image 组件（留空则不生效）")]
    public Image cardTypeImage;
    [Tooltip("攻击卡颜色 (默认暗红)")]
    public Color attackColor = new Color(0.9f, 0.35f, 0.35f, 1f);
    [Tooltip("技能卡颜色 (默认冷蓝)")]
    public Color skillColor = new Color(0.35f, 0.65f, 0.95f, 1f);
    [Tooltip("位移卡颜色 (默认青绿)")]
    public Color movementColor = new Color(0.35f, 0.9f, 0.65f, 1f);
    [Tooltip("特殊卡颜色 (默认星辰紫)")]
    public Color specialColor = new Color(0.7f, 0.45f, 0.95f, 1f);
    [Tooltip("能力卡颜色 (默认金黄)")]
    public Color abilityColor = new Color(0.95f, 0.75f, 0.3f, 1f);
    [Tooltip("默认/未识别类型颜色")]
    public Color defaultTypeColor = Color.white;

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
    private Vector3 originalLocalRot; // 弧形基准旋转角
    private Vector3 originalScale;
    private RectTransform rectTransform;
    private bool isDragging = false;
    private bool hasShifted = false;
    private Camera uiCamera;

    public CardData CurrentCardData => currentCardData;
    public bool IsDragging => isDragging;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    private void Start()
    {
        originalLocalPos = transform.localPosition;
        originalLocalRot = transform.localEulerAngles;
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

        if (data != null)
        {
            UpdateCardTypeColor(data.type);
        }
    }

    /// <summary>
    /// 由 HandCardArcLayout 布局组件调用：更新卡牌在弧线上的归位基准位置与旋转
    /// </summary>
    public void UpdateHomeTransform(Vector3 pos, Vector3 rot)
    {
        originalLocalPos = pos;
        originalLocalRot = rot;
    }

    private void UpdateCardTypeColor(CardType type)
    {
        if (cardTypeImage == null) return;

        switch (type)
        {
            case CardType.Attack:
                cardTypeImage.color = attackColor;
                break;
            case CardType.Skill:
                cardTypeImage.color = skillColor;
                break;
            case CardType.Movement:
                cardTypeImage.color = movementColor;
                break;
            case CardType.Special:
                cardTypeImage.color = specialColor;
                break;
            case CardType.Ability:
                cardTypeImage.color = abilityColor;
                break;
            default:
                cardTypeImage.color = defaultTypeColor;
                break;
        }
    }

    // --- 1. 鼠标悬停阅牌效果 ---
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isDisplayOnly || isDragging || (currentlySelectedCard != null && currentlySelectedCard != this))
            return;

        UpdateHighlightState(true);
        transform.DOKill();

        // 悬停时：放大、回正旋转、向上微浮 35 像素，增强阅读舒适度
        transform.DOScale(originalScale * 1.15f, tweenDuration).SetEase(Ease.OutBack);
        transform.DOLocalRotate(Vector3.zero, tweenDuration).SetEase(Ease.OutBack);
        transform.DOLocalMove(originalLocalPos + new Vector3(0f, 35f, 0f), tweenDuration).SetEase(Ease.OutBack);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isDisplayOnly || isDragging) return;

        UpdateHighlightState(false);
        transform.DOKill();

        // 鼠标移出：平滑弹回弧线初始位置与倾角
        transform.DOScale(originalScale, tweenDuration).SetEase(Ease.OutCubic);
        transform.DOLocalMove(originalLocalPos, tweenDuration).SetEase(Ease.OutCubic);
        transform.DOLocalRotate(originalLocalRot, tweenDuration).SetEase(Ease.OutCubic);
    }

    // --- 2. 拖拽交互 ---
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isDisplayOnly || isSelectingMode) return;

        currentlySelectedCard = this;
        isDragging = true;
        hasShifted = false;
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

    // --- 3. 动画与逻辑状态控制 ---
    private void TriggerShiftUp()
    {
        hasShifted = true;
        Vector3 targetPos = originalLocalPos + new Vector3(0, offsetY, 0);
        UpdateHighlightState(true);
        transform.DOKill();

        // 预选出牌时，卡牌回正角度并放大抬升
        transform.DOLocalMove(targetPos, tweenDuration).SetEase(Ease.OutBack);
        transform.DOLocalRotate(Vector3.zero, tweenDuration).SetEase(Ease.OutBack);
        transform.DOScale(originalScale * shiftedScaleMultiplier, tweenDuration).SetEase(Ease.OutBack);

        if (CardDragController.Instance != null)
        {
            CardDragController.Instance.OnCardDragging(currentCardData);
        }
    }

    private void CancelShift()
    {
        hasShifted = false;
        transform.DOKill();

        // 取消预选拉回：恢复弧度位置、旋转及悬停放大倍率
        transform.DOLocalMove(originalLocalPos, tweenDuration).SetEase(Ease.OutCubic);
        transform.DOLocalRotate(originalLocalRot, tweenDuration).SetEase(Ease.OutCubic);
        transform.DOScale(originalScale * 1.15f, tweenDuration).SetEase(Ease.OutCubic);

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

        // 彻底复位至弧线初始状态
        transform.DOLocalMove(originalLocalPos, tweenDuration).SetEase(Ease.OutCubic);
        transform.DOLocalRotate(originalLocalRot, tweenDuration).SetEase(Ease.OutCubic);
        transform.DOScale(originalScale, tweenDuration).SetEase(Ease.OutCubic);

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
            if (isSelectingMode)
            {
                highlightBorderImage.color = selectingHoverColor;
            }
            else
            {
                highlightBorderImage.color = CanUseCurrentCard() ? affordableColor : unaffordableColor;
            }
            highlightBorderImage.gameObject.SetActive(true);
        }
    }

    private bool CanUseCurrentCard()
    {
        if (currentCardData == null || CombatStatsManager.Instance == null)
        {
            return false;
        }

        if (CombatStatsManager.Instance.currentEnergy < currentCardData.GetEffectiveCost())
        {
            return false;
        }

        PlayerMoveController moveController = FindFirstObjectByType<PlayerMoveController>();
        Vector2Int targetGrid = moveController != null
            ? moveController.PlayerGridPos
            : new Vector2Int(-1, -1);
        return CardEffectCore.CanPlayAll(currentCardData, targetGrid);
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