using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;

/// <summary>
/// 商店单个卡牌商品槽位
/// </summary>
[System.Serializable]
public class ShopCardSlot
{
    [Tooltip("挂载生成的 Card Prefab 的父节点位置")]
    public Transform cardAnchor;

    [Tooltip("购买该卡牌的按钮")]
    public Button buyButton;

    [Tooltip("价格前面的货币小图标 Image/GameObject（售罄后会自动隐藏）")]
    public GameObject priceIcon;

    [Tooltip("只显示纯数字价格的文本（售罄时显示'已售罄'）")]
    public TextMeshProUGUI priceText;

    [HideInInspector] public CardData assignedCard;
    [HideInInspector] public int basePrice;
    [HideInInspector] public int finalPrice;
    [HideInInspector] public bool isDiscounted;
    [HideInInspector] public bool isSold;
    [HideInInspector] public GameObject spawnedCardObj;
    [HideInInspector] public Vector3 originalSlotScale = Vector3.one;
}

/// <summary>
/// 商店场景总控制器 (3_Shop_Scene)：
/// 货币实时动态读取、无硬编码覆盖、组件无拉扯变形。
/// </summary>
public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance { get; private set; }

    [Header("--- 1. 预制体与卡池配置 ---")]
    [Tooltip("拖入 Project 中的 Card Prefab 预制体")]
    [SerializeField] private GameObject cardPrefab;

    [Tooltip("备用卡池配置（若 RunDataManager 未挂初始卡池则使用此项）")]
    [SerializeField] private CardDeckData fallbackDeckData;

    [Tooltip("可选：直接手动拖入可售卖的卡牌池")]
    [SerializeField] private List<CardData> customShopPool = new List<CardData>();

    [Header("--- 2. 货架槽位与 UI 绑定 ---")]
    [Tooltip("商店里的卡牌商品槽位列表")]
    [SerializeField] private List<ShopCardSlot> cardSlots = new List<ShopCardSlot>();

    [Tooltip("生成的货架卡牌相对于 Slot 中心的 Y 轴向上偏移量")]
    [SerializeField] private float cardOffsetY = 45f;

    [Tooltip("右上角显示玩家当前钞票数量的文本")]
    [SerializeField] private TextMeshProUGUI currencyText;

    [Tooltip("离开商店返回大地图的按钮")]
    [SerializeField] private Button leaveShopButton;

    [Tooltip("大地图场景名称")]
    [SerializeField] private string mapSceneName = "1_Map_Scene";

    [Header("--- 3. 悬停放大参数 ---")]
    [SerializeField] private float hoverScaleMultiplier = 1.15f;
    [SerializeField] private float hoverTweenDuration = 0.2f;

    [Header("--- 4. 价格与打折色彩配置 ---")]
    [SerializeField] private int minBasePrice = 35;
    [SerializeField] private int maxBasePrice = 60;
    [Range(0f, 1f)]
    [SerializeField] private float discountChance = 0.35f;
    [SerializeField] private float discountRate = 0.65f;

    [Header("价格纯数字颜色配置")]
    [SerializeField] private Color normalAffordableColor = Color.white;
    [SerializeField] private Color unaffordableColor = new Color(1f, 0.3f, 0.3f, 1f);
    [SerializeField] private Color discountColor = new Color(0.2f, 1f, 0.45f, 1f);

    [Header("--- 5. 删牌服务配置 ---")]
    [SerializeField] private Button removeCardServiceButton;
    [SerializeField] private GameObject removeCardPriceIcon;
    [SerializeField] private TextMeshProUGUI removeCardPriceText;
    [SerializeField] private int removeCardCost = 50;

    [Header("--- 6. 删牌弹窗参数解耦配置 ---")]
    [Tooltip("点选删牌全屏蒙版面板")]
    [SerializeField] private GameObject removeCardPanel;
    [Tooltip("装载删牌卡牌的 Content 节点")]
    [SerializeField] private Transform removeCardContentParent;
    [Tooltip("取消删牌按钮")]
    [SerializeField] private Button cancelRemoveButton;
    [Tooltip("删牌弹窗顶部标题提示文本")]
    [SerializeField] private TextMeshProUGUI removePanelTitleText;

    [Tooltip("删牌弹窗内卡牌的显示缩放倍率（推荐 1.1 ~ 1.25）")]
    [Range(0.5f, 2.0f)]
    [SerializeField] private float removeCardDisplayScale = 1.15f;

    private bool hasUsedRemoveService = false;
    private Vector3 originalRemoveBtnScale = Vector3.one;
    private readonly List<GameObject> spawnedRemoveCardObjects = new List<GameObject>();

    private void Awake()
    {
        Instance = this;
        AutoBindSlotsIfNeeded();
    }

    private void Start()
    {
        if (RunDataManager.Instance != null)
        {
            RunDataManager.Instance.OnCurrencyChanged += UpdateCurrencyUI;
            UpdateCurrencyUI(RunDataManager.Instance.currency);
        }

        if (leaveShopButton != null)
        {
            leaveShopButton.onClick.AddListener(OnLeaveShopClicked);
        }

        if (removeCardServiceButton != null)
        {
            removeCardServiceButton.onClick.RemoveAllListeners();
            removeCardServiceButton.onClick.AddListener(OpenRemoveCardSelection);
            originalRemoveBtnScale = removeCardServiceButton.transform.localScale;
            BindStandaloneHover(removeCardServiceButton.gameObject, originalRemoveBtnScale);
        }

        if (cancelRemoveButton != null)
        {
            cancelRemoveButton.onClick.RemoveAllListeners();
            cancelRemoveButton.onClick.AddListener(CloseRemoveCardSelection);

            EventTrigger cancelTrigger = cancelRemoveButton.GetComponent<EventTrigger>();
            if (cancelTrigger == null) cancelTrigger = cancelRemoveButton.gameObject.AddComponent<EventTrigger>();
            cancelTrigger.triggers.Clear();

            EventTrigger.Entry clickEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            clickEntry.callback.AddListener((data) => { CloseRemoveCardSelection(); });
            cancelTrigger.triggers.Add(clickEntry);
        }

        if (removeCardPanel != null)
        {
            removeCardPanel.SetActive(false);
        }

        GenerateShopInventory();
        RefreshShopInteractiveState();
    }

    private void OnDestroy()
    {
        if (RunDataManager.Instance != null)
        {
            RunDataManager.Instance.OnCurrencyChanged -= UpdateCurrencyUI;
        }
    }

    [ContextMenu("自动绑定子物体槽位 (Auto Bind Slots)")]
    public void AutoBindSlotsIfNeeded()
    {
        if (cardSlots != null && cardSlots.Count > 0)
        {
            foreach (var slot in cardSlots)
            {
                if (slot.cardAnchor != null) slot.originalSlotScale = slot.cardAnchor.localScale;
            }
            return;
        }

        cardSlots = new List<ShopCardSlot>();
        foreach (Transform child in transform)
        {
            if (!child.gameObject.activeInHierarchy || child.name.Contains("Exit") || child.name.Contains("Remove"))
                continue;

            Button btn = child.GetComponentInChildren<Button>(true);
            if (btn != null && btn != leaveShopButton && btn != removeCardServiceButton)
            {
                Image iconImg = null;
                Image[] childImgs = btn.GetComponentsInChildren<Image>(true);
                foreach (var img in childImgs)
                {
                    if (img.gameObject != btn.gameObject)
                    {
                        iconImg = img;
                        break;
                    }
                }

                ShopCardSlot newSlot = new ShopCardSlot
                {
                    cardAnchor = child,
                    buyButton = btn,
                    priceIcon = (iconImg != null) ? iconImg.gameObject : null,
                    priceText = btn.GetComponentInChildren<TextMeshProUGUI>(true),
                    originalSlotScale = child.localScale
                };
                cardSlots.Add(newSlot);
            }
        }
    }

    public void GenerateShopInventory()
    {
        List<CardData> pool = GetAvailableCardPool();
        if (pool.Count == 0 || cardPrefab == null) return;

        List<CardData> candidates = new List<CardData>(pool);
        for (int i = 0; i < candidates.Count; i++)
        {
            int rand = Random.Range(i, candidates.Count);
            (candidates[i], candidates[rand]) = (candidates[rand], candidates[i]);
        }

        for (int i = 0; i < cardSlots.Count; i++)
        {
            ShopCardSlot slot = cardSlots[i];
            if (slot.cardAnchor == null) continue;

            slot.originalSlotScale = slot.cardAnchor.localScale;

            if (slot.spawnedCardObj != null) Destroy(slot.spawnedCardObj);

            if (i >= candidates.Count)
            {
                if (slot.buyButton != null) slot.buyButton.gameObject.SetActive(false);
                if (slot.priceIcon != null) slot.priceIcon.SetActive(false);
                continue;
            }

            CardData data = candidates[i];
            slot.assignedCard = data;
            slot.basePrice = Random.Range(minBasePrice, maxBasePrice + 1);

            slot.isDiscounted = (Random.value < discountChance);
            slot.finalPrice = slot.isDiscounted ? Mathf.RoundToInt(slot.basePrice * discountRate) : slot.basePrice;
            slot.isSold = false;

            if (slot.priceIcon != null) slot.priceIcon.SetActive(true);

            GameObject cardObj = Instantiate(cardPrefab, slot.cardAnchor);
            cardObj.transform.localPosition = new Vector3(0f, cardOffsetY, 0f);
            cardObj.transform.localRotation = Quaternion.identity;
            cardObj.transform.localScale = Vector3.one;
            cardObj.transform.SetAsFirstSibling();
            slot.spawnedCardObj = cardObj;

            CardUI cardUI = cardObj.GetComponent<CardUI>();
            if (cardUI != null)
            {
                cardUI.isDisplayOnly = true;
                cardUI.Setup(data);
            }

            int slotIndex = i;

            if (slot.buyButton != null)
            {
                slot.buyButton.gameObject.SetActive(true);
                slot.buyButton.onClick.RemoveAllListeners();
                slot.buyButton.onClick.AddListener(() => TryBuyCard(slotIndex));
                BindHoverEvents(slot, slot.buyButton.gameObject);
            }

            BindDirectClick(cardObj, () => TryBuyCard(slotIndex));
            BindHoverEvents(slot, cardObj);
        }
    }

    private void BindHoverEvents(ShopCardSlot slot, GameObject target)
    {
        if (target == null || slot.cardAnchor == null) return;

        EventTrigger trigger = target.GetComponent<EventTrigger>();
        if (trigger == null) trigger = target.AddComponent<EventTrigger>();

        EventTrigger.Entry enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener((data) =>
        {
            if (slot.isSold) return;
            slot.cardAnchor.DOKill();
            slot.cardAnchor.SetAsLastSibling();
            slot.cardAnchor.DOScale(slot.originalSlotScale * hoverScaleMultiplier, hoverTweenDuration).SetEase(Ease.OutBack);
        });
        trigger.triggers.Add(enter);

        EventTrigger.Entry exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener((data) =>
        {
            slot.cardAnchor.DOKill();
            slot.cardAnchor.DOScale(slot.originalSlotScale, hoverTweenDuration).SetEase(Ease.OutCubic);
        });
        trigger.triggers.Add(exit);
    }

    private void BindStandaloneHover(GameObject target, Vector3 normalScale)
    {
        if (target == null) return;

        EventTrigger trigger = target.GetComponent<EventTrigger>();
        if (trigger == null) trigger = target.AddComponent<EventTrigger>();
        trigger.triggers.Clear();

        EventTrigger.Entry enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener((data) =>
        {
            if (hasUsedRemoveService) return;
            if (removeCardPanel != null && removeCardPanel.activeSelf) return;

            target.transform.DOKill();
            target.transform.DOScale(normalScale * hoverScaleMultiplier, hoverTweenDuration).SetEase(Ease.OutBack);
        });
        trigger.triggers.Add(enter);

        EventTrigger.Entry exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener((data) =>
        {
            target.transform.DOKill();
            target.transform.DOScale(normalScale, hoverTweenDuration).SetEase(Ease.OutCubic);
        });
        trigger.triggers.Add(exit);
    }

    private void BindDirectClick(GameObject target, System.Action onClickAction)
    {
        if (target == null || onClickAction == null) return;

        EventTrigger trigger = target.GetComponent<EventTrigger>();
        if (trigger == null) trigger = target.AddComponent<EventTrigger>();

        EventTrigger.Entry click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
        click.callback.AddListener((data) => { onClickAction.Invoke(); });
        trigger.triggers.Add(click);
    }

    private void TryBuyCard(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= cardSlots.Count) return;
        ShopCardSlot slot = cardSlots[slotIndex];

        if (slot.isSold || slot.assignedCard == null || RunDataManager.Instance == null) return;

        if (RunDataManager.Instance.TrySpendCurrency(slot.finalPrice))
        {
            RunDataManager.Instance.AddCardToGlobalDeck(slot.assignedCard);
            slot.isSold = true;

            if (slot.spawnedCardObj != null)
            {
                Destroy(slot.spawnedCardObj);
            }
            if (slot.cardAnchor != null)
            {
                slot.cardAnchor.DOKill();
                slot.cardAnchor.localScale = slot.originalSlotScale;
            }

            Debug.Log($"[ShopManager] 购买成功：{slot.assignedCard.cardName}，花费：{slot.finalPrice}");
            RefreshShopInteractiveState();
        }
    }

    public void RefreshShopInteractiveState()
    {
        // 动态读取真实货币，不设假默认值
        int currentMoney = (RunDataManager.Instance != null) ? RunDataManager.Instance.currency : 0;

        foreach (var slot in cardSlots)
        {
            if (slot.buyButton == null) continue;

            if (slot.isSold)
            {
                slot.buyButton.interactable = false;

                if (slot.priceIcon != null)
                {
                    slot.priceIcon.SetActive(false);
                }

                if (slot.priceText != null)
                {
                    slot.priceText.text = "已售罄";
                    slot.priceText.color = Color.gray;
                }
            }
            else
            {
                bool canAfford = currentMoney >= slot.finalPrice;
                slot.buyButton.interactable = canAfford;

                if (slot.priceIcon != null)
                {
                    slot.priceIcon.SetActive(true);
                }

                if (slot.priceText != null)
                {
                    slot.priceText.text = slot.finalPrice.ToString();

                    if (!canAfford)
                    {
                        slot.priceText.color = unaffordableColor;
                    }
                    else if (slot.isDiscounted)
                    {
                        slot.priceText.color = discountColor;
                    }
                    else
                    {
                        slot.priceText.color = normalAffordableColor;
                    }
                }
            }
        }

        if (removeCardServiceButton != null)
        {
            if (hasUsedRemoveService)
            {
                removeCardServiceButton.interactable = false;
                if (removeCardPriceIcon != null) removeCardPriceIcon.SetActive(false);
                if (removeCardPriceText != null)
                {
                    removeCardPriceText.text = "已使用";
                    removeCardPriceText.color = Color.gray;
                }
            }
            else
            {
                bool canAffordRemove = currentMoney >= removeCardCost;
                removeCardServiceButton.interactable = canAffordRemove;
                if (removeCardPriceIcon != null) removeCardPriceIcon.SetActive(true);

                if (removeCardPriceText != null)
                {
                    removeCardPriceText.text = removeCardCost.ToString();
                    removeCardPriceText.color = canAffordRemove ? normalAffordableColor : unaffordableColor;
                }
            }
        }
    }

    // ==================== 删牌交互 ====================

    private void OpenRemoveCardSelection()
    {
        if (hasUsedRemoveService || RunDataManager.Instance == null) return;

        if (RunDataManager.Instance.currency < removeCardCost)
        {
            Debug.Log("[ShopManager] 钱不够，无法开启删牌服务！");
            return;
        }

        if (RunDataManager.Instance.playerGlobalDeck.Count <= 1)
        {
            Debug.LogWarning("[ShopManager] 卡组仅存 1 张牌，不可继续删除！");
            return;
        }

        if (removeCardServiceButton != null)
        {
            removeCardServiceButton.transform.DOKill();
            removeCardServiceButton.transform.localScale = originalRemoveBtnScale;
        }

        if (removeCardPanel != null)
        {
            removeCardPanel.transform.SetAsLastSibling();
            Vector3 pos = removeCardPanel.transform.localPosition;
            removeCardPanel.transform.localPosition = new Vector3(pos.x, pos.y, 0f);

            removeCardPanel.SetActive(true);

            if (cancelRemoveButton != null)
            {
                cancelRemoveButton.transform.SetAsLastSibling();
                Vector3 cPos = cancelRemoveButton.transform.localPosition;
                cancelRemoveButton.transform.localPosition = new Vector3(cPos.x, cPos.y, 0f);
            }

            PopulateDeckForRemoval();
        }
    }

    private void PopulateDeckForRemoval()
    {
        foreach (var obj in spawnedRemoveCardObjects)
        {
            if (obj != null) Destroy(obj);
        }
        spawnedRemoveCardObjects.Clear();

        if (removePanelTitleText != null)
        {
            removePanelTitleText.text = $"请点选一张要遗忘的卡牌 (消耗 {removeCardCost})";
        }

        Transform parent = removeCardContentParent != null ? removeCardContentParent : removeCardPanel.transform;
        List<CardData> currentDeck = RunDataManager.Instance.playerGlobalDeck;

        Vector2 originalPrefabSize = new Vector2(180f, 300f);
        RectTransform pRect = cardPrefab != null ? cardPrefab.GetComponent<RectTransform>() : null;
        if (pRect != null)
        {
            originalPrefabSize = pRect.rect.size;
        }

        for (int i = 0; i < currentDeck.Count; i++)
        {
            CardData cardData = currentDeck[i];
            if (cardData == null) continue;

            GameObject slotObj = new GameObject($"RemoveCardSlot_{i}", typeof(RectTransform));
            slotObj.transform.SetParent(parent, false);

            RectTransform slotRect = slotObj.GetComponent<RectTransform>();
            slotRect.sizeDelta = originalPrefabSize * removeCardDisplayScale;

            LayoutElement le = slotObj.AddComponent<LayoutElement>();
            le.preferredWidth = originalPrefabSize.x * removeCardDisplayScale;
            le.preferredHeight = originalPrefabSize.y * removeCardDisplayScale;

            GameObject cardObj = Instantiate(cardPrefab, slotObj.transform);
            RectTransform cardRect = cardObj.GetComponent<RectTransform>();
            if (cardRect != null)
            {
                cardRect.anchorMin = new Vector2(0.5f, 0.5f);
                cardRect.anchorMax = new Vector2(0.5f, 0.5f);
                cardRect.pivot = new Vector2(0.5f, 0.5f);
                cardRect.anchoredPosition = Vector2.zero;
                cardRect.sizeDelta = originalPrefabSize;
            }
            cardObj.transform.localPosition = Vector3.zero;
            cardObj.transform.localScale = Vector3.one * removeCardDisplayScale;
            cardObj.transform.localRotation = Quaternion.identity;

            spawnedRemoveCardObjects.Add(slotObj);

            CardUI cardUI = cardObj.GetComponent<CardUI>();
            if (cardUI != null)
            {
                cardUI.isDisplayOnly = true;
                cardUI.Setup(cardData);
            }

            GameObject clickOverlay = new GameObject("ClickTrigger", typeof(RectTransform), typeof(Image), typeof(EventTrigger));
            clickOverlay.transform.SetParent(slotObj.transform, false);

            RectTransform overlayRect = clickOverlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            clickOverlay.transform.localPosition = Vector3.zero;

            Image overlayImg = clickOverlay.GetComponent<Image>();
            overlayImg.color = new Color(0f, 0f, 0f, 0f);
            overlayImg.raycastTarget = true;

            CardData capturedCard = cardData;
            GameObject capturedSlot = slotObj;
            EventTrigger trigger = clickOverlay.GetComponent<EventTrigger>();

            EventTrigger.Entry enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener((d) =>
            {
                capturedSlot.transform.DOKill();
                capturedSlot.transform.DOScale(Vector3.one * 1.05f, 0.15f).SetEase(Ease.OutBack);
            });
            trigger.triggers.Add(enter);

            EventTrigger.Entry exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener((d) =>
            {
                capturedSlot.transform.DOKill();
                capturedSlot.transform.DOScale(Vector3.one, 0.15f).SetEase(Ease.OutCubic);
            });
            trigger.triggers.Add(exit);

            EventTrigger.Entry click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            click.callback.AddListener((d) =>
            {
                ConfirmRemoveCard(capturedCard);
            });
            trigger.triggers.Add(click);
        }

        if (cancelRemoveButton != null)
        {
            cancelRemoveButton.transform.SetAsLastSibling();
        }
    }

    private void ConfirmRemoveCard(CardData cardToRemove)
    {
        if (cardToRemove == null || RunDataManager.Instance == null) return;

        if (RunDataManager.Instance.TrySpendCurrency(removeCardCost))
        {
            RunDataManager.Instance.RemoveCardFromGlobalDeck(cardToRemove);
            hasUsedRemoveService = true;
            Debug.Log($"[ShopManager] 成功删除卡牌：{cardToRemove.cardName}，扣除 {removeCardCost}");

            CloseRemoveCardSelection();
            RefreshShopInteractiveState();
        }
    }

    public void CloseRemoveCardSelection()
    {
        if (removeCardPanel != null)
        {
            removeCardPanel.SetActive(false);
        }

        foreach (var obj in spawnedRemoveCardObjects)
        {
            if (obj != null) Destroy(obj);
        }
        spawnedRemoveCardObjects.Clear();
    }

    private void UpdateCurrencyUI(int newCurrency)
    {
        if (currencyText != null)
        {
            currencyText.text = newCurrency.ToString();
        }
        RefreshShopInteractiveState();
    }

    private List<CardData> GetAvailableCardPool()
    {
        if (customShopPool != null && customShopPool.Count > 0)
            return customShopPool;

        CardDeckData deckConfig = (RunDataManager.Instance != null && RunDataManager.Instance.initialDeckConfig != null)
            ? RunDataManager.Instance.initialDeckConfig
            : fallbackDeckData;

        if (deckConfig != null && deckConfig.AllAvailableCards != null && deckConfig.AllAvailableCards.Count > 0)
        {
            List<CardData> validCards = new List<CardData>();
            foreach (var c in deckConfig.AllAvailableCards)
            {
                if (c != null) validCards.Add(c);
            }
            return validCards;
        }

        return new List<CardData>();
    }

    private void OnLeaveShopClicked()
    {
        SceneManager.LoadScene(mapSceneName);
    }
}