using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class CardPileViewerUI : MonoBehaviour
{
    public static CardPileViewerUI Instance; // 单例模式，方便其他脚本调用

    /// <summary>
    /// 弹窗关闭事件广播：通知牌堆图标恢复收起状态
    /// </summary>
    public event Action OnViewerClosed;

    [Header("--- UI 节点关联 ---")]
    [Tooltip("弹窗的主背景容器 (挂载了 CanvasGroup)")]
    public CanvasGroup popupPanel;

    [Tooltip("弹窗的标题 (显示'抽牌堆'或'弃牌堆')")]
    public TextMeshProUGUI titleText;

    [Tooltip("卡牌生成的父节点 (通常是 Scroll View 里的 Content)")]
    public Transform contentParent;

    [Header("--- 预制体 ---")]
    [Tooltip("你用来显示的卡牌预制体 (可以直接用之前的 CardPrefab)")]
    public GameObject cardPrefab;

    [Header("--- 卡牌缩放与尺寸解耦配置 ---")]
    [Tooltip("卡牌在弹窗中的显示缩放倍率 (推荐 1.1 ~ 1.25，可在 Inspector 自由拉大拉小)")]
    [Range(0.5f, 2.0f)]
    public float cardDisplayScale = 1.15f;

    [Tooltip("卡牌预制体基准参考尺寸 (留空或为0则自动读取 Prefab 自身的宽高)")]
    public Vector2 defaultCardSize = new Vector2(180f, 300f);

    private void Awake()
    {
        // 设置单例
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // 游戏开始时，确保弹窗是隐藏的
        if (popupPanel != null)
        {
            popupPanel.alpha = 0f;
            popupPanel.interactable = false;
            popupPanel.blocksRaycasts = false;
        }
    }

    /// <summary>
    /// 打开弹窗并展示指定的卡牌堆
    /// </summary>
    /// <param name="pileToView">要展示的卡牌数据列表</param>
    /// <param name="title">弹窗标题</param>
    public void ShowPile(List<CardData> pileToView, string title)
    {
        if (popupPanel == null) return;

        // 1. 设置标题
        if (titleText != null)
        {
            int count = pileToView != null ? pileToView.Count : 0;
            titleText.text = $"{title} ({count} 张)";
        }

        // 2. 清空之前生成的旧卡牌
        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }

        // 3. 读取卡牌原本的标准设计尺寸 (180 x 300)
        Vector2 originalPrefabSize = defaultCardSize;
        if (cardPrefab != null)
        {
            RectTransform pRect = cardPrefab.GetComponent<RectTransform>();
            if (pRect != null && defaultCardSize == Vector2.zero)
            {
                originalPrefabSize = pRect.rect.size;
            }
        }

        // 4. 生成新卡牌（采用 Slot 槽位隔离机制，锁定卡牌 sizeDelta，使用 localScale 物理整体放大）
        if (pileToView != null && cardPrefab != null && contentParent != null)
        {
            for (int i = 0; i < pileToView.Count; i++)
            {
                CardData cardData = pileToView[i];
                if (cardData == null) continue;

                // ① 创建槽位容器，负责被 Content 的 LayoutGroup 排版，并吸收布局尺寸
                GameObject slotObj = new GameObject($"PileCardSlot_{i}", typeof(RectTransform));
                slotObj.transform.SetParent(contentParent, false);

                RectTransform slotRect = slotObj.GetComponent<RectTransform>();
                slotRect.sizeDelta = originalPrefabSize * cardDisplayScale;

                LayoutElement le = slotObj.AddComponent<LayoutElement>();
                le.preferredWidth = originalPrefabSize.x * cardDisplayScale;
                le.preferredHeight = originalPrefabSize.y * cardDisplayScale;

                // ② 在槽位内部生成卡牌实体：保持原始预制体宽高不变，绝不被拉扯！
                GameObject cardObj = Instantiate(cardPrefab, slotObj.transform);
                RectTransform cardRect = cardObj.GetComponent<RectTransform>();
                if (cardRect != null)
                {
                    cardRect.anchorMin = new Vector2(0.5f, 0.5f);
                    cardRect.anchorMax = new Vector2(0.5f, 0.5f);
                    cardRect.pivot = new Vector2(0.5f, 0.5f);
                    cardRect.anchoredPosition = Vector2.zero;
                    cardRect.sizeDelta = originalPrefabSize; // 牢牢锁死原始尺寸
                }

                cardObj.transform.localPosition = Vector3.zero;
                cardObj.transform.localScale = Vector3.one * cardDisplayScale; // 纯物理整体放大
                cardObj.transform.localRotation = Quaternion.identity;

                // ③ 数据绑定与纯展示模式锁定
                CardUI cardUI = cardObj.GetComponent<CardUI>();
                if (cardUI != null)
                {
                    cardUI.isDisplayOnly = true; // 【关键】标记为仅展示，防止在弹窗里打出牌！
                    cardUI.Setup(cardData);
                }
            }

            // 强制刷新一次排版计算
            if (contentParent is RectTransform rt)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            }
        }

        // 5. 播放弹窗出现动画（保持原逻辑完全不变）
        popupPanel.DOKill();
        popupPanel.transform.DOKill();

        popupPanel.gameObject.SetActive(true);
        popupPanel.transform.localScale = Vector3.one * 0.8f;

        popupPanel.DOFade(1f, 0.25f);
        popupPanel.transform.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutBack);

        popupPanel.interactable = true;
        popupPanel.blocksRaycasts = true;
    }

    /// <summary>
    /// 关闭弹窗 (绑定给弹窗上的关闭按钮，函数名与原脚本保持一致)
    /// </summary>
    public void HidePile()
    {
        if (popupPanel == null) return;

        popupPanel.interactable = false;
        popupPanel.blocksRaycasts = false;

        popupPanel.DOKill();
        popupPanel.transform.DOKill();

        // 播放消失动画并在收起完毕后隐藏对象（保持原逻辑完全不变）
        popupPanel.transform.DOScale(Vector3.one * 0.8f, 0.2f).SetEase(Ease.InBack);
        popupPanel.DOFade(0f, 0.2f).OnComplete(() =>
        {
            popupPanel.gameObject.SetActive(false);
        });

        // 触发关闭事件广播，通知牌堆图标恢复收起
        OnViewerClosed?.Invoke();
    }
}