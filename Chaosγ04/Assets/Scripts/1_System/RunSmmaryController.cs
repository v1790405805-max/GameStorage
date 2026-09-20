using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// 负责控制大地图场景中的通关结算界面
/// </summary>
public class RunSummaryController : MonoBehaviour
{
    [Header("UI 引用")]
    public GameObject summaryPanel;
    public TextMeshProUGUI txtKills;
    public TextMeshProUGUI txtDamage;
    public TextMeshProUGUI txtCards;

    [Header("主菜单场景名")]
    public string mainMenuSceneName = "0_MainMenu_Scene";

    private void Start()
    {
        // 一进入大地图场景，就检查是否有待处理的 Boss 通关标记
        if (RunDataManager.Instance != null && RunDataManager.Instance.pendingVictorySummary)
        {
            // 消费掉这个标记，防止重复触发
            RunDataManager.Instance.pendingVictorySummary = false;

            // 弹出结算面板
            ShowVictorySummary();
        }
        else
        {
            // 默认保持隐藏
            if (summaryPanel != null)
            {
                summaryPanel.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 显示真实的通关结算数据
    /// </summary>
    public void ShowVictorySummary()
    {
        if (summaryPanel != null)
        {
            summaryPanel.SetActive(true);
        }

        Time.timeScale = 0f; // 暂停大地图操作

        // ==========================================
        // 【核心修改】读取 RunDataManager 中的真实统计数据
        // ==========================================
        int realKills = 0;
        int realDamage = 0;
        int totalCards = 0;

        if (RunDataManager.Instance != null)
        {
            realKills = RunDataManager.Instance.totalKills;
            realDamage = RunDataManager.Instance.totalDamageDealt;
            totalCards = RunDataManager.Instance.playerGlobalDeck.Count;
        }

        // 填充到 UI 上
        if (txtKills != null) txtKills.text = $"击杀怪物：{realKills}";
        if (txtDamage != null) txtDamage.text = $"造成伤害：{realDamage}";
        if (txtCards != null) txtCards.text = $"收集卡牌：{totalCards} 张";

        Debug.Log($"[RunSummary] 已在大地图成功弹出真实的通关结算面板：杀敌{realKills}，伤害{realDamage}，卡牌{totalCards}");
    }

    /// <summary>
    /// 绑定给面板上的“结束游戏”按钮
    /// </summary>
    public void OnEndGameClicked()
    {
        Time.timeScale = 1f;

        // 清理战斗与大地图全局存档
        if (CombatStatsManager.Instance != null)
        {
            CombatStatsManager.Instance.hasSavedGame = false;
        }

        if (RunDataManager.Instance != null)
        {
            RunDataManager.Instance.ResetRunData();
        }

        Debug.Log("[RunSummary] 通关结束，返回主菜单。");
        SceneManager.LoadScene(mainMenuSceneName);
    }
}