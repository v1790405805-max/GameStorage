using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BattleRewardUI : MonoBehaviour
{
    [Header("UI 面板引用")]
    public GameObject victoryPanel;
    public GameObject defeatPanel;

    [Header("三选一奖励卡设置")]
    public Transform rewardCardContainer; // 存放3张卡片的父容器(可搭配 Horizontal Layout Group 使用)
    public GameObject rewardCardPrefab;   // 奖励展示卡牌的卡牌Prefab(需包含 Button 和 CardUI 组件)

    [Header("场景切换设置")]
    [Tooltip("指定场景中的 SceneLoader 脚本组件，用于战斗结束后转场回地图或菜单")]
    public SceneLoader sceneLoader;

    private void Start()
    {
        // 游戏开始时隐藏奖励面板
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (defeatPanel != null) defeatPanel.SetActive(false);
    }

    /// <summary>
    /// 显示失败面板（由 BattleConditionManager 调用）
    /// </summary>
    public void ShowDefeat()
    {
        if (defeatPanel != null) defeatPanel.SetActive(true);
    }

    /// <summary>
    /// 显示胜利奖励并发放卡牌（由 BattleConditionManager 调用）
    /// </summary>
    public void ShowVictoryReward()
    {
        if (victoryPanel != null) victoryPanel.SetActive(true);

        // 1. 获取 3 张随机卡牌作为奖励
        List<CardData> rewardCards = CardManager.Instance.GetRandomRewardCards(3);

        // 2. 清空奖励UI，防止重复生成
        foreach (Transform child in rewardCardContainer)
        {
            Destroy(child.gameObject);
        }

        // 3. 生成可点击的奖励卡
        foreach (CardData card in rewardCards)
        {
            GameObject cardObj = Instantiate(rewardCardPrefab, rewardCardContainer);

            // 将数据绑定到 UI
            CardUI cardUI = cardObj.GetComponent<CardUI>();
            if (cardUI != null)
            {
                cardUI.isSelectingMode = true; // reward cards: selecting mode (golden hover border, drag disabled)
                cardUI.Setup(card);
            }

            // 确保有 Button 组件
            Button btn = cardObj.GetComponent<Button>();
            if (btn == null) btn = cardObj.AddComponent<Button>();

            // 添加监听：将选中的卡加入全局卡组，然后切换场景
            btn.onClick.AddListener(() => OnRewardCardSelected(card));
        }
    }

    /// <summary>
    /// 玩家点击选中的某张卡牌后逻辑
    /// </summary>
    private void OnRewardCardSelected(CardData selectedCard)
    {
        Debug.Log($"你选择了奖励卡: {selectedCard.cardName}");

        // 1. 加入本地全局卡组
        if (RunDataManager.Instance != null)
        {
            RunDataManager.Instance.AddCardToGlobalDeck(selectedCard);
        }

        // 2. 使用场景中的 SceneLoader 完成转场
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
