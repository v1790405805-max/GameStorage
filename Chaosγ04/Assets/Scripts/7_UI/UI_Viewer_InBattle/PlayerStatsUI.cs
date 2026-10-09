using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerStatsUI : MonoBehaviour
{
    [Header("--- 血量 (HP) ---")]
    public Slider hpSlider;              // 拖入 HP Canvas[cite: 24]
    public TextMeshProUGUI hpText;       // 拖入 HP Value[cite: 24]
    [Tooltip("血量文本格式：{0} 为当前血量，{1} 为血量上限。例如：'{0} / {1}'、'{0}' 或 'HP {0}/{1}'")]
    public string hpFormat = "{0} / {1}";

    [Header("--- 护甲 (Armor) ---")]
    public Slider armorSlider;           // 拖入 Armor 节点下的 HP Canvas[cite: 24]
    public TextMeshProUGUI armorText;    // 拖入 Armor Value[cite: 24]
    [Tooltip("护甲文本格式：{0} 为当前护甲值。例如：'{0}' 或 '盾 {0}'")]
    public string armorFormat = "{0}";
    [Tooltip("护甲进度条满管时代表的数值上限（因为护甲通常无上限，为了让进度条有长短变化设定的视觉参考值）")]
    public int maxArmorVisual = 50;      //[cite: 24]

    [Header("--- 能量 (Energy) ---")]
    public Slider energySlider;          // 拖入 Energy 节点下的 Canvas[cite: 24]
    public TextMeshProUGUI energyText;   // 拖入 Energy Value[cite: 24]
    [Tooltip("能量文本格式：{0} 为当前能量，{1} 为能量上限。例如：'{0} / {1}' 或 '{0}'")]
    public string energyFormat = "{0} / {1}";

    [Header("--- 行动点 (Action Point) ---")]
    public Slider actionPointSlider;     // 拖入 ActionPoint 节点下的 Canvas[cite: 24]
    public TextMeshProUGUI actionPointText;// 拖入 ActionPoint Value[cite: 24]
    [Tooltip("行动点文本格式：{0} 为当前行动点，{1} 为行动点上限。例如：'{0} / {1}' 或 '{0}'")]
    public string actionPointFormat = "{0} / {1}";

    [Header("--- 本回合行动力消耗 (Action Point Count) ---")]
    public Slider actionPointCountSlider; // 拖入 ActionPointCount Canvas[cite: 24]
    public TextMeshProUGUI actionPointCountText; // 拖入 ActionPointCount Value[cite: 24]
    [Tooltip("行动力消耗文本格式：{0} 为已消耗行动力数值")]
    public string actionPointCountFormat = "{0}";
    [Tooltip("本回合行动力消耗条的视觉满值，用于控制 Slider 的填充比例。")]
    public int maxActionPointCountVisual = 50; //[cite: 24]

    private void Start()
    {
        // 订阅 PlayerStatsManager 的属性变化事件[cite: 24]
        if (CombatStatsManager.Instance != null)
        {
            CombatStatsManager.Instance.OnStatsChanged += UpdateStatsUI; //[cite: 24]
            CombatStatsManager.Instance.OnActionPointCountChanged += UpdateActionPointCountUI; //[cite: 24]
            UpdateStatsUI(); // 初始化时刷新一次[cite: 24]
            UpdateActionPointCountUI(CombatStatsManager.Instance.UsedActionPointCount); //[cite: 24]
        }
    }

    private void OnDestroy()
    {
        // 取消订阅，防止内存泄漏[cite: 24]
        if (CombatStatsManager.Instance != null)
        {
            CombatStatsManager.Instance.OnStatsChanged -= UpdateStatsUI; //[cite: 24]
            CombatStatsManager.Instance.OnActionPointCountChanged -= UpdateActionPointCountUI; //[cite: 24]
        }
    }

    /// <summary>
    /// 统一刷新所有状态的 UI 显示与进度条[cite: 24]
    /// </summary>
    private void UpdateStatsUI()
    {
        if (CombatStatsManager.Instance == null) return; //[cite: 24]

        var stats = CombatStatsManager.Instance; //[cite: 24]

        // ================== 1. 更新血量 (HP) ==================[cite: 24]
        if (hpSlider != null && stats.maxHP > 0)
        {
            hpSlider.value = Mathf.Clamp01((float)stats.currentHP / stats.maxHP); //[cite: 24]
        }
        if (hpText != null)
        {
            hpText.text = FormatSafe(hpFormat, stats.currentHP, stats.maxHP);
        }

        // ================== 2. 更新护甲 (Armor) ==================[cite: 24]
        if (armorSlider != null && maxArmorVisual > 0)
        {
            armorSlider.value = Mathf.Clamp01((float)stats.currentBlock / maxArmorVisual); //[cite: 24]
        }
        if (armorText != null)
        {
            armorText.text = FormatSafe(armorFormat, stats.currentBlock);
        }

        // ================== 3. 更新能量 (Energy) ==================[cite: 24]
        if (energySlider != null && stats.maxEnergy > 0)
        {
            energySlider.value = Mathf.Clamp01((float)stats.currentEnergy / stats.maxEnergy); //[cite: 24]
        }
        if (energyText != null)
        {
            energyText.text = FormatSafe(energyFormat, stats.currentEnergy, stats.maxEnergy);
        }

        // ================== 4. 更新行动点 (Action Point) ==================[cite: 24]
        if (actionPointSlider != null && stats.maxActionPoint > 0)
        {
            actionPointSlider.value = Mathf.Clamp01((float)stats.currentActionPoint / stats.maxActionPoint); //[cite: 24]
        }
        if (actionPointText != null)
        {
            actionPointText.text = FormatSafe(actionPointFormat, stats.currentActionPoint, stats.maxActionPoint);
        }
        if (energyText != null)
        {
            energyText.text = FormatSafe(energyFormat, stats.currentEnergy, stats.maxEnergy);
            Debug.Log($"[PlayerStatsUI] 赋值完成，对象名: {energyText.gameObject.name}，文本内容: '{energyText.text}'");
        }
    }

    private void UpdateActionPointCountUI(int usedActionPointCount)
    {
        if (actionPointCountSlider != null && maxActionPointCountVisual > 0)
        {
            actionPointCountSlider.value = Mathf.Clamp01((float)usedActionPointCount / maxActionPointCountVisual); //[cite: 24]
        }

        if (actionPointCountText != null)
        {
            actionPointCountText.text = FormatSafe(actionPointCountFormat, usedActionPointCount);
        }
    }

    /// <summary>
    /// 安全格式化文本：当 Inspector 填写的占位符有误（如括号未闭合）时自动兜底，避免运行时报错
    /// </summary>
    private string FormatSafe(string format, params object[] args)
    {
        if (string.IsNullOrEmpty(format))
        {
            return args != null && args.Length > 0 ? string.Join(" / ", args) : string.Empty;
        }

        try
        {
            return string.Format(format, args);
        }
        catch (System.FormatException)
        {
            return string.Join(" / ", args);
        }
    }
}