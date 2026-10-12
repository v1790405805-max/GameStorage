using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

/// <summary>
/// 牌堆图标悬停动效控制器：
/// 支持悬停展开/复原，以及点开弹窗期间的“常驻展开状态锁定”。
/// </summary>
public class CardPileHoverEffect : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    [Header("结构节点绑定")]
    [Tooltip("随悬停上浮的整体父容器 (Content_Float)")]
    [SerializeField] private RectTransform floatingContainer;

    [Tooltip("左侧/底层卡牌 (Card_Back_Left)")]
    [SerializeField] private RectTransform cardBackLeft;

    [Tooltip("右侧/中层卡牌 (Card_Back_Right)")]
    [SerializeField] private RectTransform cardBackRight;

    [Tooltip("右上角数字徽章 (Badge_Count)")]
    [SerializeField] private RectTransform countBadge;

    [Tooltip("留在原地的深色卡槽底座 (Base_Slot)")]
    [SerializeField] private RectTransform baseSlot;

    [Header("动效参数配置")]
    [Tooltip("卡牌整体上浮的 Y 轴高度（像素）")]
    [SerializeField] private float floatHeight = 12f;

    [Tooltip("背后卡牌向两侧展开的最大旋转角（度数）")]
    [SerializeField] private float fanRotateAngle = 10f;

    [Tooltip("背后卡牌向两侧展开的水平位移量（像素）")]
    [SerializeField] private float fanSpreadX = 6f;

    [Tooltip("过渡动画时长（秒）")]
    [SerializeField] private float tweenDuration = 0.22f;

    private Vector2 floatOriginPos;
    private Vector2 leftCardOriginPos;
    private Vector2 rightCardOriginPos;
    private Vector3 leftCardOriginRot;
    private Vector3 rightCardOriginRot;
    private Vector3 badgeOriginScale;
    private Vector3 baseSlotOriginScale;

    private bool isHovered = false;
    private bool isHoldOpen = false; // 是否因查看弹窗被锁定在展开状态

    private void Awake()
    {
        if (floatingContainer != null)
            floatOriginPos = floatingContainer.anchoredPosition;

        if (cardBackLeft != null)
        {
            leftCardOriginPos = cardBackLeft.anchoredPosition;
            leftCardOriginRot = cardBackLeft.localEulerAngles;
        }

        if (cardBackRight != null)
        {
            rightCardOriginPos = cardBackRight.anchoredPosition;
            rightCardOriginRot = cardBackRight.localEulerAngles;
        }

        if (countBadge != null)
            badgeOriginScale = countBadge.localScale;

        if (baseSlot != null)
            baseSlotOriginScale = baseSlot.localScale;
    }

    /// <summary>
    /// 外部控制：锁定或解锁展开状态（查看弹窗期间锁定）
    /// </summary>
    public void SetHoldOpen(bool hold)
    {
        isHoldOpen = hold;

        if (isHoldOpen)
        {
            DoOpenAnimation();
        }
        else
        {
            // 解锁时，仅在鼠标已经离开按钮区域的情况下才执行收拢复原
            if (!isHovered)
            {
                DoCloseAnimation();
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        DoOpenAnimation();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;

        // 如果弹窗还在打开状态，移开鼠标绝不收拢
        if (isHoldOpen) return;

        DoCloseAnimation();
    }

    private void DoOpenAnimation()
    {
        KillAllTweens();

        // 1. 主体向上浮起
        if (floatingContainer != null)
        {
            floatingContainer.DOAnchorPosY(floatOriginPos.y + floatHeight, tweenDuration)
                .SetEase(Ease.OutBack, 1.2f);
        }

        // 2. 两侧扇形展开
        if (cardBackLeft != null)
        {
            cardBackLeft.DOAnchorPosX(leftCardOriginPos.x - fanSpreadX, tweenDuration).SetEase(Ease.OutCubic);
            cardBackLeft.DOLocalRotate(new Vector3(0, 0, fanRotateAngle), tweenDuration).SetEase(Ease.OutCubic);
        }

        if (cardBackRight != null)
        {
            cardBackRight.DOAnchorPosX(rightCardOriginPos.x + fanSpreadX, tweenDuration).SetEase(Ease.OutCubic);
            cardBackRight.DOLocalRotate(new Vector3(0, 0, -fanRotateAngle), tweenDuration).SetEase(Ease.OutCubic);
        }

        // 3. 底座立体收缩
        if (baseSlot != null)
        {
            baseSlot.DOScale(baseSlotOriginScale * 0.92f, tweenDuration).SetEase(Ease.OutCubic);
        }

        // 4. 数字徽章微弹
        if (countBadge != null)
        {
            countBadge.DOScale(badgeOriginScale * 1.15f, tweenDuration).SetEase(Ease.OutBack, 1.8f);
        }
    }

    private void DoCloseAnimation()
    {
        KillAllTweens();

        if (floatingContainer != null)
            floatingContainer.DOAnchorPosY(floatOriginPos.y, tweenDuration).SetEase(Ease.OutCubic);

        if (cardBackLeft != null)
        {
            cardBackLeft.DOAnchorPosX(leftCardOriginPos.x, tweenDuration).SetEase(Ease.OutCubic);
            cardBackLeft.DOLocalRotate(leftCardOriginRot, tweenDuration).SetEase(Ease.OutCubic);
        }

        if (cardBackRight != null)
        {
            cardBackRight.DOAnchorPosX(rightCardOriginPos.x, tweenDuration).SetEase(Ease.OutCubic);
            cardBackRight.DOLocalRotate(rightCardOriginRot, tweenDuration).SetEase(Ease.OutCubic);
        }

        if (baseSlot != null)
            baseSlot.DOScale(baseSlotOriginScale, tweenDuration).SetEase(Ease.OutCubic);

        if (countBadge != null)
            countBadge.DOScale(badgeOriginScale, tweenDuration).SetEase(Ease.OutCubic);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (floatingContainer != null)
        {
            floatingContainer.DOKill();
            floatingContainer.DOScale(0.92f, 0.1f).SetEase(Ease.OutQuad);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (floatingContainer != null)
        {
            floatingContainer.DOKill();
            floatingContainer.DOScale(1f, 0.15f).SetEase(Ease.OutBack);
        }
    }

    private void KillAllTweens()
    {
        if (floatingContainer != null) floatingContainer.DOKill();
        if (cardBackLeft != null) cardBackLeft.DOKill();
        if (cardBackRight != null) cardBackRight.DOKill();
        if (baseSlot != null) baseSlot.DOKill();
        if (countBadge != null) countBadge.DOKill();
    }

    private void OnDisable()
    {
        KillAllTweens();
    }
}