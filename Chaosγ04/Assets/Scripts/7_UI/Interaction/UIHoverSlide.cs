using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening; // 引入 DOTween 命名空间

/// <summary>
/// 基于 DOTween 的 UI 悬浮滑动组件：
/// 鼠标悬浮时向左平滑滑出，移出时平滑归位。
/// </summary>
public class UIHoverSlide : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("滑动目标与偏移")]
    [Tooltip("实际执行位移的 RectTransform。留空则默认作用于自身")]
    [SerializeField] private RectTransform targetRect;

    [Tooltip("向左拉出的偏移距离（负数向左，例如 X = -80）")]
    [SerializeField] private Vector2 slideOffset = new Vector2(-80f, 0f);

    [Header("DOTween 动画参数")]
    [Tooltip("拉出/收回的耗时（秒）")]
    [SerializeField] private float duration = 0.2f;

    [Tooltip("滑出时的曲线（推荐 OutCubic 迅捷利落，或 OutBack 带轻微机械回弹）")]
    [SerializeField] private Ease enterEase = Ease.OutCubic;

    [Tooltip("收回时的曲线（推荐 InCubic 或 InQuad）")]
    [SerializeField] private Ease exitEase = Ease.InCubic;

    [Tooltip("是否忽略时间缩放（游戏暂停时依然保持响应）")]
    [SerializeField] private bool ignoreTimeScale = true;

    private Vector2 defaultAnchoredPos;

    private void Awake()
    {
        if (targetRect == null)
            targetRect = GetComponent<RectTransform>();

        if (targetRect != null)
            defaultAnchoredPos = targetRect.anchoredPosition;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (targetRect == null) return;

        // 打断未完成的补间，防止快速划过时抽搐
        targetRect.DOKill();

        targetRect.DOAnchorPos(defaultAnchoredPos + slideOffset, duration)
            .SetEase(enterEase)
            .SetUpdate(ignoreTimeScale);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (targetRect == null) return;

        targetRect.DOKill();

        targetRect.DOAnchorPos(defaultAnchoredPos, duration)
            .SetEase(exitEase)
            .SetUpdate(ignoreTimeScale);
    }

    private void OnDisable()
    {
        // UI 被失活或切界面时安全收尾
        if (targetRect != null)
        {
            targetRect.DOKill();
            targetRect.anchoredPosition = defaultAnchoredPos;
        }
    }
}