using System.Collections.Generic;
using UnityEngine;
using TMPro;
using DG.Tweening;

public class CardPileViewerUI : MonoBehaviour
{
    public static CardPileViewerUI Instance; // 单例模式，方便其他脚本调用

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
        if (titleText != null) titleText.text = title;

        // 2. 清空之前生成的旧卡牌
        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }

        // 3. 生成新卡牌
        foreach (CardData cardData in pileToView)
        {
            GameObject cardObj = Instantiate(cardPrefab, contentParent);
            CardUI cardUI = cardObj.GetComponent<CardUI>();

            if (cardUI != null)
            {
                cardUI.Setup(cardData);
                cardUI.isDisplayOnly = true; // 【关键】标记为仅展示，防止在弹窗里打出牌！
            }
        }

        // 4. 播放弹窗出现动画 (类似 Figma 的弹出效果)
        popupPanel.gameObject.SetActive(true);
        popupPanel.transform.localScale = Vector3.one * 0.8f; // 初始稍微缩小

        popupPanel.DOFade(1f, 0.25f);
        popupPanel.transform.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutBack);

        popupPanel.interactable = true;
        popupPanel.blocksRaycasts = true;
    }

    /// <summary>
    /// 关闭弹窗 (绑定给弹窗上的关闭按钮)
    /// </summary>
    public void HidePile()
    {
        if (popupPanel == null) return;

        popupPanel.interactable = false;
        popupPanel.blocksRaycasts = false;

        // 播放消失动画
        popupPanel.transform.DOScale(Vector3.one * 0.8f, 0.2f).SetEase(Ease.InBack);
        popupPanel.DOFade(0f, 0.2f);
    }
}