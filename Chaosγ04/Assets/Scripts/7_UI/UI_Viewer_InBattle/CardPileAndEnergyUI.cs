using UnityEngine;
using TMPro;

/// <summary>
/// 负责抽牌堆、弃牌堆数量显示，并协同管理点击弹窗与牌堆展开状态锁定。
/// </summary>
public class CardPileAndEnergyUI : MonoBehaviour
{
    [Header("牌堆数据 UI 引用")]
    [Tooltip("抽牌堆右上角的数字文本")]
    public TextMeshProUGUI drawPileText;

    [Tooltip("弃牌堆右上角的数字文本")]
    public TextMeshProUGUI discardPileText;

    [Header("牌堆动效组件引用 (可选)")]
    [Tooltip("抽牌堆按钮上的 CardPileHoverEffect 组件")]
    public CardPileHoverEffect drawPileHoverEffect;

    [Tooltip("弃牌堆按钮上的 CardPileHoverEffect 组件")]
    public CardPileHoverEffect discardPileHoverEffect;

    private void Start()
    {
        // 1. 监听并刷新牌堆数据
        if (CardManager.Instance != null)
        {
            CardManager.Instance.OnPileCountChanged += UpdatePileUI;
            UpdatePileUI(CardManager.Instance.drawPile.Count, CardManager.Instance.discardPile.Count);
        }
        else
        {
            Debug.LogWarning("[CardPileAndEnergyUI] 未找到 CardManager.Instance，请检查场景中的挂载状态！");
        }

        // 2. 监听弹窗关闭事件，弹窗关闭后自动收拢牌堆
        if (CardPileViewerUI.Instance != null)
        {
            CardPileViewerUI.Instance.OnViewerClosed += HandleViewerClosed;
        }
    }

    private void OnDestroy()
    {
        if (CardManager.Instance != null)
        {
            CardManager.Instance.OnPileCountChanged -= UpdatePileUI;
        }

        if (CardPileViewerUI.Instance != null)
        {
            CardPileViewerUI.Instance.OnViewerClosed -= HandleViewerClosed;
        }
    }

    #region UI 刷新逻辑

    private void UpdatePileUI(int drawCount, int discardCount)
    {
        if (drawPileText != null) drawPileText.text = drawCount.ToString();
        if (discardPileText != null) discardPileText.text = discardCount.ToString();
    }

    #endregion

    #region UI 按钮点击交互 (调用弹窗并锁定展开)

    /// <summary>
    /// 绑定给“抽牌堆图标”上的 Button 组件
    /// </summary>
    public void OnClickDrawPile()
    {
        if (CardManager.Instance == null) return;

        // 锁定抽牌堆为展开状态
        if (drawPileHoverEffect != null)
            drawPileHoverEffect.SetHoldOpen(true);

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

        // 锁定弃牌堆为展开状态
        if (discardPileHoverEffect != null)
            discardPileHoverEffect.SetHoldOpen(true);

        if (CardPileViewerUI.Instance != null)
        {
            CardPileViewerUI.Instance.ShowPile(CardManager.Instance.discardPile, "弃牌堆");
        }
        else
        {
            Debug.LogWarning("未找到 CardPileViewerUI 单例，请确认场景中是否已添加弹窗预制体！");
        }
    }

    /// <summary>
    /// 当玩家点击弹窗里的“关闭弹窗”后触发，解除牌堆锁定并收拢
    /// </summary>
    private void HandleViewerClosed()
    {
        if (drawPileHoverEffect != null)
            drawPileHoverEffect.SetHoldOpen(false);

        if (discardPileHoverEffect != null)
            discardPileHoverEffect.SetHoldOpen(false);
    }

    #endregion
}