using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CardDeckData", menuName = "Card Basic/CardDeckData")]
public class CardDeckData : ScriptableObject
{
    [Header("--- 初始卡组配置 ---")]
    [SerializeField] private List<CardData> initialDeck = new List<CardData>();

    [Header("--- 游戏全局卡池 (图鉴总库) ---")]
    [SerializeField] private List<CardData> allAvailableCards = new List<CardData>();

    public IReadOnlyList<CardData> InitialDeck => initialDeck;
    public IReadOnlyList<CardData> AllAvailableCards => allAvailableCards;
}