using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class BattleConditionManager : MonoBehaviour
{
    public static BattleConditionManager Instance { get; private set; }

    [Header("结算UI引用")]
    public BattleRewardUI rewardUI; // 普通战斗的奖励UI

    [Header("场景跳转配置")]
    [Tooltip("大地图场景的名称")]
    public string mapSceneName = "1_Map_Scene";

    private bool isBattleEnded = false;

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        MonsterStats.OnAnyMonsterDied += CheckWinCondition;
    }

    private void OnDisable()
    {
        MonsterStats.OnAnyMonsterDied -= CheckWinCondition;
    }

    private void CheckWinCondition()
    {
        if (isBattleEnded) return;
        StartCoroutine(DelayCheckWinRoutine());
    }

    private IEnumerator DelayCheckWinRoutine()
    {
        yield return new WaitForEndOfFrame();

        if (MonsterIdentitySystem.Instance.MonsterCount <= 0)
        {
            TriggerVictory();
        }
    }

    public void TriggerVictory()
    {
        if (isBattleEnded) return;
        isBattleEnded = true;
        Debug.Log("【战斗结束】玩家胜利！");

        // ==========================================
        // 【核心修改】如果是 Boss 战胜利
        // ==========================================
        if (RunDataManager.Instance != null && RunDataManager.Instance.currentNodeType == MapNodeType.Boss)
        {
            Debug.Log("【战斗结束】Boss 被击败，设置通关标记，准备返回大地图弹出结算！");

            // 1. 设置跨场景通关标记
            RunDataManager.Instance.pendingVictorySummary = true;

            // 2. 直接切回大地图场景
            SceneManager.LoadScene(mapSceneName);
            return;
        }

        // ==========================================
        // 普通战斗胜利流程
        // ==========================================
        if (rewardUI != null)
        {
            rewardUI.ShowVictoryReward();
        }
    }

    public void TriggerDefeat()
    {
        if (isBattleEnded) return;
        isBattleEnded = true;
        Debug.Log("【战斗结束】玩家失败！");

        if (rewardUI != null)
        {
            rewardUI.ShowDefeat();
        }
    }
}