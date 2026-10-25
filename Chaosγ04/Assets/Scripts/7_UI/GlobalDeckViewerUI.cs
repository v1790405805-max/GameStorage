using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 全局卡组查看器：
/// 保持卡牌原始Rect尺寸不变，仅通过整体Scale缩放，彻底杜绝内部组件被拉扯错位。
/// </summary>
public class GlobalDeckViewerUI : MonoBehaviour
{
    public static GlobalDeckViewerUI Instance { get; private set; }

    [Header("UI 容器与组件配置")]
    [Tooltip("全屏弹窗根面板（用于显隐）")]
    [SerializeField] private GameObject panelRoot;

    [Tooltip("装载卡牌预制体的 Content 父节点 (建议挂载 GridLayoutGroup 或 HorizontalLayoutGroup)")]
    [SerializeField] private Transform cardContentParent;

    [Tooltip("卡牌预制体 (Card Prefab)")]
    [SerializeField] private GameObject cardPrefab;

    [Tooltip("标题文本（如：当前全局卡组 (10张)）")]
    [SerializeField] private TextMeshProUGUI titleText;

    [Tooltip("关闭/返回按钮")]
    [SerializeField] private Button closeButton;

    [Header("常驻呼出按钮")]
    [Tooltip("右侧/右上角常驻卡组按钮 (点击此按钮直接呼出)")]
    [SerializeField] private Button openDeckButton;

    [Header("--- 卡牌缩放解耦配置 ---")]
    [Tooltip("卡牌在卡组面板中的整体缩放倍率（推荐 1.1 ~ 1.25）")]
    [Range(0.5f, 2.0f)]
    [SerializeField] private float cardDisplayScale = 1.15f;

    private readonly List<GameObject> spawnedCardObjects = new List<GameObject>();

    private void Awake()
    {
        Instance = this;

        if (panelRoot == null)
        {
            panelRoot = gameObject;
        }

        if (openDeckButton != null)
        {
            openDeckButton.onClick.RemoveAllListeners();
            openDeckButton.onClick.AddListener(OpenViewer);
            BindPointerDown(openDeckButton.gameObject, OpenViewer);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(CloseViewer);
            BindPointerDown(closeButton.gameObject, CloseViewer);
        }
    }

    private void Start()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    private void BindPointerDown(GameObject target, System.Action callback)
    {
        if (target == null || callback == null) return;

        EventTrigger trigger = target.GetComponent<EventTrigger>();
        if (trigger == null) trigger = target.AddComponent<EventTrigger>();

        EventTrigger.Entry downEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        downEntry.callback.AddListener((d) => { callback.Invoke(); });
        trigger.triggers.Add(downEntry);
    }

    public void OpenViewer()
    {
        if (panelRoot != null)
        {
            panelRoot.transform.SetAsLastSibling();
            Vector3 pos = panelRoot.transform.localPosition;
            panelRoot.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
            panelRoot.SetActive(true);
        }

        RefreshDeckDisplay();
    }

    public void CloseViewer()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        ClearSpawnedCards();
    }

    public void RefreshDeckDisplay()
    {
        ClearSpawnedCards();

        if (RunDataManager.Instance == null)
        {
            Debug.LogWarning("[GlobalDeckViewerUI] 未找到 RunDataManager 实例！");
            return;
        }

        List<CardData> deck = RunDataManager.Instance.playerGlobalDeck;
        if (titleText != null)
        {
            titleText.text = $"当前全局卡组 ({deck.Count} 张)";
        }

        if (cardPrefab == null || cardContentParent == null) return;

        // 严格获取卡牌预制体自身原本的真实尺寸（如 180 x 300）
        Vector2 originalPrefabSize = new Vector2(180f, 300f);
        RectTransform prefabRect = cardPrefab.GetComponent<RectTransform>();
        if (prefabRect != null)
        {
            originalPrefabSize = prefabRect.rect.size;
        }

        for (int i = 0; i < deck.Count; i++)
        {
            CardData data = deck[i];
            if (data == null) continue;

            // 1. 槽位占位容器：尺寸设为 原尺寸 × 缩放倍率，保证 Grid 留出正确的网格间距
            GameObject slotObj = new GameObject($"DeckCardSlot_{i}", typeof(RectTransform));
            slotObj.transform.SetParent(cardContentParent, false);

            RectTransform slotRect = slotObj.GetComponent<RectTransform>();
            slotRect.sizeDelta = originalPrefabSize * cardDisplayScale;

            LayoutElement le = slotObj.AddComponent<LayoutElement>();
            le.preferredWidth = originalPrefabSize.x * cardDisplayScale;
            le.preferredHeight = originalPrefabSize.y * cardDisplayScale;

            // 2. 卡牌实例：保持原有 sizeDelta 完全不变，只进行整体 localScale 缩放！
            GameObject cardObj = Instantiate(cardPrefab, slotObj.transform);
            RectTransform cardRect = cardObj.GetComponent<RectTransform>();
            if (cardRect != null)
            {
                cardRect.anchorMin = new Vector2(0.5f, 0.5f);
                cardRect.anchorMax = new Vector2(0.5f, 0.5f);
                cardRect.pivot = new Vector2(0.5f, 0.5f);
                cardRect.anchoredPosition = Vector2.zero;
                cardRect.sizeDelta = originalPrefabSize; // 锁定原始尺寸，杜绝子节点拉扯
            }

            cardObj.transform.localPosition = Vector3.zero;
            cardObj.transform.localScale = Vector3.one * cardDisplayScale; // 纯物理整体放大
            cardObj.transform.localRotation = Quaternion.identity;

            spawnedCardObjects.Add(slotObj);

            CardUI cardUI = cardObj.GetComponent<CardUI>();
            if (cardUI != null)
            {
                cardUI.isDisplayOnly = true;
                cardUI.Setup(data);
            }
        }
    }

    private void ClearSpawnedCards()
    {
        foreach (var obj in spawnedCardObjects)
        {
            if (obj != null) Destroy(obj);
        }
        spawnedCardObjects.Clear();
    }
}