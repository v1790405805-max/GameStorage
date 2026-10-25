using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

/// <summary>
/// 商店货架悬停放大联动器：
/// 当鼠标移入卡牌或其下方价格按钮时，整个槽位统一平滑放大并提升渲染顺序。
/// </summary>
public class ShopCardHoverTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("动效配置")]
    [SerializeField] private float hoverScale = 1.15f;
    [SerializeField] private float duration = 0.2f;

    private Vector3 originalScale;
    private int originalSiblingIndex;

    private void Awake()
    {
        originalScale = transform.localScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        transform.DOKill();
        // 原生置顶，防止放大时被相邻右侧的卡牌或按钮压住
        originalSiblingIndex = transform.GetSiblingIndex();
        transform.SetAsLastSibling();

        transform.DOScale(originalScale * hoverScale, duration).SetEase(Ease.OutBack);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        transform.DOKill();
        transform.SetSiblingIndex(originalSiblingIndex);
        transform.DOScale(originalScale, duration).SetEase(Ease.OutCubic);
    }
}