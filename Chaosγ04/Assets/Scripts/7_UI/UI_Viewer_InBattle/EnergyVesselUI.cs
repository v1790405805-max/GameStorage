using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// 异形灵魂容器/能量槽控制器：
/// 自动监听 CombatStatsManager 并驱动非规则液位平滑升降与受击/获得回弹动效
/// </summary>
public class EnergyVesselUI : MonoBehaviour
{
    [Header("核心组件绑定")]
    [Tooltip("中间层的液体 Image（Image Type 必须设为 Filled，Fill Method 设为 Vertical）")]
    [SerializeField] private Image liquidImage;

    [Tooltip("可选：能量数值文本（如 3/3）")]
    [SerializeField] private TextMeshProUGUI energyText;

    [Tooltip("可选：满能光晕 CanvasGroup（能量全满时亮起）")]
    [SerializeField] private CanvasGroup fullGlowGroup;

    [Header("动效参数")]
    [Tooltip("液位补间变化耗时（秒）")]
    [SerializeField] private float fillDuration = 0.35f;

    [Tooltip("获得能量时的容器弹性跳动幅度")]
    [SerializeField] private float punchScaleFactor = 0.12f;

    private int lastEnergy = -1;

    private void Start()
    {
        if (CombatStatsManager.Instance != null)
        {
            CombatStatsManager.Instance.OnStatsChanged += RefreshEnergy;
            RefreshEnergy();
        }
    }

    private void OnDestroy()
    {
        if (CombatStatsManager.Instance != null)
        {
            CombatStatsManager.Instance.OnStatsChanged -= RefreshEnergy;
        }
        transform.DOKill();
    }

    /// <summary>
    /// 刷新能量容器表现
    /// </summary>
    public void RefreshEnergy()
    {
        if (CombatStatsManager.Instance == null || liquidImage == null) return;

        int current = CombatStatsManager.Instance.currentEnergy;
        int max = CombatStatsManager.Instance.maxEnergy;
        if (max <= 0) return;

        float targetRatio = Mathf.Clamp01((float)current / max);

        // 1. 液位平滑上升/下降补间
        liquidImage.DOKill();
        liquidImage.DOFillAmount(targetRatio, fillDuration).SetEase(Ease.OutCubic);

        // 2. 数值文本更新
        if (energyText != null)
        {
            energyText.text = $"{current}/{max}";
        }

        // 3. 获得能量时的回弹震颤反馈 (Punch Scale)
        if (lastEnergy != -1 && current > lastEnergy)
        {
            transform.DOKill(true);
            transform.DOPunchScale(Vector3.one * punchScaleFactor, 0.25f, 6, 0.5f);
        }
        lastEnergy = current;

        // 4. 满能量泛光控制
        if (fullGlowGroup != null)
        {
            fullGlowGroup.DOKill();
            fullGlowGroup.DOFade(current >= max ? 1f : 0f, 0.25f);
        }
    }
}