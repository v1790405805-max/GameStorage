using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 单个行动点灯珠控制器：管理亮灭状态与消耗动效
/// </summary>
public class ActionPointPipUI : MonoBehaviour
{
    [Header("视觉组件绑定")]
    [Tooltip("点亮状态的亮黄色 Image（或挂有 CanvasGroup 的物体）")]
    [SerializeField] private Image litImage;

    [Header("动效参数")]
    [SerializeField] private float fadeDuration = 0.2f;

    private bool isLit = true;

    /// <summary>
    /// 设置亮灭状态（带补间动画）
    /// </summary>
    public void SetState(bool active, bool animate = true)
    {
        if (litImage == null) return;
        if (isLit == active && animate) return;

        isLit = active;
        litImage.DOKill();
        transform.DOKill();

        float targetAlpha = active ? 1f : 0f;

        if (!animate)
        {
            litImage.color = new Color(litImage.color.r, litImage.color.g, litImage.color.b, targetAlpha);
            return;
        }

        if (active)
        {
            // 点亮反馈：平滑渐显并带有轻微膨胀回弹
            litImage.DOFade(1f, fadeDuration).SetEase(Ease.OutQuad);
            transform.DOScale(1f, fadeDuration).SetEase(Ease.OutBack);
        }
        else
        {
            // 熄灭反馈：迅速微震收缩并淡出
            transform.DOPunchScale(Vector3.one * 0.2f, 0.15f, 4, 0.5f);
            litImage.DOFade(0f, fadeDuration).SetEase(Ease.InQuad);
        }
    }
}