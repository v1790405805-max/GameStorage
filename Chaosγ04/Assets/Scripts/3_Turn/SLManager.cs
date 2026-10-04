using System.Collections.Generic;
using UnityEngine;

public class SLManager : MonoBehaviour, ITurnStateListener
{
    public static SLManager Instance { get; private set; }

    [Header("SL 功能开关")]
    [Tooltip("是否只允许在玩家回合内进行读档")]
    [SerializeField] private bool onlyAllowLoadDuringPlayerTurn = true;

    [Header("是否发送调试数据")]
    [SerializeField] private bool logDebugInfo = true;

    [Header("存档状态")]
    [SerializeField] private bool hasSnapshot;
    public bool HasSnapshot => hasSnapshot;

    // ================= 快照数据字段 =================
    [Header("快照 - 玩家状态（仅供 Inspector 预览）")]
    [SerializeField] private int snapshotHP;
    [SerializeField] private int snapshotBlock;
    [SerializeField] private int snapshotEnergy;
    [SerializeField] private int snapshotActionPoint;
    [SerializeField] private int snapshotUsedActionPointCount;

    // 玩家朝向（Animator 参数）快照
    [SerializeField] private float snapshotHorizontal;
    [SerializeField] private float snapshotVertical;

    // 回合数快照
    [SerializeField] private int snapshotRoundCount;

    [Header("快照 - 卡组状态（仅供 Inspector 预览）")]
    [SerializeField] private List<CardData> snapshotHand = new List<CardData>();
    [SerializeField] private List<CardData> snapshotDrawPile = new List<CardData>();
    [SerializeField] private List<CardData> snapshotDiscardPile = new List<CardData>();
    [SerializeField] private List<CardData> snapshotExhaustPile = new List<CardData>();
    [SerializeField] private List<CardData> snapshotSpecialCards = new List<CardData>();

    [Header("快照 - 位置状态（仅供 Inspector 预览）")]
    [SerializeField] private Vector3 snapshotPlayerPosition;
    [SerializeField] private Quaternion snapshotPlayerRotation;

