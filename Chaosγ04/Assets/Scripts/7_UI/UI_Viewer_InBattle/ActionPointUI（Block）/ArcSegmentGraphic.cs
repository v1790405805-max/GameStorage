using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UGUI 程序化单段扇环网格渲染器：
/// 用于生成指定起止角度、半径与厚度的圆弧能量格
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
[ExecuteAlways]
public class ArcSegmentGraphic : MaskableGraphic
{
    [Header("扇环尺寸")]
    public float radius = 95f;        // 圆心到弧线中轴的半径
    public float thickness = 14f;     // 弧线厚度（宽度）

    [Header("角度范围 (度数)")]
    public float startAngle = 0f;
    public float endAngle = 30f;

    [Header("网格细分")]
    [Range(4, 40)]
    public int segments = 16;         // 单格细分数，越大边缘越圆滑

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (radius <= 0f || thickness <= 0f) return;

        float rInner = radius - thickness * 0.5f;
        float rOuter = radius + thickness * 0.5f;

        Color32 vertColor = color;

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            float angleDeg = Mathf.Lerp(startAngle, endAngle, t);
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
    /// 动态配置扇环角度与尺寸
    /// </summary>
    public void SetSegment(float newRadius, float newThickness, float fromAngle, float toAngle)
    {
        radius = newRadius;
        thickness = newThickness;
        startAngle = fromAngle;
        endAngle = toAngle;
        SetVerticesDirty();
    }
}