using UnityEngine;
using DG.Tweening;

/// <summary>
/// 单个行动点分段格子：管理亮起、熄灭、消耗微缩与预扣警戒红
/// </summary>
public class ActionPointSegmentItem : MonoBehaviour
{
    [Header("网格组件引用")]
    [SerializeField] private ArcSegmentGraphic graphic;

    [Header("颜色配置")]
    [SerializeField] private Color litColor = new Color(1f, 0.85f, 0.2f, 1f);        // 点亮：金黄发光
    [SerializeField] private Color unlitColor = new Color(0.15f, 0.15f, 0.25f, 0.5f); // 熄灭：暗深色半透明
    [SerializeField] private Color previewColor = new Color(0.95f, 0.3f, 0.25f, 1f);  // 预扣费：警戒红

    private bool isLit = false;
    private bool hasInitialized = false;

    private void Awake()
    {
        if (graphic == null) graphic = GetComponent<ArcSegmentGraphic>();
    }

    public void InitShape(float radius, float thickness, float startAngle, float endAngle)
    {
        if (graphic == null) graphic = GetComponent<ArcSegmentGraphic>();
        if (graphic != null)
        {
            graphic.SetSegment(radius, thickness, startAngle, endAngle);
            // 解决开局白色问题：生成形状时立即刷上初始金色，防止 Prefab 默认纯白暴露
            graphic.color = litColor;
        }
    }

    /// <summary>
    /// 更新亮灭状态
    /// </summary>
    public void SetState(bool active, bool animate = true)
    {
        if (graphic == null) graphic = GetComponent<ArcSegmentGraphic>();
        if (graphic == null) return;

        // 仅在已成功初始化、且状态未变时才跳过
        if (hasInitialized && isLit == active && animate) return;

        hasInitialized = true;
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
            graphic.DOColor(targetColor, 0.25f).SetEase(Ease.OutQuad);
        }
        else
        {
            transform.DOPunchScale(Vector3.one * 0.15f, 0.2f, 4, 0.5f);
            graphic.DOColor(targetColor, 0.2f).SetEase(Ease.OutQuad);
        }
    }

    /// <summary>
    /// 设置预消耗预览状态
    /// </summary>
    public void SetPreview(bool isPreviewCost)
    {
        if (graphic == null || !isLit) return;
        graphic.DOKill();
        graphic.color = isPreviewCost ? previewColor : litColor;
    }
}