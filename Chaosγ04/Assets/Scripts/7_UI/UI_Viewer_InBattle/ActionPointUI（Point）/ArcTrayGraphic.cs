using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UGUI 程序化弧形底槽渲染器：
/// 用于生成头像边缘的弧形托盘、导轨金线等异形背景
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
[ExecuteAlways]
public class ArcTrayGraphic : MaskableGraphic
{
    [Header("几何尺寸")]
    [Tooltip("弧线中心基准半径（像素）")]
    public float radius = 100f;

    [Tooltip("底槽厚度/宽度（像素）")]
    public float thickness = 28f;

    [Header("弧度范围 (度数)")]
    public float startAngle = -35f;
    public float endAngle = -85f;

    [Tooltip("两端额外延伸的角度（防止两头的灯珠卡在边沿）")]
    public float paddingAngle = 8f;

    [Header("圆滑细分")]
    [Range(10, 90)]
    public int segments = 36;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (radius <= 0f || thickness <= 0f) return;

        float rInner = radius - thickness * 0.5f;
        float rOuter = radius + thickness * 0.5f;

        // 向两端微调延展 paddingAngle
        float actualStart = startAngle + (startAngle >= endAngle ? paddingAngle : -paddingAngle);
        float actualEnd = endAngle + (startAngle >= endAngle ? -paddingAngle : paddingAngle);

        Color32 vertColor = color;

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            float angleDeg = Mathf.Lerp(actualStart, actualEnd, t);
            float rad = angleDeg * Mathf.Deg2Rad;

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
    /// 同步弧度与半径参数
    /// </summary>
    public void SetArcParameters(float newRadius, float newStartAngle, float newEndAngle)
    {
        radius = newRadius;
        startAngle = newStartAngle;
        endAngle = newEndAngle;
        SetVerticesDirty();
    }
}