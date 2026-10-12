using UnityEngine;
using TMPro;
using DG.Tweening;

/// <summary>
/// 环形能量槽控制器：
/// 驱动程序化 ProceduralRingGraphic 进行矢量充能与弹性反馈
/// </summary>
public class EnergyRingUI : MonoBehaviour
{
    [Header("程序化环形组件")]
    [Tooltip("亮色充能环（挂载 ProceduralRingGraphic）")]
    [SerializeField] private ProceduralRingGraphic ringFillGraphic;

    [Tooltip("暗色凹槽底环（可选，挂载 ProceduralRingGraphic）")]
    [SerializeField] private ProceduralRingGraphic ringBgGraphic;

    [Header("文本与装饰")]
    [Tooltip("中心数值文本（显示如 1/3）")]
    [SerializeField] private TextMeshProUGUI valueText;

    [Tooltip("可选：中心漩涡图案（能量变动时可做旋转回弹）")]
    [SerializeField] private Transform centerSwirlTransform;

    [Header("动画配置")]
    [Tooltip("充能环过渡时长（秒）")]
    [SerializeField] private float fillDuration = 0.3f;

    [Tooltip("数值增加时的弹性放大系数")]
    [SerializeField] private float punchScale = 0.08f;

    private int lastEnergy = -1;

    private void Start()
    {
        if (CombatStatsManager.Instance != null)
        {
            CombatStatsManager.Instance.OnStatsChanged += RefreshEnergyDisplay;
            RefreshEnergyDisplay();
        }
    }

    private void OnDestroy()
    {
        if (CombatStatsManager.Instance != null)
        {
            CombatStatsManager.Instance.OnStatsChanged -= RefreshEnergyDisplay;
        }
        transform.DOKill();
    }

    public void RefreshEnergyDisplay()
    {
        if (CombatStatsManager.Instance == null || ringFillGraphic == null) return;

        int cur = CombatStatsManager.Instance.currentEnergy;
        int max = CombatStatsManager.Instance.maxEnergy;
        if (max <= 0) return;

        float targetRatio = Mathf.Clamp01((float)cur / max);

        // 1. 矢量环平滑充能补间
        ringFillGraphic.DOKill();
        ringFillGraphic.DOFillAmount(targetRatio, fillDuration).SetEase(Ease.OutCubic);

        // 2. 文本刷新
        if (valueText != null)
        {
            valueText.text = $"{cur}/{max}";
        }

        // 3. 获得能量时的回弹动效
        if (lastEnergy != -1 && cur > lastEnergy)
        {
            transform.DOKill(true);
            transform.DOPunchScale(Vector3.one * punchScale, 0.25f, 5, 0.4f);

            if (centerSwirlTransform != null)
            {
                centerSwirlTransform.DOKill(true);
                centerSwirlTransform.DOPunchRotation(new Vector3(0, 0, 20f), 0.3f, 4, 0.4f);
            }
        }
        lastEnergy = cur;
    }

    /// <summary>
    /// 在 Inspector 中统一微调环形尺寸（可供外部调用或编辑器联动）
    /// </summary>
    public void SetRingDimensions(float newRadius, float newThickness)
    {
        if (ringFillGraphic != null)
        {
            ringFillGraphic.Radius = newRadius;
            ringFillGraphic.Thickness = newThickness;
        }

        if (ringBgGraphic != null)
        {
            ringBgGraphic.Radius = newRadius;
            ringBgGraphic.Thickness = newThickness;
        }
    }
}