    [Header("快照 - 怪物完整状态（仅供 Inspector 预览）")]
    [SerializeField] private List<MonsterStateSnapshot> snapshotMonsters = new List<MonsterStateSnapshot>();

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
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.RegisterPlayerBehaviour(this);
        }
        else
        {
            Debug.LogWarning("[SLManager] 未找到 TurnManager 实例。");
        }

        if (!hasSnapshot)
        {
            StartCoroutine(CaptureSnapshotNextFrame());
        }
    }

    public void OnTurnActivated()
    {
        hasSnapshot = false;
        StopAllCoroutines();
        StartCoroutine(CaptureSnapshotNextFrame());
    }

    public void OnTurnDeactivated() { }

    private System.Collections.IEnumerator CaptureSnapshotNextFrame()
    {
        yield return new WaitForEndOfFrame();
        if (!hasSnapshot)
        {
            CaptureSnapshot();
        }
    }

    public void CaptureSnapshot()
    {
        CapturePlayerStats();
        CaptureCardState();
        CapturePositions();
        CaptureEnemyStates();

        if (TurnManager.Instance != null)
        {
            snapshotRoundCount = TurnManager.Instance.currentRoundCount;
        }

        hasSnapshot = true;
        if (logDebugInfo)
        {
            Debug.Log($"[SLManager] 已记录回合快照 | 手牌数:{snapshotHand.Count} 抽牌堆:{snapshotDrawPile.Count} 怪物数:{snapshotMonsters.Count}");
        }
    }

    private void CapturePlayerStats()
    {
        var stats = CombatStatsManager.Instance;
        if (stats == null) return;

        stats.CapturePlayerFacing();
        snapshotHP = stats.currentHP;
        snapshotBlock = stats.currentBlock;
        snapshotEnergy = stats.currentEnergy;
        snapshotActionPoint = stats.currentActionPoint;
        snapshotUsedActionPointCount = stats.UsedActionPointCount;
        snapshotHorizontal = stats.Horizontal;
        snapshotVertical = stats.Vertical;
    }

    private void CaptureCardState()
    {
        var cards = CardManager.Instance;
        if (cards == null) return;
        snapshotHand = CloneCardList(cards.hand);
        snapshotDrawPile = CloneCardList(cards.drawPile);
        snapshotDiscardPile = CloneCardList(cards.discardPile);
        snapshotExhaustPile = CloneCardList(cards.exhaustPile);
        snapshotSpecialCards = CloneCardList(cards.specialCards);
    }

    private List<CardData> CloneCardList(List<CardData> source)
    {
        List<CardData> result = new List<CardData>();
        if (source == null) return result;
        foreach (CardData card in source)
        {
            if (card == null) continue;
            result.Add(card.Clone());
        }
        return result;
    }

    private void CapturePositions()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            Transform playerRoot = playerObj.transform.root;
            snapshotPlayerPosition = playerRoot.position;
            snapshotPlayerRotation = playerRoot.rotation;
        }
    }

    private void CaptureEnemyStates()
    {
        snapshotMonsters.Clear();

        MonsterIdentityManager[] monsters =
            FindObjectsByType<MonsterIdentityManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (MonsterIdentityManager monster in monsters)
        {
            if (monster == null) continue;
            snapshotMonsters.Add(MonsterStateSnapshot.Capture(monster));
        }
    }

    public TurnStartSnapshotData ExportSnapshotData()
    {
        TurnStartSnapshotData data = new TurnStartSnapshotData();

        data.roundCount = snapshotRoundCount;
        data.hp = snapshotHP;
        data.block = snapshotBlock;
        data.energy = snapshotEnergy;
        data.actionPoint = snapshotActionPoint;
        data.usedActionPointCount = snapshotUsedActionPointCount;
        data.hand = CloneCardList(snapshotHand);
        data.drawPile = CloneCardList(snapshotDrawPile);
        data.discardPile = CloneCardList(snapshotDiscardPile);
        data.exhaustPile = CloneCardList(snapshotExhaustPile);
        data.specialCards = CloneCardList(snapshotSpecialCards);
        data.playerPosition = snapshotPlayerPosition;
        data.playerRotation = snapshotPlayerRotation;
        data.horizontal = snapshotHorizontal;
        data.vertical = snapshotVertical;
        data.monsters = CloneMonsterSnapshotList(snapshotMonsters);
        return data;
    }

    public void ImportSnapshotData(TurnStartSnapshotData data)
    {
        if (data == null) return;

        hasSnapshot = true;
        snapshotRoundCount = data.roundCount;
        snapshotHP = data.hp;
        snapshotBlock = data.block;
        snapshotEnergy = data.energy;
        snapshotActionPoint = data.actionPoint;
        snapshotUsedActionPointCount = data.usedActionPointCount;
        snapshotHand = CloneCardList(data.hand);
        snapshotDrawPile = CloneCardList(data.drawPile);
        snapshotDiscardPile = CloneCardList(data.discardPile);
        snapshotExhaustPile = CloneCardList(data.exhaustPile);
        snapshotSpecialCards = CloneCardList(data.specialCards);
        snapshotPlayerPosition = data.playerPosition;
        snapshotPlayerRotation = data.playerRotation;
        snapshotHorizontal = data.horizontal;
        snapshotVertical = data.vertical;
        snapshotMonsters = CloneMonsterSnapshotList(data.monsters);

        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.RestoreRoundCount(snapshotRoundCount);
        }
    }

    public void OnSLButtonPressed()
    {
        if (!hasSnapshot)
        {
            Debug.LogWarning("[SLManager] 当前没有可用的存档，无法读档。");
            return;
        }

        if (onlyAllowLoadDuringPlayerTurn &&
            TurnManager.Instance != null &&
            TurnManager.Instance.CurrentTurn != TurnState.Player)
        {
            Debug.LogWarning("[SLManager] 当前不是玩家回合，暂不允许读档。");
            return;
        }

        PlayerMoveController playerMove = FindFirstObjectByType<PlayerMoveController>();
        if (playerMove != null && playerMove.IsMoving)
        {
            Debug.LogWarning("[SLManager] 玩家正在移动中，无法进行 SL 重置操作！");
            return;
        }

        RestorePlayerStats();
        RestoreCardState();
        RestorePositions();
        RestoreEnemyStates();

        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.RestoreRoundCount(snapshotRoundCount);
        }

        if (logDebugInfo)
        {
            Debug.Log("[SLManager] 已恢复到回合开始时的状态。");
        }
    }

    private void RestorePlayerStats()
    {
        var stats = CombatStatsManager.Instance;
        if (stats == null) return;
        stats.currentHP = snapshotHP;
        stats.currentBlock = snapshotBlock;
        stats.currentEnergy = snapshotEnergy;
        stats.currentActionPoint = snapshotActionPoint;
        stats.RestoreUsedActionPointCount(snapshotUsedActionPointCount);
        stats.Horizontal = snapshotHorizontal;
        stats.Vertical = snapshotVertical;
        stats.RestorePlayerFacing();
        stats.TriggerStatsChanged();
    }

    private void RestoreCardState()
    {
        var cards = CardManager.Instance;
        if (cards == null) return;
        cards.RestoreDeckFromSnapshot(
            snapshotHand,
            snapshotDrawPile,
            snapshotDiscardPile,
            snapshotExhaustPile,
            snapshotSpecialCards);
    }

    private void RestorePositions()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            Transform playerRoot = playerObj.transform.root;
            playerRoot.position = snapshotPlayerPosition;
            playerRoot.rotation = snapshotPlayerRotation;
        }
    }

    private void RestoreEnemyStates()
    {
        MonsterIdentityManager[] currentMonsters =
            FindObjectsByType<MonsterIdentityManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        Dictionary<string, MonsterIdentityManager> currentById =
            new Dictionary<string, MonsterIdentityManager>();

        foreach (MonsterIdentityManager monster in currentMonsters)
        {
            if (monster == null || string.IsNullOrEmpty(monster.monsterId)) continue;
            currentById[monster.monsterId] = monster;
        }

        HashSet<MonsterIdentityManager> handledMonsters =
            new HashSet<MonsterIdentityManager>();

        foreach (MonsterStateSnapshot snapshot in snapshotMonsters)
        {
            if (snapshot == null || string.IsNullOrEmpty(snapshot.monsterId)) continue;

            if (currentById.TryGetValue(snapshot.monsterId, out MonsterIdentityManager monster))
            {
                snapshot.ApplyTo(monster);
                handledMonsters.Add(monster);
            }
            else
            {
                Debug.LogWarning($"[SLManager] 找不到怪物 {snapshot.monsterId}，无法恢复该怪物的回合开始状态。");
            }
        }

        foreach (MonsterIdentityManager monster in currentMonsters)
        {
            if (monster == null || handledMonsters.Contains(monster)) continue;

            // 本回合开始时不存在的怪物（例如之后新召唤的怪），直接移除。
            monster.gameObject.SetActive(false);
            Destroy(monster.gameObject);
        }
    }

    private List<MonsterStateSnapshot> CloneMonsterSnapshotList(List<MonsterStateSnapshot> source)
    {
        List<MonsterStateSnapshot> result = new List<MonsterStateSnapshot>();
        if (source == null) return result;

        foreach (MonsterStateSnapshot snapshot in source)
        {
            if (snapshot != null)
            {
                result.Add(snapshot.Clone());
            }
        }

        return result;
    }

    public void ClearSnapshot()
    {
        hasSnapshot = false;
        snapshotRoundCount = 0;
        snapshotUsedActionPointCount = 0;
        snapshotHorizontal = 0f;
        snapshotVertical = 0f;
        snapshotHand.Clear();
        snapshotDrawPile.Clear();
        snapshotDiscardPile.Clear();
        snapshotExhaustPile.Clear();
        snapshotSpecialCards.Clear();
        snapshotMonsters.Clear();
    }
}
