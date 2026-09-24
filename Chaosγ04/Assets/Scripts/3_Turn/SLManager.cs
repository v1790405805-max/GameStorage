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
    [SerializeField] private List<Transform> snapshotEnemyRefs = new List<Transform>();
    [SerializeField] private List<Vector3> snapshotEnemyPositions = new List<Vector3>();
    [SerializeField] private List<Quaternion> snapshotEnemyRotations = new List<Quaternion>();

    // 【新增】怪物完整数值与状态快照
    [SerializeField] private List<int> snapshotEnemyHPs = new List<int>();
    [SerializeField] private List<int> snapshotEnemyBlocks = new List<int>();
    [SerializeField] private List<bool> snapshotEnemyActiveStates = new List<bool>();

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
            Debug.Log($"[SLManager] 已记录回合快照 | 手牌数:{snapshotHand.Count} 抽牌堆:{snapshotDrawPile.Count}");
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
        snapshotEnemyRefs.Clear();
        snapshotEnemyPositions.Clear();
        snapshotEnemyRotations.Clear();
        snapshotEnemyHPs.Clear();
        snapshotEnemyBlocks.Clear();
        snapshotEnemyActiveStates.Clear();

        GameObject[] monsterObjs = GameObject.FindGameObjectsWithTag("Monster");
        foreach (GameObject monster in monsterObjs)
        {
            if (monster == null) continue;
            Transform root = monster.transform.root;
            if (snapshotEnemyRefs.Contains(root)) continue;

            snapshotEnemyRefs.Add(root);
            snapshotEnemyPositions.Add(root.position);
            snapshotEnemyRotations.Add(root.rotation);
            snapshotEnemyActiveStates.Add(root.gameObject.activeSelf);

            MonsterStats stats = root.GetComponentInChildren<MonsterStats>();
            if (stats != null)
            {
                snapshotEnemyHPs.Add(stats.currentHp);
                snapshotEnemyBlocks.Add(stats.currentBlock);
            }
            else
            {
                snapshotEnemyHPs.Add(0);
                snapshotEnemyBlocks.Add(0);
            }
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

        for (int i = 0; i < snapshotEnemyRefs.Count; i++)
        {
            Transform enemy = snapshotEnemyRefs[i];
            if (enemy == null) continue;
            MonsterIdentityManager identity = enemy.GetComponentInChildren<MonsterIdentityManager>();
            string id = identity != null ? identity.monsterId : enemy.name;
            data.enemyIds.Add(id);
            data.enemyPositions.Add(snapshotEnemyPositions[i]);
            data.enemyRotations.Add(snapshotEnemyRotations[i]);
        }
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

        snapshotEnemyRefs.Clear();
        snapshotEnemyPositions.Clear();
        snapshotEnemyRotations.Clear();
        snapshotEnemyHPs.Clear();
        snapshotEnemyBlocks.Clear();
        snapshotEnemyActiveStates.Clear();

        GameObject[] monsterObjs = GameObject.FindGameObjectsWithTag("Monster");
        foreach (GameObject monster in monsterObjs)
        {
            if (monster == null) continue;
            MonsterIdentityManager identity = monster.GetComponentInChildren<MonsterIdentityManager>();
            if (identity == null) continue;
            int idx = data.enemyIds.IndexOf(identity.monsterId);
            if (idx < 0) continue;

            Transform root = monster.transform.root;
            if (snapshotEnemyRefs.Contains(root)) continue;

            snapshotEnemyRefs.Add(root);
            snapshotEnemyPositions.Add(data.enemyPositions[idx]);
            snapshotEnemyRotations.Add(data.enemyRotations[idx]);
            snapshotEnemyActiveStates.Add(true);

            MonsterStats stats = root.GetComponentInChildren<MonsterStats>();
            if (stats != null)
            {
                snapshotEnemyHPs.Add(stats.currentHp);
                snapshotEnemyBlocks.Add(stats.currentBlock);
            }
            else
            {
                snapshotEnemyHPs.Add(0);
                snapshotEnemyBlocks.Add(0);
            }
        }

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
        for (int i = 0; i < snapshotEnemyRefs.Count; i++)
        {
            Transform enemy = snapshotEnemyRefs[i];
            if (enemy == null) continue;

            if (i < snapshotEnemyActiveStates.Count)
            {
                enemy.gameObject.SetActive(snapshotEnemyActiveStates[i]);
            }

            if (i < snapshotEnemyPositions.Count) enemy.position = snapshotEnemyPositions[i];
            if (i < snapshotEnemyRotations.Count) enemy.rotation = snapshotEnemyRotations[i];

            MonsterStats stats = enemy.GetComponentInChildren<MonsterStats>();
            if (stats != null)
            {
                if (i < snapshotEnemyHPs.Count) stats.currentHp = snapshotEnemyHPs[i];
                if (i < snapshotEnemyBlocks.Count) stats.currentBlock = snapshotEnemyBlocks[i];

                // 通知 UI 强制刷新
                MonsterInfoUI infoUI = enemy.GetComponentInChildren<MonsterInfoUI>();
                if (infoUI != null)
                {
                    infoUI.UpdateHpDisplay(stats.currentHp, stats.maxHp);
                    infoUI.HideDamagePreview();
                }
            }
        }
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
        snapshotEnemyRefs.Clear();
        snapshotEnemyPositions.Clear();
        snapshotEnemyRotations.Clear();
        snapshotEnemyHPs.Clear();
        snapshotEnemyBlocks.Clear();
        snapshotEnemyActiveStates.Clear();
    }
}
