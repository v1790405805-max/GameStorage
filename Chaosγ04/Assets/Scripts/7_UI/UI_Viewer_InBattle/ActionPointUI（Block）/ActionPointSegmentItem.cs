using UnityEngine;
using DG.Tweening;

/// <summary>
/// 单个行动点分段格子：管理亮起、熄灭、消耗微缩动画
/// </summary>
public class ActionPointSegmentItem : MonoBehaviour
{
    [Header("网格组件引用")]
    [SerializeField] private ArcSegmentGraphic graphic;

    [Header("颜色配置")]
    [SerializeField] private Color litColor = new Color(1f, 0.85f, 0.2f, 1f);      // 点亮：金黄发光
    [SerializeField] private Color unlitColor = new Color(0.15f, 0.15f, 0.25f, 0.5f);// 熄灭：暗深色半透明
    [SerializeField] private Color previewColor = new Color(0.95f, 0.3f, 0.25f, 1f); // 预扣费：警戒红

    private bool isLit = true;

    private void Awake()
    {
        if (graphic == null) graphic = GetComponent<ArcSegmentGraphic>();
    }

    public void InitShape(float radius, float thickness, float startAngle, float endAngle)
    {
        if (graphic == null) graphic = GetComponent<ArcSegmentGraphic>();
        graphic.SetSegment(radius, thickness, startAngle, endAngle);
    }

    /// <summary>
    /// 更新亮灭状态
    /// </summary>
    public void SetState(bool active, bool animate = true)
    {
        if (graphic == null) return;
        if (isLit == active && animate) return;

        isLit = active;
        graphic.DOKill();
        transform.DOKill();

        Color targetColor = active ? litColor : unlitColor;

        if (!animate)
        {
            graphic.color = targetColor;
            return;
        }

        if (active)
        {
            // 恢复/点亮：平滑变色
            graphic.DOColor(targetColor, 0.25f).SetEase(Ease.OutQuad);
        }
        else
        {
            // 消耗瞬间：轻微缩放震颤 + 变暗
            transform.DOPunchScale(Vector3.one * 0.15f, 0.2f, 4, 0.5f);
            graphic.DOColor(targetColor, 0.2f).SetEase(Ease.OutQuad);
        }
    }

    /// <summary>
    /// 设置预消耗预览状态（如拖拽移动或卡牌悬停时）
    /// </summary>
    public void SetPreview(bool isPreviewCost)
    {
        if (graphic == null || !isLit) return;
        graphic.DOKill();
        graphic.color = isPreviewCost ? previewColor : litColor;
    }
}