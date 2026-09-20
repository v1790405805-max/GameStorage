using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public struct MonsterSaveData
{
    public string monsterId;
    public MonsterIdentityManager.MonsterType monsterType;
    public Vector3 position;
    public int hp;
    public int block;
}

[Serializable]
public class TurnStartSnapshotData
{
    // ==========================================
    // 【核心新增】快照里要存回合数
    // ==========================================
    public int roundCount;
    public int hp;
    public int block;
    public int energy;
    public int actionPoint;
    public List<CardData> hand = new List<CardData>();
    public List<CardData> drawPile = new List<CardData>();
    public List<CardData> discardPile = new List<CardData>();
    public List<CardData> exhaustPile = new List<CardData>();
    public List<CardData> specialCards = new List<CardData>();
    public Vector3 playerPosition;
    public Quaternion playerRotation;
    public float horizontal;
    public float vertical;
    public List<string> enemyIds = new List<string>();
    public List<Vector3> enemyPositions = new List<Vector3>();
    public List<Quaternion> enemyRotations = new List<Quaternion>();
}

/// <summary>
/// 战斗状态与快照管理器：
/// 负责所有"只在战斗场景有意义"的数据，包括实时战斗属性、怪物位置、手牌快照及中途存档恢复。
///
/// 【职责边界】本类只负责数值的记录与增减计算（护甲/生命/能量/行动点等），
/// 传入多少就修改多少，不做任何"这个数值应该是多少"的业务判断
/// （例如伤害减免、暴击、buff 加成等），这些逻辑交由调用方（如
/// PlayerOrientationDamageController）在调用前算好。
/// </summary>
public class CombatStatsManager : MonoBehaviour
{
    private static CombatStatsManager _instance;
    private static bool _isQuitting = false;
    public static CombatStatsManager Instance
    {
        get
        {
            if (_isQuitting) return null;
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<CombatStatsManager>();
                if (_instance == null)
                {
                    GameObject managerGo = new GameObject("[CombatStatsManager]");
                    _instance = managerGo.AddComponent<CombatStatsManager>();
                }
            }
            return _instance;
        }
    }

    [Header("--- 玩家战斗临时属性 ---")]
    public int maxHP = 66;
    public int currentHP;
    public int currentBlock;
    public int maxEnergy = 3;
    public int currentEnergy;
    public int maxActionPoint = 5;
    public int currentActionPoint;

    [Header("玩家朝向（Animator 参数，Tag=Player）")]
    public float Horizontal;
    public float Vertical;

    [Header("--- 战斗中途存档快照 (Save & Quit 数据) ---")]
    public bool hasSavedGame = false;
    public string savedSceneName;
    public Vector3 savedPlayerPosition;
    public List<MonsterSaveData> savedMonsters = new List<MonsterSaveData>();
    public List<CardData> savedHand = new List<CardData>();
    public List<CardData> savedDrawPile = new List<CardData>();
    public List<CardData> savedDiscardPile = new List<CardData>();
    public List<CardData> savedExhaustPile = new List<CardData>();
    public TurnStartSnapshotData savedTurnStartSnapshot;
    public float savedHorizontal;
    public float savedVertical;

    public event Action OnStatsChanged;

    private void Awake()
    {
        _isQuitting = false;
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnApplicationQuit() => _isQuitting = true;
    private void OnDestroy() { if (_instance == this) _isQuitting = true; }

    /// <summary>
    /// Horizontal/Vertical 是判定"正面接敌"要用到的关键实时数值，
    /// 不能只在存档时才更新一次——每帧从 Tag=Player 的 Animator
    /// 同步一次，保证任何时刻读取到的都是玩家当前的真实朝向。
    /// </summary>
    private void Update()
    {
        CapturePlayerFacing();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize() { _ = Instance; }

    public void InitializeCombatStats()
    {
        currentHP = maxHP;
        currentEnergy = maxEnergy;
        currentActionPoint = maxActionPoint;
        currentBlock = 0;
        Horizontal = 0f;
        Vertical = 0f;
        hasSavedGame = false;
        savedTurnStartSnapshot = null;
        TriggerStatsChanged();
        Debug.Log("[CombatStatsManager] 战斗属性已初始化（全新战斗）。");
    }

    // ===================================================================
    // 玩家朝向（Animator Horizontal/Vertical）记录与恢复
    // ===================================================================

    /// <summary>
    /// 从 Tag=Player 物体上的 Animator 读取 Horizontal/Vertical 朝向参数，存入临时属性。
    /// </summary>
    public void CapturePlayerFacing()
    {
        Animator animator = GetPlayerAnimator();
        if (animator == null) return;
        Horizontal = animator.GetFloat("Horizontal");
        Vertical = animator.GetFloat("Vertical");
    }

    /// <summary>
    /// 将临时属性中记录的 Horizontal/Vertical 朝向参数写回 Tag=Player 物体上的 Animator。
    /// </summary>
    public void RestorePlayerFacing()
    {
        Animator animator = GetPlayerAnimator();
        if (animator == null) return;
        animator.SetFloat("Horizontal", Horizontal);
        animator.SetFloat("Vertical", Vertical);
    }

    private Animator GetPlayerAnimator()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null) return null;
        return playerObj.GetComponentInChildren<Animator>();
    }

    #region 属性修改与扣减核心方法（纯数值计算，传入多少改多少）

    public bool ConsumeEnergy(int amount)
    {
        if (currentEnergy >= amount)
        {
            currentEnergy -= amount;
            TriggerStatsChanged();
            return true;
        }
        return false;
    }

    public void AddBlock(int amount)
    {
        currentBlock += amount;
        TriggerStatsChanged();
    }

    public void Heal(int amount)
    {
        currentHP = Mathf.Min(maxHP, currentHP + amount);
        TriggerStatsChanged();
    }

    public void TakeSelfDamage(int amount)
    {
        currentHP = Mathf.Max(0, currentHP - amount);
        TriggerStatsChanged();
        if (currentHP <= 0)
        {
            Debug.Log("[CombatStatsManager] 玩家生命值归零，触发死亡逻辑。");
        }
    }

    public void ModifyEnergy(int amount, bool allowExceedMax = false)
    {
        currentEnergy += amount;
        if (!allowExceedMax)
        {
            currentEnergy = Mathf.Min(currentEnergy, maxEnergy);
        }
        currentEnergy = Mathf.Max(0, currentEnergy);
        TriggerStatsChanged();
    }

    public bool HasEnoughActionPoint(int amount)
    {
        return currentActionPoint >= amount;
    }

    public bool ConsumeActionPoint(int amount)
    {
        if (HasEnoughActionPoint(amount))
        {
            currentActionPoint -= amount;
            TriggerStatsChanged();
            return true;
        }
        return false;
    }

    public void ModifyActionPoint(int amount, bool allowExceedMax = false)
    {
        currentActionPoint += amount;
        if (!allowExceedMax)
        {
            currentActionPoint = Mathf.Min(currentActionPoint, maxActionPoint);
        }
        currentActionPoint = Mathf.Max(0, currentActionPoint);
        TriggerStatsChanged();
    }

    #endregion

    /// <summary>
    /// 核心功能：打包当前战斗的所有快照并退回主菜单
    /// </summary>
    public void SaveCombatAndExit(string mainMenuSceneName = "0_MainMenu_Scene")
    {
        savedSceneName = SceneManager.GetActiveScene().name;

        MonsterIdentityManager[] allMonsters = FindObjectsByType<MonsterIdentityManager>(FindObjectsSortMode.None);

        if (savedSceneName.Contains("Map") || allMonsters.Length == 0)
        {
            hasSavedGame = false;
            savedMonsters.Clear();
            savedHand.Clear();
            savedDrawPile.Clear();
            savedDiscardPile.Clear();
            savedExhaustPile.Clear();
            savedTurnStartSnapshot = null;
            savedHorizontal = 0f;
            savedVertical = 0f;
            Debug.Log("【CombatStatsManager】当前处于大地图或非战斗状态，已清理战斗残留标记，安全退回主菜单。");
            SceneManager.LoadScene(mainMenuSceneName);
            return;
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) savedPlayerPosition = playerObj.transform.root.position;

        // 记录玩家当前朝向（Animator Horizontal/Vertical），供继续游戏时恢复
        CapturePlayerFacing();
        savedHorizontal = Horizontal;
        savedVertical = Vertical;

        savedMonsters.Clear();
        foreach (var monster in allMonsters)
        {
            MonsterStats stats = monster.GetComponent<MonsterStats>();
            int currentHp = stats != null ? stats.currentHp : 20;
            int currentBlock = stats != null ? stats.currentBlock : 0;

            savedMonsters.Add(new MonsterSaveData
            {
                monsterId = monster.monsterId,
                monsterType = monster.type,
                position = monster.transform.position,
                hp = currentHp,
                block = currentBlock
            });
        }

        if (CardManager.Instance != null)
        {
            savedHand = CloneCardList(CardManager.Instance.hand);
            savedDrawPile = CloneCardList(CardManager.Instance.drawPile);
            savedDiscardPile = CloneCardList(CardManager.Instance.discardPile);
            savedExhaustPile = CloneCardList(CardManager.Instance.exhaustPile);
        }

        if (SLManager.Instance != null && SLManager.Instance.HasSnapshot)
        {
            savedTurnStartSnapshot = SLManager.Instance.ExportSnapshotData();
        }
        else
        {
            savedTurnStartSnapshot = null;
        }

        hasSavedGame = true;
        Debug.Log("【CombatStatsManager】战斗快照打包完毕，安全退回主菜单。");
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private List<CardData> CloneCardList(List<CardData> source)
    {
        List<CardData> result = new List<CardData>();
        foreach (var card in source)
        {
            if (card != null) result.Add(card.Clone());
        }
        return result;
    }

    public void ResetForNewTurn()
    {
        currentEnergy = maxEnergy;
        currentActionPoint = maxActionPoint;
        currentBlock = 0;
        TriggerStatsChanged();
    }

    public void TriggerStatsChanged() => OnStatsChanged?.Invoke();

    /// <summary>
    /// 对玩家造成伤害的唯一入口：只做"先扣护甲、再扣血"的纯数值结算。
    /// 传入的 damageAmount 就是最终要结算的伤害值，本方法不做任何
    /// 减伤/加成运算——那些运算属于调用方（例如
    /// PlayerOrientationDamageController）的职责，应在调用本方法之前完成。
    /// </summary>
    public void TakeDamage(int damageAmount)
    {
        int remainingDamage = damageAmount;

        if (currentBlock > 0)
        {
            if (currentBlock >= remainingDamage)
            {
                currentBlock -= remainingDamage;
                remainingDamage = 0;
            }
            else
            {
                remainingDamage -= currentBlock;
                currentBlock = 0;
            }
        }

        if (remainingDamage > 0)
        {
            currentHP = Mathf.Max(0, currentHP - remainingDamage);
            Debug.Log($"[CombatStatsManager] 玩家受到 {remainingDamage} 点伤害，剩余HP: {currentHP}");
        }

        TriggerStatsChanged();

        if (currentHP <= 0)
        {
            Debug.Log("[CombatStatsManager] 玩家死亡，触发失败结算！");
            if (BattleConditionManager.Instance != null)
                BattleConditionManager.Instance.TriggerDefeat();
        }
    }
}