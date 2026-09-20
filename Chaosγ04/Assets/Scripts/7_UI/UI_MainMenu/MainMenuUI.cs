using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class MainMenuUI : MonoBehaviour
{
    [Header("UI引用")]
    public TextMeshProUGUI startGameButtonText;

    private void Start()
    {
        // 检查是否有中途战斗存档
        if (CombatStatsManager.Instance != null && CombatStatsManager.Instance.hasSavedGame)
        {
            if (startGameButtonText != null) startGameButtonText.text = "继续游戏";
        }
        else
        {
            if (startGameButtonText != null) startGameButtonText.text = "开始游戏";
        }
    }

    public void OnStartGameClicked()
    {
        // 如果有中途存档，直接读战斗快照
        if (CombatStatsManager.Instance != null && CombatStatsManager.Instance.hasSavedGame)
        {
            Debug.Log($"读取战斗存档，正在返回场景: {CombatStatsManager.Instance.savedSceneName}");
            SceneManager.LoadScene(CombatStatsManager.Instance.savedSceneName);
        }
        else
        {
            Debug.Log("开启新游戏，初始化全局资产与大地图...");

            // 1. 初始化战斗属性管理器
            if (CombatStatsManager.Instance != null)
            {
                CombatStatsManager.Instance.InitializeCombatStats();
            }

            // 2. 【核心修复 2】彻底重置并初始化本局游戏的 Run 级资产
            if (RunDataManager.Instance != null)
            {
                // 使用 ResetRunData() 而不是仅仅 InitializeRunDeck()
                // 这样不仅会发放初始卡牌，还会把大地图的旧图纸销毁、进度清零！
                RunDataManager.Instance.ResetRunData();
            }

            // 3. 跳转到大地图或第一层级
            SceneManager.LoadScene("1_Map_Scene");
        }
    }
}