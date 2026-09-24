using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public interface ITurnStateListener
{
    void OnTurnActivated();
    void OnTurnDeactivated();
}

public enum TurnState
{
    Player,
    Enemy
}

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    [Header("回合状态配置")]
    [SerializeField] private TurnState currentTurn = TurnState.Player;
    public TurnState CurrentTurn => currentTurn;

    [Header("回合计数器")]
    [Tooltip("当前是第几个回合")]
    public int currentRoundCount = 0;

    [Tooltip("用来显示回合数的 UI 文本")]
    public TextMeshProUGUI turnCounterText;

    [Header("我方行为逻辑脚本（玩家回合开启，敌方回合关闭）")]
    [SerializeField] private List<MonoBehaviour> playerBehaviours = new List<MonoBehaviour>();

    [Header("敌方行为管理器列表（按顺序执行行动）")]
    [SerializeField] private List<MonsterActionManager> enemyBehaviours = new List<MonsterActionManager>();

    private Coroutine enemyTurnCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        currentRoundCount = 0;

        if (CombatStatsManager.Instance != null && CombatStatsManager.Instance.hasSavedGame)
        {
            Debug.Log("========== 检测到战斗存档，正在准备恢复游戏 ==========");
            StartCoroutine(RestoreGameFromSaveRoutine());
        }
        else
        {
            if (CombatStatsManager.Instance != null)
            {
                CombatStatsManager.Instance.InitializeCombatStats();
            }

            StartNewPlayerTurn();
        }
    }

    private IEnumerator RestoreGameFromSaveRoutine()
    {
        yield return null;

        var saveObj = CombatStatsManager.Instance;

        if (CombatStatsManager.Instance != null)
        {
            CombatStatsManager.Instance.TriggerStatsChanged();
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerObj.transform.root.position = saveObj.savedPlayerPosition;
            Debug.Log($"[TurnManager] 已恢复玩家位置至: {saveObj.savedPlayerPosition}");
        }

        saveObj.Horizontal = saveObj.savedHorizontal;
        saveObj.Vertical = saveObj.savedVertical;
        saveObj.RestorePlayerFacing();

        MonsterIdentityManager[] sceneMonsters = FindObjectsByType<MonsterIdentityManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<MonsterIdentityManager> unhandledSceneMonsters = new List<MonsterIdentityManager>(sceneMonsters);

        foreach (MonsterSaveData savedData in saveObj.savedMonsters)
        {
            MonsterIdentityManager targetMonster = null;

            if (!string.IsNullOrEmpty(savedData.monsterId))
            {
                targetMonster = unhandledSceneMonsters.Find(m => m.monsterId == savedData.monsterId);
            }

            if (targetMonster == null)
            {
                targetMonster = unhandledSceneMonsters.Find(m => m.type == savedData.monsterType);
            }

            if (targetMonster != null)
            {
                targetMonster.transform.position = savedData.position;

                MonsterStats stats = targetMonster.GetComponent<MonsterStats>();
                if (stats != null)
                {
                    stats.currentHp = savedData.hp;
                    stats.currentBlock = savedData.block;
                }

                unhandledSceneMonsters.Remove(targetMonster);
            }
        }

        foreach (var extraMonster in unhandledSceneMonsters)
        {
            Destroy(extraMonster.gameObject);
        }

        if (CardManager.Instance != null)
        {
            CardManager.Instance.RestoreDeckFromSave(
                saveObj.savedHand,
                saveObj.savedDrawPile,
                saveObj.savedDiscardPile,
                saveObj.savedExhaustPile
            );
        }

        saveObj.hasSavedGame = false;
        currentTurn = TurnState.Player;
        SetGroupState(playerBehaviours, true);

        if (saveObj.savedTurnStartSnapshot != null && SLManager.Instance != null)
        {
            SLManager.Instance.ImportSnapshotData(saveObj.savedTurnStartSnapshot);
        }

        Debug.Log("========== 战斗存档精准恢复完毕 ==========");
    }

    public void OnEndRoundButtonPressed()
    {
        if (currentTurn == TurnState.Player)
        {
            SetTurn(TurnState.Enemy);
        }
        else
        {
            Debug.LogWarning("当前处于非玩家回合或正在结算中，请勿连点！");
        }
    }

    public void SetTurn(TurnState newTurn)
    {
        if (CombatStatsManager.Instance != null && CombatStatsManager.Instance.currentHP <= 0)
        {
            return;
        }

        if (currentTurn == newTurn) return;

        if (enemyTurnCoroutine != null)
        {
            StopCoroutine(enemyTurnCoroutine);
            enemyTurnCoroutine = null;
        }

        currentTurn = newTurn;
        ApplyTurnState();
    }

    private void ApplyTurnState()
    {
        bool isPlayerTurn = (currentTurn == TurnState.Player);

        if (isPlayerTurn)
        {
            StartNewPlayerTurn();
        }
        else
        {
            StartEnemyTurn();
        }
    }

    private void StartNewPlayerTurn()
    {
        currentRoundCount++;
        UpdateTurnCounterUI();

        Debug.Log($"========== 第 {currentRoundCount} 回合 / 玩家回合开始 ==========");

        SetGroupState(playerBehaviours, true);

        // ==========================================
        // 【新增】：玩家回合开始，刷新全场怪物的攻击意图
        // ==========================================
        MonsterInfoUI[] allMonsterUIs = FindObjectsByType<MonsterInfoUI>(FindObjectsSortMode.None);
        foreach (var monsterUI in allMonsterUIs)
        {
            MonsterAttackAction attackAction = monsterUI.GetComponentInParent<MonsterAttackAction>();
            if (attackAction != null)
            {
                monsterUI.SetIntent(null, attackAction.attackDamage);
            }
        }

        if (CombatStatsManager.Instance != null)
        {
            CombatStatsManager.Instance.ResetForNewTurn();
        }

        AbilityCore.NotifyPlayerTurnStarted();

        if (CardManager.Instance != null)
        {
            CardManager.Instance.StartTurn();
        }
    }

    private void StartEnemyTurn()
    {
        Debug.Log($"========== 第 {currentRoundCount} 回合 / 玩家回合结束 / 敌方回合开始 ==========");

        SetGroupState(playerBehaviours, false);
        AbilityCore.NotifyEnemyTurnStarted();

        // ==========================================
        // 【新增】：怪物开始行动，隐藏意图图标
        // ==========================================
        MonsterInfoUI[] allMonsterUIs = FindObjectsByType<MonsterInfoUI>(FindObjectsSortMode.None);
        foreach (var monsterUI in allMonsterUIs)
        {
            monsterUI.HideIntent();
        }

        if (CardManager.Instance != null)
        {
            CardManager.Instance.DiscardHand();
        }

        enemyTurnCoroutine = StartCoroutine(ExecuteEnemyTurnSequence());
    }

    private IEnumerator ExecuteEnemyTurnSequence()
    {
        for (int i = 0; i < enemyBehaviours.Count; i++)
        {
            if (CombatStatsManager.Instance != null && CombatStatsManager.Instance.currentHP <= 0)
            {
                yield break;
            }

            var monsterMgr = enemyBehaviours[i];
            if (monsterMgr == null) continue;

            bool isFinished = false;
            Action finishHandler = null;

            finishHandler = () =>
            {
                isFinished = true;
                monsterMgr.OnSequenceFinished -= finishHandler;
            };

            monsterMgr.OnSequenceFinished += finishHandler;
            monsterMgr.OnTurnActivated();

            yield return new WaitUntil(() => isFinished);
        }

        enemyTurnCoroutine = null;
        SetTurn(TurnState.Player);
    }

    private void SetGroupState(List<MonoBehaviour> group, bool active)
    {
        foreach (var behaviour in group)
        {
            if (behaviour == null) continue;

            if (!active && behaviour.enabled && behaviour is ITurnStateListener listenerBeforeOff)
            {
                listenerBeforeOff.OnTurnDeactivated();
            }

            behaviour.enabled = active;

            if (active && behaviour is ITurnStateListener listenerAfterOn)
            {
                listenerAfterOn.OnTurnActivated();
            }
        }
    }

    public void RegisterPlayerBehaviour(MonoBehaviour behaviour)
    {
        if (behaviour == null || playerBehaviours.Contains(behaviour)) return;
        playerBehaviours.Add(behaviour);
        behaviour.enabled = (currentTurn == TurnState.Player);
    }

    public void RegisterEnemyBehaviour(MonsterActionManager behaviour)
    {
        if (behaviour == null || enemyBehaviours.Contains(behaviour)) return;
        enemyBehaviours.Add(behaviour);
    }

    public void UnregisterPlayerBehaviour(MonoBehaviour behaviour) => playerBehaviours.Remove(behaviour);

    public void UnregisterEnemyBehaviour(MonsterActionManager behaviour) => enemyBehaviours.Remove(behaviour);

    public void RestoreRoundCount(int savedRound)
    {
        currentRoundCount = savedRound;
        UpdateTurnCounterUI();
        Debug.Log($"[TurnManager] 已从存档恢复回合数：第 {currentRoundCount} 回合");
    }

    private void UpdateTurnCounterUI()
    {
        if (turnCounterText != null)
        {
            turnCounterText.text = $"回合 {currentRoundCount}";
        }
    }
}
