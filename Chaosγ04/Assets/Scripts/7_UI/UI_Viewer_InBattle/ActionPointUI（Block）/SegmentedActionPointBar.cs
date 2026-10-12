using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 弧线分段式行动点总控制器：
/// 1. 默认保底 5 格基准细分；
/// 2. 超过 5 点动态增加细分（按当前点数等分），消耗溢出点数时动态减少细分；
/// 3. 小于等于 5 点时固定锁定 5 格，消耗时走正常灭灯暗槽逻辑；
/// 4. 支持移动与出牌时的预扣警戒红（Preview Color）预览。
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

    [Header("分段基准规则")]
    [Tooltip("默认基准分段数（默认固定为 5 格）")]
    [SerializeField] private int defaultSegmentCount = 5;

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
    private int cachedSegmentCount = -1;
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

        // 【核心规则计算】：
        // 当行动点大于 5 点时，有多少点就动态切分多少格；
        // 当行动点 <= 5 点时，固定维持 5 格细分，消耗时只灭灯不缩减格数。
        int targetSegmentCount = Mathf.Max(defaultSegmentCount, current);

        bool needsRebuild = (targetSegmentCount != cachedSegmentCount) || (segments.Count != targetSegmentCount);

        if (needsRebuild)
        {
            // 细分数量发生改变（从 5 增至 6/7，或从 7 缩减回 6/5），重建几何网格
            RebuildSegments(targetSegmentCount, current);
        }
        else
        {
            // 格子总数不变（例如在 <= 5 点内部消耗或恢复），执行平滑亮灭与震颤动效
            for (int i = 0; i < segments.Count; i++)
            {
                if (segments[i] != null)
                {
                    bool active = i < current;
                    segments[i].SetState(active, animate: true);
                }
            }
        }

        currentCostPreview = 0;
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

        // 安全限制，防止超出实际格子总数
        int effectiveCurrent = Mathf.Min(current, segments.Count);
        int previewStartIndex = Mathf.Max(0, effectiveCurrent - currentCostPreview);

        for (int i = 0; i < effectiveCurrent; i++)
        {
            if (i < segments.Count && segments[i] != null)
            {
                bool isPreview = i >= previewStartIndex;
                segments[i].SetPreview(isPreview);
            }
        }
    }

    /// <summary>
    /// 清除预扣预览，所有可用格恢复亮黄状态
    /// </summary>
    public void ClearPreview()
    {
        if (segments == null || segments.Count == 0) return;

        currentCostPreview = 0;

        for (int i = 0; i < segments.Count; i++)
        {
            if (segments[i] != null)
            {
                segments[i].SetPreview(false);
            }
        }
    }

    private void RebuildSegments(int targetCount, int current)
    {
        cachedSegmentCount = targetCount;

        // 清空旧格子
        foreach (var item in segments)
        {
            if (item != null) Destroy(item.gameObject);
        }
        segments.Clear();

        if (targetCount <= 0) return;

        // 同步底座暗槽完整圆弧
        if (baseTrackGraphic != null)
        {
            baseTrackGraphic.SetSegment(radius, thickness + 4f, startAngle, endAngle);
        }

        float totalSpan = endAngle - startAngle;
        float sign = Mathf.Sign(totalSpan);
        float absTotalAngle = Mathf.Abs(totalSpan);

        // 扣除间隙后均匀分配给每个格子
        float totalGapsAngle = (targetCount - 1) * gapAngle;
        float availableAngle = Mathf.Max(0f, absTotalAngle - totalGapsAngle);
        float stepAngle = availableAngle / targetCount;

        for (int i = 0; i < targetCount; i++)
        {
            GameObject obj = Instantiate(segmentPrefab, segmentsContainer);
            obj.name = $"Segment_{i}";

            float segStart = startAngle + sign * (i * (stepAngle + gapAngle));
            float segEnd = segStart + sign * stepAngle;

            ActionPointSegmentItem item = obj.GetComponent<ActionPointSegmentItem>();
            if (item != null)
            {
                item.InitShape(radius, thickness, segStart, segEnd);

                // 重建时不播放震颤补间，直接呈现对应状态，避免细分切换时画面突兀闪动
                bool active = i < current;
                item.SetState(active, animate: false);
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