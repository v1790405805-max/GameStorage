using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// 专业级手牌弧形/扇形排布控制器：
/// 维护独立的稳定逻辑队列，支持卡牌纯原生置顶且弧线坐标绝不乱跑。
/// </summary>
public class HandCardArcLayout : MonoBehaviour
{
    public static HandCardArcLayout Instance { get; private set; }

    [Header("--- 1. 空间与安全区约束 ---")]
    [Tooltip("手牌绝对允许占用的最大总宽度（含卡牌自身宽度，左右边缘绝不会超出此范围）")]
    [SerializeField] private float maxSafeVisualWidth = 720f;

    [Tooltip("单张卡牌的大致视觉宽度（像素，用于计算边缘防越界）")]
    [SerializeField] private float cardVisualWidth = 140f;

    [Tooltip("手牌较少时的自然基础间距（像素，保证少牌时紧密不散开）")]
    [SerializeField] private float naturalSpacing = 135f;

    [Tooltip("极端多牌时允许压缩到的最小极限间距（像素）")]
    [SerializeField] private float minAllowedSpacing = 45f;

    [Header("--- 2. 弧形几何参数 ---")]
    [Tooltip("抛物线拱起高度系数（建议 8 ~ 14）")]
    [SerializeField] private float curveHeightFactor = 10f;

    [Tooltip("卡牌旋转倾角步长（度数，建议 2.5 ~ 4.0）")]
    [SerializeField] private float baseAngleStep = 3.2f;

    [Header("--- 3. 补间动画配置 ---")]
    [Tooltip("卡牌归位动画时长（秒）")]
    [SerializeField] private float arrangeDuration = 0.22f;

    [Tooltip("排布缓动曲线")]
    [SerializeField] private Ease arrangeEase = Ease.OutCubic;

    // 内部稳定的手牌逻辑队列，绝不随 Hierarchy 顺序变更而乱序
    private readonly List<CardUI> trackedCards = new List<CardUI>();
    private int lastChildCount = -1;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        RefreshLayout();
    }

    private void OnTransformChildrenChanged()
    {
        // 关键保护：仅当卡牌数量真正发生增减（抽牌/打出）时才刷新布局，
        // 悬停置顶引起的兄弟节点顺序调换直接忽略，杜绝死循环与位置乱飞！
        if (transform.childCount != lastChildCount)
        {
            lastChildCount = transform.childCount;
            RefreshLayout();
        }
    }

    private void Update()
    {
        if (transform.childCount != lastChildCount)
        {
            lastChildCount = transform.childCount;
            RefreshLayout();
        }
    }

    /// <summary>
    /// 同步稳定的手牌列表：移除失效卡牌，按添加顺序追加新抽到的卡牌
    /// </summary>
    private void SyncTrackedCards()
    {
        for (int i = trackedCards.Count - 1; i >= 0; i--)
        {
            if (trackedCards[i] == null || !trackedCards[i].gameObject.activeSelf || trackedCards[i].transform.parent != transform)
            {
                trackedCards.RemoveAt(i);
            }
        }

        foreach (Transform child in transform)
        {
            CardUI card = child.GetComponent<CardUI>();
            if (card != null && child.gameObject.activeSelf && !trackedCards.Contains(card))
            {
                trackedCards.Add(card);
            }
        }
    }

    /// <summary>
    /// 卡牌置顶：直接移到同级末尾，在 UGUI 中永远渲染在同层最表面
    /// </summary>
    public void BringCardToFront(CardUI card)
    {
        if (card == null || card.transform.parent != transform) return;
        card.transform.SetAsLastSibling();
    }

    /// <summary>
    /// 卡牌还原：根据它在逻辑队列中的正确槽位还原物理层级
    /// </summary>
    public void RestoreCardOrder(CardUI card)
    {
        if (card == null || card.transform.parent != transform) return;
        int index = trackedCards.IndexOf(card);
        if (index >= 0)
        {
            card.transform.SetSiblingIndex(index);
        }
    }

    /// <summary>
    /// 全局刷新手牌弧线排布
    /// </summary>
    public void RefreshLayout()
    {
        SyncTrackedCards();

        int count = trackedCards.Count;
        if (count == 0) return;

        // 1. 单张手牌直接正中居中
        if (count == 1)
        {
            CardUI singleCard = trackedCards[0];
            if (singleCard != null && !singleCard.IsDragging)
            {
                singleCard.UpdateHomeTransform(Vector3.zero, Vector3.zero);
                singleCard.transform.DOKill();
                singleCard.transform.DOLocalMove(Vector3.zero, arrangeDuration).SetEase(arrangeEase);
                singleCard.transform.DOLocalRotate(Vector3.zero, arrangeDuration).SetEase(arrangeEase);
            }
            return;
        }

        // 2. 核心算法：安全区限制与自适应压缩比计算
        float maxCenterSpan = Mathf.Max(0f, maxSafeVisualWidth - cardVisualWidth);
        float autoSpacing = maxCenterSpan / (count - 1);
        float effectiveSpacing = Mathf.Clamp(Mathf.Min(naturalSpacing, autoSpacing), minAllowedSpacing, naturalSpacing);

        float compressFactor = Mathf.Clamp01(effectiveSpacing / naturalSpacing);
        float currentAngleStep = baseAngleStep * Mathf.Lerp(0.65f, 1f, compressFactor);
        float currentHeightFactor = curveHeightFactor * Mathf.Lerp(0.7f, 1f, compressFactor);

        float midIndex = (count - 1) / 2f;

        // 3. 严格遍历 trackedCards（而不是 transform 遍历），保证卡牌坐标绝对锁定在原槽位
        for (int i = 0; i < count; i++)
        {
            CardUI card = trackedCards[i];
            if (card == null || card.IsDragging) continue;

            float offset = i - midIndex;

            float targetX = offset * effectiveSpacing;
            float targetY = -Mathf.Pow(offset, 2f) * currentHeightFactor;
            float targetAngle = -offset * currentAngleStep;

            Vector3 targetPos = new Vector3(targetX, targetY, 0f);
            Vector3 targetRot = new Vector3(0f, 0f, targetAngle);

            card.transform.SetSiblingIndex(i);
            card.UpdateHomeTransform(targetPos, targetRot);

            card.transform.DOKill();
            card.transform.DOLocalMove(targetPos, arrangeDuration).SetEase(arrangeEase);
            card.transform.DOLocalRotate(targetRot, arrangeDuration).SetEase(arrangeEase);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        float halfW = maxSafeVisualWidth * 0.5f;

        Vector3 left = transform.TransformPoint(new Vector3(-halfW, 0f, 0f));
        Vector3 right = transform.TransformPoint(new Vector3(halfW, 0f, 0f));

        Gizmos.DrawLine(left, right);
        Gizmos.DrawLine(transform.TransformPoint(new Vector3(-halfW, -60f, 0f)), transform.TransformPoint(new Vector3(-halfW, 60f, 0f)));
        Gizmos.DrawLine(transform.TransformPoint(new Vector3(halfW, -60f, 0f)), transform.TransformPoint(new Vector3(halfW, 60f, 0f)));
    }
#endif
}