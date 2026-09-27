using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("绑定组件")]
    public Image glowImage;              // 你的 Glow 花纹贴图
    public ParticleSystem hoverParticles;// 绑定的悬浮粒子

    [Header("发光呼吸参数")]
    public Color glowColor = new Color(0.3f, 0.95f, 0.75f, 1f); // 发光基础色（青碧色）
    public float breatheSpeed = 3f;      // 呼吸快慢
    [Range(0f, 1f)] public float minAlpha = 0.35f; // 最暗透明度
    [Range(0f, 1f)] public float maxAlpha = 1.0f;  // 最亮透明度
    public float fadeSpeed = 8f;         // 鼠标移入/移出时的淡入淡出速度

    private bool isHovered = false;
    private float hoverWeight = 0f;      // 移入淡入权重 (0~1)

    private void Awake()
    {
        if (glowImage != null)
        {
            Color c = glowColor;
            c.a = 0f;
            glowImage.color = c;
        }
    }

    private void Update()
    {
        if (glowImage == null) return;

        // 1. 平滑过渡淡入淡出
        float target = isHovered ? 1f : 0f;
        hoverWeight = Mathf.MoveTowards(hoverWeight, target, fadeSpeed * Time.unscaledDeltaTime);

        if (hoverWeight <= 0f)
        {
            // 移出后完全归零
            Color c = glowColor;
            c.a = 0f;
            glowImage.color = c;
            return;
        }

        // 2. 用正弦波计算呼吸节奏 (0 ~ 1)
        float wave = (Mathf.Sin(Time.unscaledTime * breatheSpeed) + 1f) * 0.5f;
        float breatheAlpha = Mathf.Lerp(minAlpha, maxAlpha, wave);

        // 3. 同时控制透明度与亮度，产生发光呼吸感
        Color finalColor = glowColor;
        finalColor.a = breatheAlpha * hoverWeight;
        glowImage.color = finalColor;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        if (hoverParticles != null) hoverParticles.Play();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        if (hoverParticles != null)
            hoverParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }
}