using UnityEngine;
using TMPro; // 引入 TextMeshPro 命名空间

public class CardPileAndEnergyUI : MonoBehaviour
{
    [Header("牌堆数据 UI 引用")]
    [Tooltip("抽牌堆右上角的数字文本")]
    public TextMeshProUGUI drawPileText;

    [Tooltip("弃牌堆右上角的数字文本")]
    public TextMeshProUGUI discardPileText;

    [Header("能量点/行动点数据 UI 引用")]
    [Tooltip("能量点数据文本")]
    public TextMeshProUGUI energyText;

    [Tooltip("行动点数据文本")]
    public TextMeshProUGUI actionPointText;

    // 预留拓展（后续需要时可解除注释）
    // public TextMeshProUGUI hpText; 
    // public TextMeshProUGUI blockText;

    private void Start()
    {
        // ---------------- 1. 监听并刷新 牌堆数据 ----------------
        if (CardManager.Instance != null)
        {
            // 订阅牌堆变化事件
            CardManager.Instance.OnPileCountChanged += UpdatePileUI;

            // 初始主动刷新一次，防止刚进游戏时显示为空
            UpdatePileUI(CardManager.Instance.drawPile.Count, CardManager.Instance.discardPile.Count);
        }
        else
        {
            Debug.LogWarning("CardPileAndEnergyUI: 未找到 CardManager.Instance，请检查场景中的挂载状态！");
        }

        // ---------------- 2. 监听并刷新 玩家属性数据 ----------------
        if (CombatStatsManager.Instance != null)
        {
            // 订阅战斗属性变化事件
            CombatStatsManager.Instance.OnStatsChanged += UpdateStatsUI;

            // 初始主动刷新一次
            UpdateStatsUI();
        }
        else
        {
            Debug.LogWarning("CardPileAndEnergyUI: 未找到 BattleManager.Instance，请检查场景中的挂载状态！");
        }
    }

    private void OnDestroy()
    {
        // 取消订阅，防止内存泄漏或切场景报错
        if (CardManager.Instance != null)
        {
            CardManager.Instance.OnPileCountChanged -= UpdatePileUI;
        }

        if (CombatStatsManager.Instance != null)
        {
            CombatStatsManager.Instance.OnStatsChanged -= UpdateStatsUI;
        }
    }

    #region UI 刷新逻辑

    /// <summary>
    /// 刷新牌堆数量显示
    /// </summary>
    private void UpdatePileUI(int drawCount, int discardCount)
    {
        if (drawPileText != null) drawPileText.text = drawCount.ToString();
        if (discardPileText != null) discardPileText.text = discardCount.ToString();
    }

    /// <summary>
    /// 刷新玩家属性（能量、行动点等）显示
    /// </summary>
    private void UpdateStatsUI()
    {
        if (CombatStatsManager.Instance == null) return;

        if (energyText != null)
        {
            energyText.text = $"能量: {CombatStatsManager.Instance.currentEnergy} / {CombatStatsManager.Instance.maxEnergy}";
        }

        if (actionPointText != null)
        {
            actionPointText.text = $"行动点: {CombatStatsManager.Instance.currentActionPoint} / {CombatStatsManager.Instance.maxActionPoint}";
        }
    }

    #endregion

    #region UI 按钮点击交互 (调用弹窗)

    /// <summary>
    /// 绑定给“抽牌堆图标”上的 Button 组件
    /// </summary>
    public void OnClickDrawPile()
    {
        if (CardManager.Instance == null) return;

        if (CardPileViewerUI.Instance != null)
        {
            CardPileViewerUI.Instance.ShowPile(CardManager.Instance.drawPile, "抽牌堆");
        }
        else
        {
            Debug.LogWarning("未找到 CardPileViewerUI 单例，请确认场景中是否已添加弹窗预制体！");
        }
    }

    /// <summary>
    /// 绑定给“弃牌堆图标”上的 Button 组件
    /// </summary>
    public void OnClickDiscardPile()
    {
        if (CardManager.Instance == null) return;

        if (CardPileViewerUI.Instance != null)
        {
            CardPileViewerUI.Instance.ShowPile(CardManager.Instance.discardPile, "弃牌堆");
        }
        else
        {
            Debug.LogWarning("未找到 CardPileViewerUI 单例，请确认场景中是否已添加弹窗预制体！");
        }
    }

    #endregion
}