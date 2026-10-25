using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 单局游戏管理器 (RunDataManager)：
/// 专门负责整局游戏（Run）跨战斗继承的宏观资产（如全局卡组、货币、生命值、遗物、大地图进度等）。
/// </summary>
public class RunDataManager : MonoBehaviour
{
    public static RunDataManager Instance { get; private set; }

    [Header("--- 初始卡组配置 ---")]
    [Tooltip("请将你的初始卡组资产（CardDeckData）直接拖到这里")]
    public CardDeckData initialDeckConfig;

    [Header("--- 整局游戏继承资产 (Run 级) ---")]
    [Tooltip("玩家本次游戏拥有的总卡组（动态变化）")]
    public List<CardData> playerGlobalDeck = new List<CardData>();

    [Tooltip("开局初始钞票/晶石数量（在 Inspector 修改此处即可生效）")]
    public int initialCurrency = 66;

    [Tooltip("玩家当前持有的钞票/晶石数量（运行时动态变化）")]
    public int currency = 66;

    [Tooltip("玩家跨场景当前生命值")]
    public int playerCurrentHP = 50;

    [Tooltip("玩家跨场景最大生命值")]
    public int playerMaxHP = 50;

    // 货币与血量变化事件（供 TopBar UI 实时监听刷新）
    public event Action<int> OnCurrencyChanged;
    public event Action<int, int> OnPlayerHPChanged;

    [Header("--- 大地图进度追踪 ---")]
    [Tooltip("整局游戏唯一的地图图纸缓存")]
    public MapData savedMapData;

    [Tooltip("玩家当前所在的层级。-1代表还没出发。")]
    public int currentFloor = -1;

    [Tooltip("玩家当前所在的节点类型（用于战斗结算判断）")]
    public MapNodeType currentNodeType;

    [Tooltip("是否刚打赢 Boss，等待在大地图弹出通关结算")]
    public bool pendingVictorySummary = false;

    [Tooltip("玩家目前可以点击的下一个节点的列表 (存储节点的名字)")]
    public List<string> availableNextNodeNames = new List<string>();

    [Tooltip("用于记录玩家曾经走过的所有节点的名字")]
    public List<string> visitedNodeNames = new List<string>();

    [Header("--- 游戏通关统计数据 ---")]
    public int totalKills = 0;
    public int totalDamageDealt = 0;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 【核心修复】：启动时以配置的 initialCurrency 为准，杜绝被序列化的旧值覆盖
            currency = initialCurrency;
            playerCurrentHP = playerMaxHP;

            if (playerGlobalDeck == null || playerGlobalDeck.Count == 0)
            {
                InitializeRunDeck();
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 编辑器未运行状态下，修改 initialCurrency 自动同步给 currency，避免面板数据不一致
        if (!Application.isPlaying)
        {
            currency = initialCurrency;
        }
    }
#endif

    public void InitializeRunDeck()
    {
        playerGlobalDeck.Clear();

        if (initialDeckConfig != null && initialDeckConfig.InitialDeck != null)
        {
            foreach (var card in initialDeckConfig.InitialDeck)
            {
                if (card != null)
                {
                    playerGlobalDeck.Add(card.Clone());
                }
            }
        }
    }

    // ==================== 卡组增删接口 ====================

    public void AddCardToGlobalDeck(CardData newCard)
    {
        if (newCard != null)
        {
            playerGlobalDeck.Add(newCard.Clone());
        }
    }

    /// <summary>
    /// 从全局卡组中移除一张指定卡牌（商店删卡 / 事件献祭用）
    /// </summary>
    public bool RemoveCardFromGlobalDeck(CardData targetCard)
    {
        if (targetCard == null || playerGlobalDeck == null) return false;

        // 优先按实例匹配，其次按 cardID 匹配
        if (playerGlobalDeck.Remove(targetCard))
        {
            return true;
        }

        int index = playerGlobalDeck.FindIndex(c => c != null && c.cardID == targetCard.cardID);
        if (index >= 0)
        {
            playerGlobalDeck.RemoveAt(index);
            return true;
        }

        return false;
    }

    // ==================== 货币与血量接口 ====================

    public bool CanAfford(int cost) => currency >= cost;

    public bool TrySpendCurrency(int cost)
    {
        if (cost < 0 || currency < cost) return false;
        currency -= cost;
        OnCurrencyChanged?.Invoke(currency);
        return true;
    }

    public void AddCurrency(int amount)
    {
        currency = Mathf.Max(0, currency + amount);
        OnCurrencyChanged?.Invoke(currency);
    }

    public void ModifyPlayerHP(int delta)
    {
        playerCurrentHP = Mathf.Clamp(playerCurrentHP + delta, 1, playerMaxHP);
        OnPlayerHPChanged?.Invoke(playerCurrentHP, playerMaxHP);
    }

    // ==================== 地图与重置接口 ====================

    public void SaveAvailableNextNodes(List<MapNode> nextNodes)
    {
        availableNextNodeNames.Clear();
        foreach (var node in nextNodes)
        {
            if (node != null)
            {
                availableNextNodeNames.Add(node.gameObject.name);
            }
        }
    }

    public void AddVisitedNode(string nodeName)
    {
        if (!visitedNodeNames.Contains(nodeName))
        {
            visitedNodeNames.Add(nodeName);
        }
    }

    public void ResetRunData()
    {
        InitializeRunDeck();
        currency = initialCurrency;
        playerCurrentHP = playerMaxHP;
        OnCurrencyChanged?.Invoke(currency);
        OnPlayerHPChanged?.Invoke(playerCurrentHP, playerMaxHP);

        savedMapData = null;
        currentFloor = -1;
        currentNodeType = MapNodeType.Event;
        pendingVictorySummary = false;

        availableNextNodeNames.Clear();
        visitedNodeNames.Clear();

        totalKills = 0;
        totalDamageDealt = 0;

        Debug.Log("【RunDataManager】全局大地图与卡组数据已重置。");
    }
}