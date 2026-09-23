using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerStatsUI : MonoBehaviour
{
    [Header("--- 血量 (HP) ---")]
    public Slider hpSlider;              // 拖入 HP Canvas
    public TextMeshProUGUI hpText;       // 拖入 HP Value

    [Header("--- 护甲 (Armor) ---")]
    public Slider armorSlider;           // 拖入 Armor 节点下的 HP Canvas(建议改名为 Armor Canvas)
    public TextMeshProUGUI armorText;    // 拖入 Armor Value
    [Tooltip("护甲进度条满管时代表的数值上限（因为护甲通常无上限，为了让进度条有长短变化设定的视觉参考值）")]
    public int maxArmorVisual = 50;

    [Header("--- 能量 (Energy) ---")]
    public Slider energySlider;          // 拖入 Energy 节点下的 Canvas
    public TextMeshProUGUI energyText;   // 拖入 Energy Value

    [Header("--- 行动点 (Action Point) ---")]
    public Slider actionPointSlider;     // 拖入 ActionPoint 节点下的 Canvas
    public TextMeshProUGUI actionPointText;// 拖入 ActionPoint Value

    [Header("--- 本回合行动力消耗 (Action Point Count) ---")]
    public Slider actionPointCountSlider; // 拖入 ActionPointCount Canvas
    public TextMeshProUGUI actionPointCountText; // 拖入 ActionPointCount Value
    [Tooltip("本回合行动力消耗条的视觉满值，用于控制 Slider 的填充比例。")]
    public int maxActionPointCountVisual = 50;

    private void Start()
    {
        // 订阅 PlayerStatsManager 的属性变化事件
        if (CombatStatsManager.Instance != null)
        {
            CombatStatsManager.Instance.OnStatsChanged += UpdateStatsUI;
            CombatStatsManager.Instance.OnActionPointCountChanged += UpdateActionPointCountUI;
            UpdateStatsUI(); // 初始化时刷新一次
            UpdateActionPointCountUI(CombatStatsManager.Instance.UsedActionPointCount);
        }
    }

    private void OnDestroy()
    {
        // 取消订阅，防止内存泄漏
        if (CombatStatsManager.Instance != null)
        {
            CombatStatsManager.Instance.OnStatsChanged -= UpdateStatsUI;
            CombatStatsManager.Instance.OnActionPointCountChanged -= UpdateActionPointCountUI;
        }
    }

    /// <summary>
    /// 统一刷新所有状态的 UI 显示与进度条
    /// </summary>
    private void UpdateStatsUI()
    {
        if (CombatStatsManager.Instance == null) return;

        // 获取属性管理器实例，方便后续调用
        var stats = CombatStatsManager.Instance;

        // ================== 1. 更新血量 (HP) ==================
        if (hpSlider != null && stats.maxHP > 0)
        {
            hpSlider.value = Mathf.Clamp01((float)stats.currentHP / stats.maxHP);
        }
        if (hpText != null)
        {
            hpText.text = $"{stats.currentHP} / {stats.maxHP}";
        }

        // ================== 2. 更新护甲 (Armor) ==================
        if (armorSlider != null && maxArmorVisual > 0)
        {
            // 护甲条的填充比例 = 当前护甲 / 设定的视觉上限
            armorSlider.value = Mathf.Clamp01((float)stats.currentBlock / maxArmorVisual);
        }
        if (armorText != null)
        {
            // 护甲通常只显示当前数值，不显示 "/上限"
            armorText.text = stats.currentBlock.ToString();
        }

        // ================== 3. 更新能量 (Energy) ==================
        if (energySlider != null && stats.maxEnergy > 0)
        {
            energySlider.value = Mathf.Clamp01((float)stats.currentEnergy / stats.maxEnergy);
        }
        if (energyText != null)
        {
            energyText.text = $"{stats.currentEnergy} / {stats.maxEnergy}";
        }

        // ================== 4. 更新行动点 (Action Point) ==================
        if (actionPointSlider != null && stats.maxActionPoint > 0)
        {
            actionPointSlider.value = Mathf.Clamp01((float)stats.currentActionPoint / stats.maxActionPoint);
        }
        if (actionPointText != null)
        {
            actionPointText.text = $"{stats.currentActionPoint} / {stats.maxActionPoint}";
        }
    }

    private void UpdateActionPointCountUI(int usedActionPointCount)
    {
        if (actionPointCountSlider != null && maxActionPointCountVisual > 0)
        {
            actionPointCountSlider.value = Mathf.Clamp01((float)usedActionPointCount / maxActionPointCountVisual);
        }

        if (actionPointCountText != null)
        {
            actionPointCountText.text = usedActionPointCount.ToString();
        }
    }
}