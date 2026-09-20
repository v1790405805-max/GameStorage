using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 单局游戏管理器 (RunDataManager)：
/// 专门负责整局游戏（Run）跨战斗继承的宏观资产（如全局卡组、遗物、大地图进度等）。
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

    // ==========================================
    // 【核心新增】真实通关统计数据
    // ==========================================
    [Header("--- 游戏通关统计数据 ---")]
    public int totalKills = 0;
    public int totalDamageDealt = 0;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

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

    public void AddCardToGlobalDeck(CardData newCard)
    {
        if (newCard != null)
        {
            playerGlobalDeck.Add(newCard.Clone());
        }
    }

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
        savedMapData = null;
        currentFloor = -1;
        currentNodeType = MapNodeType.Event;
        pendingVictorySummary = false;

        availableNextNodeNames.Clear();
        visitedNodeNames.Clear();

        // 【新增】清空统计数据
        totalKills = 0;
        totalDamageDealt = 0;

        Debug.Log("【RunDataManager】全局大地图与卡组数据已重置。");
    }
}