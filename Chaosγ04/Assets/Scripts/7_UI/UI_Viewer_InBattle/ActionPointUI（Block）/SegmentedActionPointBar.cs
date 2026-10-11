using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 弧线分段式行动点总控制器：
/// 支持极坐标自适应分段生成、数据联动以及预消耗预览（变红反馈）
/// </summary>
public class SegmentedActionPointBar : MonoBehaviour
{
    public static SegmentedActionPointBar Instance { get; private set; }

    [Header("预制体与容器")]
    [Tooltip("单个能量格预制体（挂载 ArcSegmentGraphic 与 ActionPointSegmentItem）")]
    [SerializeField] private GameObject segmentPrefab;
    [Tooltip("生成能量格的父容器，留空则为自身")]
    [SerializeField] private Transform segmentsContainer;

    [Header("底座轨道背景 (可选)")]
    [Tooltip("整条弧形的暗色凹槽背景，留空则不显示")]
    [SerializeField] private ArcSegmentGraphic baseTrackGraphic;

    [Header("几何尺寸参数")]
    [Tooltip("圆弧半径")]
    [SerializeField] private float radius = 95f;

    [Tooltip("条状厚度/宽度")]
    [SerializeField] private float thickness = 14f;

    [Tooltip("圆弧起始角度（度数）")]
    [SerializeField] private float startAngle = 145f;

    [Tooltip("圆弧结束角度（度数）")]
    [SerializeField] private float endAngle = 5f;

    [Tooltip("格与格之间的微小缝隙角度（度数，建议 2 ~ 3.5）")]
    [SerializeField] private float gapAngle = 3f;

    private readonly List<ActionPointSegmentItem> segments = new List<ActionPointSegmentItem>();
    private int cachedMaxAP = -1;
    private int currentCostPreview = 0;

    private void Awake()
    {
        Instance = this;
        if (segmentsContainer == null) segmentsContainer = transform;
    }

    private void Start()
    {
        if (CombatStatsManager.Instance != null)
        {
            CombatStatsManager.Instance.OnStatsChanged += RefreshDisplay;
            RefreshDisplay();
        }
    }

    private void OnDestroy()
    {
        if (CombatStatsManager.Instance != null)
        {
            CombatStatsManager.Instance.OnStatsChanged -= RefreshDisplay;
        }
    }

    public void RefreshDisplay()
    {
        if (CombatStatsManager.Instance == null || segmentPrefab == null) return;

        int current = CombatStatsManager.Instance.currentActionPoint;
        int max = CombatStatsManager.Instance.maxActionPoint;

        if (max != cachedMaxAP || segments.Count != max)
        {
            RebuildSegments(max);
        }

        currentCostPreview = 0;

        // 基础亮灭刷新
        for (int i = 0; i < segments.Count; i++)
        {
            bool active = i < current;
            segments[i].SetState(active, animate: true);
        }
    }

    /// <summary>
    /// 预扣消耗预览：将即将扣除的行动点高亮为警戒色（Preview Color）
    /// </summary>
    /// <param name="cost">即将消耗的行动点数</param>
    public void PreviewCost(int cost)
    {
        if (CombatStatsManager.Instance == null || segments.Count == 0) return;

        currentCostPreview = Mathf.Max(0, cost);
        int current = CombatStatsManager.Instance.currentActionPoint;

        // 计算哪些格子是即将被扣减的（从最外侧的高位格开始扣）
        // 例如：当前 3 点，消耗 2 点 -> 扣除索引 1 和 2 的格子
        int previewStartIndex = Mathf.Max(0, current - currentCostPreview);

        for (int i = 0; i < current; i++)
        {
            bool isPreview = i >= previewStartIndex;
            segments[i].SetPreview(isPreview);
        }
    }

    /// <summary>
    /// 清除预扣预览，所有可用格恢复亮黄状态
    /// </summary>
    public void ClearPreview()
    {
        if (CombatStatsManager.Instance == null || segments.Count == 0) return;

        currentCostPreview = 0;
        int current = CombatStatsManager.Instance.currentActionPoint;

        for (int i = 0; i < current; i++)
        {
            segments[i].SetPreview(false);
        }
    }

    private void RebuildSegments(int maxCount)
    {
        cachedMaxAP = maxCount;

        foreach (var item in segments)
        {
            if (item != null) Destroy(item.gameObject);
        }
        segments.Clear();

        if (maxCount <= 0) return;

        if (baseTrackGraphic != null)
        {
            baseTrackGraphic.SetSegment(radius, thickness + 4f, startAngle, endAngle);
        }

        float totalSpan = endAngle - startAngle;
        float sign = Mathf.Sign(totalSpan);
        float absTotalAngle = Mathf.Abs(totalSpan);

        float totalGapsAngle = (maxCount - 1) * gapAngle;
        float availableAngle = Mathf.Max(0f, absTotalAngle - totalGapsAngle);
        float stepAngle = availableAngle / maxCount;

        for (int i = 0; i < maxCount; i++)
        {
            GameObject obj = Instantiate(segmentPrefab, segmentsContainer);
            obj.name = $"Segment_{i}";

            float segStart = startAngle + sign * (i * (stepAngle + gapAngle));
            float segEnd = segStart + sign * stepAngle;

            ActionPointSegmentItem item = obj.GetComponent<ActionPointSegmentItem>();
            if (item != null)
            {
                item.InitShape(radius, thickness, segStart, segEnd);
                segments.Add(item);
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (baseTrackGraphic != null)
        {
            baseTrackGraphic.SetSegment(radius, thickness + 4f, startAngle, endAngle);
        }
    }
#endif
}