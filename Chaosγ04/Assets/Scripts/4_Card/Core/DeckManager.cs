using UnityEngine;
using System.Collections.Generic;

public class DeckManager : MonoBehaviour
{
    public static DeckManager Instance;

    [Header("--- 玩家真实卡组 (跨场景存活) ---")]
    [Tooltip("玩家当前的背包，每一张牌独立占据一个位置")]
    public List<CardData> playerMasterDeck = new List<CardData>();

    [Header("--- 游戏全局卡池 (图鉴总库) ---")]
    [Tooltip("游戏里所有的卡牌资产，用于战斗胜利后的随机掉落或奖励抽选")]
    public List<CardData> allAvailableCards = new List<CardData>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 跨场景不销毁
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 获得一张新卡牌加入主卡组（奖励、商店购买等）
    /// </summary>
    public void AddCardToMasterDeck(CardData card)
    {
        if (card != null)
        {
            playerMasterDeck.Add(card);
            Debug.Log($"【牌库】新增卡牌：{card.cardName}");
        }
    }

    /// <summary>
    /// 从主卡组移除一张卡牌（删牌节点、事件移除等）
    /// </summary>
    public void RemoveCardFromMasterDeck(CardData card)
    {
        if (playerMasterDeck.Contains(card))
        {
            playerMasterDeck.Remove(card);
            Debug.Log($"【牌库】移除卡牌：{card.cardName}");
        }
    }

    /// <summary>
    /// 从全卡池中不重复地随机抽取指定数量的卡牌（用于战斗胜利选牌奖励）
    /// </summary>
    public List<CardData> GetRandomRewardCards(int count)
    {
        List<CardData> rewardCards = new List<CardData>();
        if (allAvailableCards == null || allAvailableCards.Count == 0) return rewardCards;

        List<CardData> tempPool = new List<CardData>(allAvailableCards);
        int drawCount = Mathf.Min(count, tempPool.Count);

        for (int i = 0; i < drawCount; i++)
        {
            int randomIndex = Random.Range(0, tempPool.Count);
            rewardCards.Add(tempPool[randomIndex]);
            tempPool.RemoveAt(randomIndex); // 移除已抽出的卡，避免奖励界面重复
        }

        return rewardCards;
    }
}