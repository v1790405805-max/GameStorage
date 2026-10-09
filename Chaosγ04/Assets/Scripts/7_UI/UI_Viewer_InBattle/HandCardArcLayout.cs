using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// 手牌弧形/扇形排布控制器：
/// 实现中间高两边低、两侧自然向外翘起、手牌超量时动态压叠的自适应布局。
/// </summary>
public class HandCardArcLayout : MonoBehaviour
{
    public static HandCardArcLayout Instance { get; private set; }

    [Header("弧形几何排布参数")]
    [Tooltip("手牌数量较少时的基础水平间距（像素）")]
    [SerializeField] private float cardSpacing = 150f;

    [Tooltip("手牌容器允许的最大总宽度（超过此宽度将自动压缩卡牌间距）")]
    [SerializeField] private float maxTotalWidth = 850f;

    [Tooltip("抛物线拱起高度系数（值越大，中间最高与两侧最低的落差越明显，建议 8 ~ 15）")]
    [SerializeField] private float curveHeightFactor = 12f;

    [Tooltip("卡牌旋转倾角步长（度数，越往两边倾斜越明显，建议 2.5 ~ 4.5）")]
    [SerializeField] private float angleStep = 3.5f;

    [Header("补间动画配置")]
    [Tooltip("卡牌滑动归位的动画耗时（秒）")]
    [SerializeField] private float arrangeDuration = 0.25f;

    [Tooltip("排布缓动曲线（推荐 OutCubic，起步迅速且停稳自然）")]
    [SerializeField] private Ease arrangeEase = Ease.OutCubic;

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
        // 当抽牌或打出牌使子物体数量发生变化时，自动重新计算排布
        RefreshLayout();
    }

    private void Update()
    {
        // 兜底检测子物体数量变化
        if (transform.childCount != lastChildCount)
        {
            lastChildCount = transform.childCount;
            RefreshLayout();
        }
    }

    /// <summary>
    /// 全局刷新手牌弧线排布
    /// </summary>
    public void RefreshLayout()
    {
        List<CardUI> cards = new List<CardUI>();
        foreach (Transform child in transform)
        {
            CardUI card = child.GetComponent<CardUI>();
            if (card != null && child.gameObject.activeSelf)
            {
                cards.Add(card);
            }
        }

        int count = cards.Count;
        if (count == 0) return;

        // 1. 动态自适应水平间距（卡牌多时自动压叠收窄）
        float effectiveSpacing = cardSpacing;
        if (count > 1 && (count - 1) * cardSpacing > maxTotalWidth)
        {
            effectiveSpacing = maxTotalWidth / (count - 1);
        }

        float midIndex = (count - 1) / 2f;

        // 2. 遍历计算每张卡牌的弧形目标位置与旋转
        for (int i = 0; i < count; i++)
        {
            CardUI card = cards[i];

            // 正在被鼠标拖拽的卡牌不强制覆盖其位移
            if (card.IsDragging) continue;

            float offset = i - midIndex; // 中心偏移量（如 5 张牌时为 -2, -1, 0, 1, 2）

            // X 轴：水平线性展开
            float targetX = offset * effectiveSpacing;

            // Y 轴：抛物线方程（offset = 0 时处于拱顶最高点，两边随平方急速下沉）
            float targetY = -Mathf.Pow(offset, 2f) * curveHeightFactor;

            // Z 轴：扇形倾角（左侧卡牌向左翘起，右侧卡牌向右翘起）
            float targetAngle = -offset * angleStep;

            Vector3 targetPos = new Vector3(targetX, targetY, 0f);
            Vector3 targetRot = new Vector3(0f, 0f, targetAngle);

            // 更新渲染层级（从左往右自然压叠）
            card.transform.SetSiblingIndex(i);

            // 将计算出的弧线基准位置与旋转同步给卡牌
            card.UpdateHomeTransform(targetPos, targetRot);

            // 驱动平滑过渡动画
            card.transform.DOKill();
            card.transform.DOLocalMove(targetPos, arrangeDuration).SetEase(arrangeEase);
            card.transform.DOLocalRotate(targetRot, arrangeDuration).SetEase(arrangeEase);
        }
    }
}