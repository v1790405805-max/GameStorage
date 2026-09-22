using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class CardManager : MonoBehaviour
{
    public static CardManager Instance;

    [Header("回合抽牌配置")]
    [Tooltip("每回合默认抽取的普通卡牌数量")]
    public int cardsDrawnEachTurn = 5;

    [Header("卡组数据资产配置")]
    [Tooltip("引用的 CardDeckData 资源（包含初始卡组和全卡池数据）")]
    public CardDeckData cardDeckData;

    [Header("实时卡组构成")]
    [Tooltip("实时显示当前所有牌堆（抽牌堆+手牌+弃牌堆+消耗堆）中的卡牌汇总分类与数量")]
    [SerializeField]
    private List<CardData> allCardsInGame = new List<CardData>();

    [Header("牌堆数据")]
    [Tooltip("手牌")]
    public List<CardData> hand = new List<CardData>();
    [Tooltip("抽牌堆")]
    public List<CardData> drawPile = new List<CardData>();
    [Tooltip("弃牌堆")]
    public List<CardData> discardPile = new List<CardData>();
    [Tooltip("消耗牌堆（本小局不可重复使用）")]
    public List<CardData> exhaustPile = new List<CardData>();
    [Tooltip("常驻特殊卡牌（每回合固定抽取，不进入抽牌弃牌循环）")]
    public List<CardData> specialCards = new List<CardData>();

    [Header("UI 引用")]
    public Transform handUIContainer;
    public GameObject cardPrefab;

    [Header("卡牌生成视觉控制")]
    [Tooltip("生成卡牌的统一缩放大小")]
    public float cardSpawnScale = 1f;
    [Tooltip("生成卡牌在手牌区内的局部高度偏移 (Y轴)")]
    public float cardHeightOffset = 0f;

    [Tooltip("玩家打出当前卡牌前的起始格（在位移效果执行前记录，供直线类效果读取）")]
    public Vector2Int playStartGrid = new Vector2Int(-1, -1);

    public event Action<int, int> OnPileCountChanged;

    private void Awake()
    {
        Instance = Instance == null ? this : Instance;
        InitializeDeck();
    }

    private void InitializeDeck()
    {
        drawPile.Clear();
        hand.Clear();
        discardPile.Clear();
        exhaustPile.Clear();
        specialCards.Clear();

        List<CardData> sourceDeck = null;

        if (RunDataManager.Instance != null && RunDataManager.Instance.playerGlobalDeck != null && RunDataManager.Instance.playerGlobalDeck.Count > 0)
        {
            sourceDeck = RunDataManager.Instance.playerGlobalDeck;
        }
        else if (cardDeckData != null)
        {
            sourceDeck = (List<CardData>)cardDeckData.InitialDeck;
        }

        if (sourceDeck == null || sourceDeck.Count == 0) return;

        foreach (CardData cardData in sourceDeck)
        {
            if (cardData == null) continue;
            CardData clonedCard = cardData.Clone();
            if (clonedCard.type == CardType.Special)
                specialCards.Add(clonedCard);
            else
                drawPile.Add(clonedCard);
        }

        Shuffle(drawPile);
        NotifyUIUpdate();
    }

    #region 卡牌增删与全卡池随机抽取
    public void AddCardToDrawPile(CardData card)
    {
        if (card == null) return;
        CardData newCard = card.Clone();
        drawPile.Add(newCard);
        NotifyUIUpdate();
    }

    public void AddCardToHand(CardData card)
    {
        if (card == null) return;
        CardData newCard = card.Clone();
        hand.Add(newCard);
        InstantiateCardUI(newCard);
        NotifyUIUpdate();
    }

    public bool RemoveCard(CardData card)
    {
        if (card == null) return false;

        CardData target = hand.Find(c => c == card || c.cardName == card.cardName);
        if (target != null)
        {
            hand.Remove(target);
            RefreshHandUI();
            NotifyUIUpdate();
            return true;
        }

        target = drawPile.Find(c => c == card || c.cardName == card.cardName);
        if (target != null)
        {
            drawPile.Remove(target);
            NotifyUIUpdate();
            return true;
        }

        target = discardPile.Find(c => c == card || c.cardName == card.cardName);
        if (target != null)
        {
            discardPile.Remove(target);
            NotifyUIUpdate();
            return true;
        }

        return false;
    }

    public List<CardData> GetRandomRewardCards(int count)
    {
        List<CardData> rewardCards = new List<CardData>();
        if (cardDeckData == null || cardDeckData.AllAvailableCards == null || cardDeckData.AllAvailableCards.Count == 0)
            return rewardCards;

        List<CardData> tempPool = new List<CardData>(cardDeckData.AllAvailableCards);
        int drawCount = Mathf.Min(count, tempPool.Count);

        for (int i = 0; i < drawCount; i++)
        {
            int randomIndex = Random.Range(0, tempPool.Count);
            rewardCards.Add(tempPool[randomIndex].Clone());
            tempPool.RemoveAt(randomIndex);
        }

        return rewardCards;
    }

    private void UpdateAllCardsInGamePreview()
    {
        allCardsInGame.Clear();
        allCardsInGame.AddRange(drawPile);
        allCardsInGame.AddRange(hand);
        allCardsInGame.AddRange(discardPile);
        allCardsInGame.AddRange(exhaustPile);
        allCardsInGame.AddRange(specialCards);
    }

    private void RefreshHandUI()
    {
        if (handUIContainer == null) return;
        foreach (Transform child in handUIContainer)
            Destroy(child.gameObject);

        foreach (CardData card in hand)
            InstantiateCardUI(card);
    }
    #endregion

    public void StartTurn()
    {
        if (hand.Count > 0) DiscardHand();

        foreach (CardData specialCard in specialCards)
        {
            CardData cardForHand = specialCard.Clone();
            hand.Add(cardForHand);
            InstantiateCardUI(cardForHand);
        }

        DrawCards(cardsDrawnEachTurn);
    }

    public void DrawCards(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (drawPile.Count == 0)
            {
                if (discardPile.Count == 0) break;
                drawPile.AddRange(discardPile);
                discardPile.Clear();
                Shuffle(drawPile);
            }

            CardData drawnCard = drawPile[0];
            drawPile.RemoveAt(0);
            hand.Add(drawnCard);
            InstantiateCardUI(drawnCard);
        }
        NotifyUIUpdate();
    }

    private void InstantiateCardUI(CardData data)
    {
        if (cardPrefab == null || handUIContainer == null) return;
        GameObject cardObj = Instantiate(cardPrefab, handUIContainer);

        RectTransform rect = cardObj.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.localScale = new Vector3(cardSpawnScale, cardSpawnScale, 1f);
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, cardHeightOffset);
        }

        CardUI cardUI = cardObj.GetComponent<CardUI>();
        if (cardUI != null) cardUI.Setup(data);
    }

    /// <summary>
    /// 出牌逻辑与复合效果结算
    /// </summary>
    public void PlayCard(CardData card, GameObject cardUIObj, Vector2Int targetGrid)
    {
        if (CombatStatsManager.Instance != null && CombatStatsManager.Instance.ConsumeEnergy(card.cost))
        {
            playStartGrid = GetPlayerGridPosition();
            bool hasExtraEffects = card.extraEffects != null && card.extraEffects.Count > 0;

            // 卡牌表现由动画和特效两个大类分别管理。
            CardAnimationCore.PlayAll(card, targetGrid);
            CardVFXCore.PlayAll(card, targetGrid);

            // 1. 基础移动效果
            if (card.effectFlags.HasFlag(CardEffectType.Movement) && !hasExtraEffects)
            {
                PlayerMoveController moveController = FindFirstObjectByType<PlayerMoveController>();
                if (moveController != null)
                {
                    moveController.MoveToTargetGridByCard(targetGrid);
                }
            }

            // 2. 攻击效果
            if (card.effectFlags.HasFlag(CardEffectType.Attack))
            {
                GridManager gridMgr = FindFirstObjectByType<GridManager>();
                if (gridMgr != null && MonsterIdentitySystem.Instance != null)
                {
                    foreach (var monster in MonsterIdentitySystem.Instance.GetAllMonsters())
                    {
                        var (mx, mz) = gridMgr.GetGridPosition(monster.transform.position);
                        if (mx == targetGrid.x && mz == targetGrid.y)
                        {
                            MonsterStats stats = monster.GetComponent<MonsterStats>();
                            if (stats != null) stats.TakeDamage(card.damage);
                            break;
                        }
                    }
                }
            }

            // 3. 防御/护甲效果
            if (card.effectFlags.HasFlag(CardEffectType.Defense) && card.block != 0)
            {
                CombatStatsManager.Instance.AddBlock(card.block);
            }

            // 4. 生命值变化 (例如：回血技能)
            if (card.effectFlags.HasFlag(CardEffectType.Health) && card.healthChange != 0)
            {
                if (card.healthChange > 0)
                {
                    CombatStatsManager.Instance.Heal(card.healthChange);
                }
                else
                {
                    CombatStatsManager.Instance.TakeSelfDamage(-card.healthChange);
                }
            }

            // 5. 能量变化
            if (card.effectFlags.HasFlag(CardEffectType.Energy) && card.energyChange != 0)
            {
                CombatStatsManager.Instance.ModifyEnergy(card.energyChange, allowExceedMax: true);
            }

            // 6. 行动力变化
            if (card.effectFlags.HasFlag(CardEffectType.ActionPoint) && card.actionPointChange != 0)
            {
                CombatStatsManager.Instance.ModifyActionPoint(card.actionPointChange, allowExceedMax: true);
            }

            // 7. 抽牌效果
            if (card.effectFlags.HasFlag(CardEffectType.DrawCard) && card.drawAmount > 0)
            {
                DrawCards(card.drawAmount);
            }

            // 8. 弃牌效果
            if (card.effectFlags.HasFlag(CardEffectType.DiscardCard) && card.discardAmount > 0)
            {
                Debug.Log($"[卡牌效果] 需要手动或随机弃牌 {card.discardAmount} 张");
            }

            // 9. 额外效果
            if (hasExtraEffects)
            {
                foreach (var extra in card.extraEffects)
                {
                    if (string.IsNullOrEmpty(extra.effectTypeName)) continue;
                    System.Type effectType = System.Type.GetType(extra.effectTypeName);
                    if (effectType == null || !typeof(CardEffectCore).IsAssignableFrom(effectType)) continue;

                    CardEffectCore instance = ScriptableObject.CreateInstance(effectType) as CardEffectCore;
                    if (instance == null) continue;

                    bool success = instance.Execute(card, targetGrid);
                    Destroy(instance);

                    if (!success)
                    {
                        Debug.LogWarning($"[CardManager] 卡牌 [{card.cardName}] 的额外效果 [{extra.effectTypeName}] 执行失败。");
                    }
                }
            }

            // 从手牌数据中移除
            hand.Remove(card);

            if (card.type == CardType.Special)
            {
                Debug.Log($"{card.cardName} 是 Special 卡牌，用完即销毁。");
            }
            else if (card.type == CardType.Power)
            {
                exhaustPile.Add(card);
            }
            else
            {
                discardPile.Add(card);
            }

            Destroy(cardUIObj);
            if (CombatStatsManager.Instance != null) CombatStatsManager.Instance.TriggerStatsChanged();
            NotifyUIUpdate();
        }
    }

    public void DiscardHand()
    {
        foreach (CardData card in hand)
        {
            if (card.type != CardType.Special) discardPile.Add(card);
        }

        hand.Clear();
        if (handUIContainer != null)
        {
            foreach (Transform child in handUIContainer) Destroy(child.gameObject);
        }

        if (CombatStatsManager.Instance != null) CombatStatsManager.Instance.TriggerStatsChanged();
        NotifyUIUpdate();
    }

    private Vector2Int GetPlayerGridPosition()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        GridManager gridMgr = FindFirstObjectByType<GridManager>();
        if (playerObj == null || gridMgr == null) return new Vector2Int(-1, -1);

        gridMgr.EnsureGridSystemInitialized();
        Vector3 logicPos = playerObj.transform.position - gridMgr.cellOffset;
        var (px, pz) = gridMgr.GetGridPosition(logicPos);
        return new Vector2Int(px, pz);
    }

    private void Shuffle(List<CardData> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            CardData temp = list[i];
            int randomIndex = Random.Range(i, list.Count);
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }

    private void NotifyUIUpdate()
    {
        UpdateAllCardsInGamePreview();
        OnPileCountChanged?.Invoke(drawPile.Count, discardPile.Count);
    }

    public void RestoreDeckFromSave(List<CardData> savedHand, List<CardData> savedDraw, List<CardData> savedDiscard, List<CardData> savedExhaust)
    {
        hand.Clear();
        drawPile.Clear();
        discardPile.Clear();
        exhaustPile.Clear();

        if (handUIContainer != null)
        {
            foreach (Transform child in handUIContainer) Destroy(child.gameObject);
        }

        foreach (var c in savedDraw) drawPile.Add(c.Clone());
        foreach (var c in savedDiscard) discardPile.Add(c.Clone());
        foreach (var c in savedExhaust) exhaustPile.Add(c.Clone());

        foreach (var c in savedHand)
        {
            CardData cloned = c.Clone();
            hand.Add(cloned);
            InstantiateCardUI(cloned);
        }

        NotifyUIUpdate();
    }
}
