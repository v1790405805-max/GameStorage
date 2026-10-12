using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// UGUI 程序化矢量圆环/环形进度条组件：
/// 零贴图依赖，直接在 Canvas 上生成高精度的径向圆环网格，支持在 Inspector 中实时调节半径与厚度。
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
[ExecuteAlways]
public class ProceduralRingGraphic : MaskableGraphic
{
    [Header("圆环尺寸参数")]
    [Tooltip("圆环中轴半径（像素），决定环的整体大小")]
    [SerializeField] private float radius = 45f;

    [Tooltip("环条的厚度/粗细（像素）")]
    [SerializeField] private float thickness = 10f;

    [Header("填充进度配置")]
    [Tooltip("填充比例 (0 ~ 1)")]
    [Range(0f, 1f)]
    [SerializeField] private float fillAmount = 1f;

    [Tooltip("起始角度（度数）：90 代表正上方 12 点钟方向，0 代表正右方 3 点钟方向")]
    [SerializeField] private float startAngle = 90f;

    [Tooltip("是否顺时针填充")]
    [SerializeField] private bool clockwise = true;

    [Header("圆滑精度")]
    [Tooltip("全圆细分面数（建议 48 ~ 72，值越大边缘越圆滑细腻）")]
    [Range(24, 120)]
    [SerializeField] private int segments = 64;

    public float Radius
    {
        get => radius;
        set { radius = Mathf.Max(0.1f, value); SetVerticesDirty(); }
    }

    public float Thickness
    {
        get => thickness;
        set { thickness = Mathf.Max(0.1f, value); SetVerticesDirty(); }
    }

    public float FillAmount
    {
        get => fillAmount;
        set
        {
            float clamped = Mathf.Clamp01(value);
            if (!Mathf.Approximately(fillAmount, clamped))
            {
                fillAmount = clamped;
                SetVerticesDirty();
            }
        }
    }

    public float StartAngle
    {
        get => startAngle;
        set { startAngle = value; SetVerticesDirty(); }
    }

    public bool Clockwise
    {
        get => clockwise;
        set { clockwise = value; SetVerticesDirty(); }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (radius <= 0f || thickness <= 0f || fillAmount <= 0.0001f)
            return;

        float rInner = Mathf.Max(0.01f, radius - thickness * 0.5f);
        float rOuter = radius + thickness * 0.5f;

        float totalSpan = 360f * fillAmount;
        float dir = clockwise ? -1f : 1f;

        // 根据填充比例动态分配面数，保证过渡平滑
        int steps = Mathf.Max(2, Mathf.CeilToInt(segments * fillAmount));
        Color32 vertColor = color;

        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            float currentAngle = startAngle + dir * totalSpan * t;
            float rad = currentAngle * Mathf.Deg2Rad;

            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);

            Vector3 posInner = new Vector3(rInner * cos, rInner * sin, 0f);
            Vector3 posOuter = new Vector3(rOuter * cos, rOuter * sin, 0f);

            vh.AddVert(posInner, vertColor, Vector2.zero);
            vh.AddVert(posOuter, vertColor, Vector2.one);

            if (i > 0)
            {
                int baseIdx = (i - 1) * 2;
                vh.AddTriangle(baseIdx, baseIdx + 1, baseIdx + 2);
                vh.AddTriangle(baseIdx + 2, baseIdx + 1, baseIdx + 3);
            }
        }
    }

    /// <summary>
    /// 支持 DOTween 平滑补间 FillAmount
    /// </summary>
    public Tweener DOFillAmount(float targetFill, float duration)
    {
        return DOTween.To(() => FillAmount, x => FillAmount = x, targetFill, duration);
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        SetVerticesDirty();
    }
#endif
}