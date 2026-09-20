using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 战斗场景退出控制器，负责提供“保存并退出”的对外接口[cite: 8]
/// </summary>
public class BattleExitController : MonoBehaviour
{
    [Header("主菜单配置")]
    [Tooltip("主菜单的场景名称，用于保存后跳转")]
    public string mainMenuSceneName = "0_MainMenu_Scene";

    /// <summary>
    /// 请将此方法绑定到战斗场景菜单的“保存并返回主菜单”按钮的 OnClick 事件中[cite: 8]
    /// </summary>
    public void OnSaveAndQuitButtonClicked()
    {
        if (CombatStatsManager.Instance != null)
        {
            // 调用 CombatStatsManager 进行快照打包并退出[cite: 8]
            CombatStatsManager.Instance.SaveCombatAndExit(mainMenuSceneName);
        }
        else
        {
            Debug.LogWarning("未找到 CombatStatsManager 单例！只能执行硬退。");
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}