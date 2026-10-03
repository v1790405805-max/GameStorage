using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通用受击闪烁组件：支持曲线平滑衰减与淡入淡出调优
/// </summary>
public class UniversalHitFlash : MonoBehaviour
{
    [Header("核心节奏设置")]
    [Tooltip("受击总时长（秒）。动作游戏建议在 0.12 ~ 0.20 秒之间")]
    [SerializeField] private float flashDuration = 0.15f;

    [Tooltip("受击衰减曲线：横轴为 0~1 的归一化时间，纵轴为受击强度的权重 (0~1)")]
    [SerializeField]
    private AnimationCurve flashCurve = new AnimationCurve(
        new Keyframe(0f, 1f, 0f, -2.5f),   // 第 0 帧瞬间拉满到 1.0 (瞬间变红)
        new Keyframe(1f, 0f, 0f, 0f)       // 结尾平滑缓出到 0.0 (无痕衰减)
    );

    [Header("色彩与过滤配置")]
    [Tooltip("受击颜色：小怪为纯红，玩家可设为红白或浅金")]
    [SerializeField] private Color defaultFlashColor = new Color(1f, 0.15f, 0.15f, 1f);

    [Tooltip("不需要闪烁的子物体名称（如脚底阴影）")]
    [SerializeField] private List<string> ignoreNames = new List<string> { "Shadow", "shadow", "Marker" };

    private readonly List<SpriteRenderer> cachedRenderers = new List<SpriteRenderer>();
    private MaterialPropertyBlock mpb;
    private Coroutine flashRoutine;

    private static readonly int FlashAmountProp = Shader.PropertyToID("_FlashAmount");
    private static readonly int FlashColorProp = Shader.PropertyToID("_FlashColor");

    private void Awake()
    {
        mpb = new MaterialPropertyBlock();
        RefreshRenderers();
    }

    /// <summary>
    /// 自动缓存身上的所有 SpriteRenderer（支持多部件换装）
    /// </summary>
    public void RefreshRenderers()
    {
        cachedRenderers.Clear();
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);

        foreach (var sr in renderers)
        {
            if (ignoreNames.Exists(name => sr.gameObject.name.Contains(name)))
            {
                continue;
            }
            cachedRenderers.Add(sr);
        }
    }

    /// <summary>
    /// 触发受击闪烁
    /// </summary>
    /// <param name="customDuration">可选：自定义持续时长，不传则使用默认值</param>
    /// <param name="customColor">可选：自定义颜色（如暴击纯白、中毒暗绿）</param>
    public void TriggerFlash(float? customDuration = null, Color? customColor = null)
    {
        if (cachedRenderers.Count == 0) return;

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
        }

        flashRoutine = StartCoroutine(FlashCoroutine(
            customDuration ?? flashDuration,
            customColor ?? defaultFlashColor
        ));
    }

    private IEnumerator FlashCoroutine(float duration, Color color)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            // 归一化进度 [0, 1]
            float progress = Mathf.Clamp01(elapsed / duration);

            // 采样曲线获取当前帧的受击透明度
            float amount = flashCurve.Evaluate(progress);

            SetProperty(amount, color);
            yield return null;
        }

        // 确保彻底归零
        SetProperty(0f, color);
        flashRoutine = null;
    }

    private void SetProperty(float amount, Color color)
    {
        foreach (var sr in cachedRenderers)
        {
            if (sr == null) continue;
            sr.GetPropertyBlock(mpb);
            mpb.SetFloat(FlashAmountProp, amount);
            mpb.SetColor(FlashColorProp, color);
            sr.SetPropertyBlock(mpb);
        }
    }

    private void OnDisable()
    {
        SetProperty(0f, defaultFlashColor);
    }

    [ContextMenu("测试受击闪红 (Test Flash)")]
    private void TestFlashInEditor()
    {
        TriggerFlash();
    }
}