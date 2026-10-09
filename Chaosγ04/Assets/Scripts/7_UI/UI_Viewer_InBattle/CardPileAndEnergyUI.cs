using UnityEngine;
using TMPro; // 引入 TextMeshPro 命名空间

/// <summary>
/// 负责抽牌堆、弃牌堆的数量显示与点击打开卡牌浏览弹窗。
/// 能量与行动力数值已移交由 PlayerStatsUI 统一管理。
/// </summary>
public class CardPileAndEnergyUI : MonoBehaviour
{
    [Header("牌堆数据 UI 引用")]
    [Tooltip("抽牌堆右上角的数字文本")]
    public TextMeshProUGUI drawPileText;

    [Tooltip("弃牌堆右上角的数字文本")]
    public TextMeshProUGUI discardPileText;

    private void Start()
    {
        // ---------------- 监听并刷新 牌堆数据 ----------------
        if (CardManager.Instance != null)
        {
            CardManager.Instance.OnPileCountChanged += UpdatePileUI;

            // 初始主动刷新一次
            UpdatePileUI(CardManager.Instance.drawPile.Count, CardManager.Instance.discardPile.Count);
        }
        else
        {
            Debug.LogWarning("[CardPileAndEnergyUI] 未找到 CardManager.Instance，请检查场景中的挂载状态！");
        }
    }

    private void OnDestroy()
    {
        if (CardManager.Instance != null)
        {
            CardManager.Instance.OnPileCountChanged -= UpdatePileUI;
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