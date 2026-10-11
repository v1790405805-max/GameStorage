using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class ActionPointBarUI : MonoBehaviour
{
    [Header("预制体与容器")]
    [Tooltip("单个灯珠预制体（AP_Pip）")]
    [SerializeField] private GameObject pipPrefab;

    [Tooltip("生成灯珠的父容器。若留空则使用自身")]
    [SerializeField] private Transform pipsContainer;

    [Header("程序化底座与导轨联动")]
    [Tooltip("深色托盘底座（挂有 ArcTrayGraphic 的子物体）")]
    [SerializeField] private ArcTrayGraphic trayBackground;

    [Tooltip("可选：中轴导轨细金线（挂有 ArcTrayGraphic 的子物体）")]
    [SerializeField] private ArcTrayGraphic trackLine;

    [Header("极坐标几何排布参数")]
    [Tooltip("灯珠距离头像圆心的半径距离（像素）")]
    [SerializeField] private float radius = 100f;

    [Tooltip("起始角度（顺时针/度数，第四象限一般在 -35 左右）")]
    [SerializeField] private float startAngle = -35f;

    [Tooltip("结束角度（第四象限底部一般在 -85 左右）")]
    [SerializeField] private float endAngle = -85f;

    private readonly List<ActionPointPipUI> spawnedPips = new List<ActionPointPipUI>();
    private int cachedMaxAP = -1;

    private void Awake()
    {
        if (pipsContainer == null) pipsContainer = transform;
    }

    private void Start()
    {
        SyncArcGraphics();

        if (CombatStatsManager.Instance != null)
        {
            CombatStatsManager.Instance.OnStatsChanged += RefreshAPDisplay;
            RefreshAPDisplay();
        }
    }

    private void OnDestroy()
    {
        if (CombatStatsManager.Instance != null)
        {
            CombatStatsManager.Instance.OnStatsChanged -= RefreshAPDisplay;
        }
    }

    public void RefreshAPDisplay()
    {
        if (CombatStatsManager.Instance == null || pipPrefab == null) return;

        int current = CombatStatsManager.Instance.currentActionPoint;
        int max = CombatStatsManager.Instance.maxActionPoint;

        if (max != cachedMaxAP || spawnedPips.Count != max)
        {
            RebuildPips(max);
        }

        for (int i = 0; i < spawnedPips.Count; i++)
        {
            bool shouldBeLit = i < current;
            spawnedPips[i].SetState(shouldBeLit, animate: true);
        }
    }

    private void RebuildPips(int maxCount)
    {
        cachedMaxAP = maxCount;

        foreach (var pip in spawnedPips)
        {
            if (pip != null) Destroy(pip.gameObject);
        }
        spawnedPips.Clear();

        SyncArcGraphics();

        if (maxCount <= 0) return;

        for (int i = 0; i < maxCount; i++)
        {
            GameObject pipObj = Instantiate(pipPrefab, pipsContainer);
            RectTransform rect = pipObj.GetComponent<RectTransform>();

            float angleDeg = maxCount > 1
                ? Mathf.Lerp(startAngle, endAngle, (float)i / (maxCount - 1))
                : (startAngle + endAngle) * 0.5f;

            float angleRad = angleDeg * Mathf.Deg2Rad;

            float x = radius * Mathf.Cos(angleRad);
            float y = radius * Mathf.Sin(angleRad);

            rect.anchoredPosition = new Vector2(x, y);
            rect.localEulerAngles = new Vector3(0f, 0f, angleDeg + 90f);

            ActionPointPipUI pipUI = pipObj.GetComponent<ActionPointPipUI>();
            if (pipUI != null)
            {
                spawnedPips.Add(pipUI);
            }
        }
    }

    private void SyncArcGraphics()
    {
        if (trayBackground != null)
            trayBackground.SetArcParameters(radius, startAngle, endAngle);

        if (trackLine != null)
            trackLine.SetArcParameters(radius, startAngle, endAngle);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        SyncArcGraphics();
    }
#endif
}