using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;

public class BattleRewardUI : MonoBehaviour
{
    [Header("UI 面板引用")]
    public GameObject victoryPanel;
    public GameObject defeatPanel;

    [Header("三选一奖励卡设置")]
    public Transform rewardCardContainer; // 存放3张卡片的父容器(挂载 Horizontal Layout Group)
    public GameObject rewardCardPrefab;   // 奖励展示卡牌的卡牌Prefab

    [Header("奖励卡牌显示与缩放配置")]
    [Tooltip("奖励卡牌在结算界面的初始基础缩放倍率 (推荐 1.0 ~ 1.2)")]
    [Range(0.5f, 2.0f)]
    public float cardDisplayScale = 1.1f;

    [Tooltip("鼠标悬停时的放大倍率 (如 1.1 代表在原有基础上放大 10%)")]
    [Range(1.0f, 1.3f)]
    public float hoverScaleMultiplier = 1.1f;

    [Tooltip("悬停动效过渡时间 (秒)")]
    public float hoverDuration = 0.2f;

    [Tooltip("卡牌预制体基准尺寸 (留空则自动读取 Prefab 自身的宽高)")]
    public Vector2 defaultCardSize = new Vector2(180f, 300f);

    [Header("场景切换设置")]
    [Tooltip("指定场景中的 SceneLoader 脚本组件，用于战斗结束后转场回地图或菜单")]
    public SceneLoader sceneLoader;

    private void Start()
    {
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (defeatPanel != null) defeatPanel.SetActive(false);
    }

    public void ShowDefeat()
    {
        if (defeatPanel != null) defeatPanel.SetActive(true);
    }

    public void ShowVictoryReward()
    {
        if (victoryPanel != null) victoryPanel.SetActive(true);

        List<CardData> rewardCards = CardManager.Instance.GetRandomRewardCards(3);

        foreach (Transform child in rewardCardContainer)
        {
            Destroy(child.gameObject);
        }

        Vector2 originalSize = defaultCardSize;
        if (rewardCardPrefab != null)
        {
            RectTransform pRect = rewardCardPrefab.GetComponent<RectTransform>();
            if (pRect != null) originalSize = pRect.rect.size;
        }

        for (int i = 0; i < rewardCards.Count; i++)
        {
            CardData card = rewardCards[i];
            if (card == null) continue;

            // 1. 独立的槽位容器（HorizontalLayoutGroup 严格排布，不改动 SiblingIndex）
            GameObject slotObj = new GameObject($"RewardSlot_{i}", typeof(RectTransform));
            slotObj.transform.SetParent(rewardCardContainer, false);

            RectTransform slotRect = slotObj.GetComponent<RectTransform>();
            slotRect.sizeDelta = originalSize * cardDisplayScale;

            LayoutElement le = slotObj.AddComponent<LayoutElement>();
            le.preferredWidth = originalSize.x * cardDisplayScale;
            le.preferredHeight = originalSize.y * cardDisplayScale;

            // 2. 卡牌实体（纯 UI 节点，不挂 Canvas，杜绝掉落到 3D 地形后方）
            GameObject cardObj = Instantiate(rewardCardPrefab, slotObj.transform);
            RectTransform cardRect = cardObj.GetComponent<RectTransform>();
            if (cardRect != null)
            {
                cardRect.anchorMin = new Vector2(0.5f, 0.5f);
                cardRect.anchorMax = new Vector2(0.5f, 0.5f);
                cardRect.pivot = new Vector2(0.5f, 0.5f);
                cardRect.anchoredPosition = Vector2.zero;
                cardRect.sizeDelta = originalSize;
            }

            cardObj.transform.localPosition = Vector3.zero;
            cardObj.transform.localScale = Vector3.one * cardDisplayScale;
            cardObj.transform.localRotation = Quaternion.identity;

            CardUI cardUI = cardObj.GetComponent<CardUI>();
            if (cardUI != null)
            {
                cardUI.isDisplayOnly = true;
                cardUI.isSelectingMode = true;
                cardUI.Setup(card);
            }

            // 3. 顶层点击与悬停事件遮罩
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

            CardData capturedCard = card;
            GameObject capturedCardObj = cardObj;
            Vector3 baseScale = Vector3.one * cardDisplayScale;

            EventTrigger trigger = clickOverlay.GetComponent<EventTrigger>();

            // 鼠标移入：仅在槽位原地平滑放大，不改动层级和位置
            EventTrigger.Entry enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enterEntry.callback.AddListener((data) =>
            {
                capturedCardObj.transform.DOKill();
                capturedCardObj.transform.DOScale(baseScale * hoverScaleMultiplier, hoverDuration).SetEase(Ease.OutBack);
            });
            trigger.triggers.Add(enterEntry);

            // 鼠标移出：平滑缩回初始大小
            EventTrigger.Entry exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exitEntry.callback.AddListener((data) =>
            {
                capturedCardObj.transform.DOKill();
                capturedCardObj.transform.DOScale(baseScale, hoverDuration).SetEase(Ease.OutCubic);
            });
            trigger.triggers.Add(exitEntry);

            // 点击选牌：存入卡组并加载转场
            EventTrigger.Entry clickEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            clickEntry.callback.AddListener((data) =>
            {
                OnRewardCardSelected(capturedCard);
            });
            trigger.triggers.Add(clickEntry);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(rewardCardContainer as RectTransform);
    }

    private void OnRewardCardSelected(CardData selectedCard)
    {
        Debug.Log($"你选择了奖励卡: {selectedCard.cardName}");

        if (RunDataManager.Instance != null)
        {
            RunDataManager.Instance.AddCardToGlobalDeck(selectedCard);
        }

        if (sceneLoader != null)
        {
            sceneLoader.LoadTargetScene();
        }
        else
        {
            Debug.LogWarning("【BattleRewardUI】未配置 SceneLoader，无法自动转场");
        }
    }
}