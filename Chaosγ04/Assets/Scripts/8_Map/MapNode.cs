using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 大地图节点逻辑组件（需挂载到节点预制体上）
/// </summary>
public class MapNode : MonoBehaviour
{
    [Header("节点坐标与状态")]
    [Tooltip("该节点所在的层级 (Y轴)")]
    public int floorIndex;

    [Tooltip("该节点的类型（Boss, 小怪, 事件等）")]
    public MapNodeType myNodeType;

    [Tooltip("该节点是否已经被玩家踩过（用于显示历史路径）")]
    public bool isVisited = false;

    [Header("路线连线 (由 MapView 自动注入)")]
    [Tooltip("从这个节点出发，能走到下一层的哪些节点？")]
    public List<MapNode> nextNodes = new List<MapNode>();

    [Header("组件引用")]
    public Button nodeButton;
    public Image nodeIcon;
    public SceneLoader sceneLoader;

    [Header("大地图节点状态颜色配置")]
    public Color normalColor = Color.white;
    public Color lockedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
    public Color visitedColor = Color.gray;

    private void Awake()
    {
        if (nodeButton == null) nodeButton = GetComponent<Button>();

        // ==========================================
        // 【核心修改】：代码自动去子层级里找名字叫 "Icon" 的物体
        // 绝对不会再误拿 Background 的 Image 组件了
        // ==========================================
        if (nodeIcon == null)
        {
            Transform iconTransform = transform.Find("Icon");
            if (iconTransform != null)
            {
                nodeIcon = iconTransform.GetComponent<Image>();
            }
            else
            {
                // 保底逻辑
                nodeIcon = GetComponent<Image>();
            }
        }

        if (sceneLoader == null) sceneLoader = GetComponent<SceneLoader>();

        if (nodeButton != null)
        {
            nodeButton.onClick.RemoveAllListeners();
            nodeButton.onClick.AddListener(OnNodeClicked);
        }
    }

    public void SetInteractable(bool canClick)
    {
        if (nodeButton != null) nodeButton.interactable = canClick;

        if (nodeIcon != null)
        {
            if (isVisited) nodeIcon.color = visitedColor;
            else if (canClick) nodeIcon.color = normalColor;
            else nodeIcon.color = lockedColor;
        }
    }

    private void OnNodeClicked()
    {
        Debug.Log($"[MapNode] 玩家点击了节点：第 {floorIndex} 层，准备跳转场景...");

        if (RunDataManager.Instance != null)
        {
            RunDataManager.Instance.currentFloor = floorIndex;
            RunDataManager.Instance.currentNodeType = myNodeType;

            isVisited = true;
            RunDataManager.Instance.AddVisitedNode(this.gameObject.name);
            RunDataManager.Instance.SaveAvailableNextNodes(nextNodes);
        }
        else
        {
            Debug.LogWarning("[MapNode] 当前场景未找到 RunDataManager，地图进度将不会被保存！");
        }

        if (sceneLoader != null)
        {
            sceneLoader.LoadTargetScene();
        }
    }
}