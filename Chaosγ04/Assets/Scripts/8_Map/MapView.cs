using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MapView : MonoBehaviour
{
    [Header("核心引用")]
    public MapGenerator mapGenerator;
    public RectTransform contentPanel;
    public GameObject nodePrefab;

    [Tooltip("用于画线的预制体 (一个简单的白色 UI Image 即可)")]
    public GameObject linePrefab;

    [Header("排布参数 (UI 坐标)")]
    public float horizontalSpacing = 150f;
    public float verticalSpacing = 200f;
    public float randomOffset = 20f;

    [Header("边距控制参数")]
    public float leftPadding = 100f;
    public float rightPadding = 100f;

    [Header("节点图标配置")]
    public Sprite minorEnemyIcon;
    public Sprite eliteEnemyIcon;
    public Sprite restSiteIcon;
    public Sprite shopIcon;
    public Sprite eventIcon;
    public Sprite bossIcon;

    private List<List<MapNode>> uiNodes = new List<List<MapNode>>();
    private List<GameObject> drawnLines = new List<GameObject>();

    private void Start()
    {
        if (mapGenerator != null && (mapGenerator.currentMapData == null || mapGenerator.currentMapData.floors.Count == 0))
        {
            mapGenerator.GenerateNewMap();
        }

        DrawMapUI();
        StartCoroutine(InstantCenterOnCurrentFloor());
    }

    public void DrawMapUI()
    {
        if (mapGenerator == null || mapGenerator.currentMapData == null) return;

        foreach (Transform child in contentPanel)
        {
            Destroy(child.gameObject);
        }
        uiNodes.Clear();
        drawnLines.Clear();

        MapData data = mapGenerator.currentMapData;

        float requiredWidth = leftPadding + (data.floors.Count - 1) * horizontalSpacing + rightPadding;
        contentPanel.sizeDelta = new Vector2(requiredWidth, contentPanel.sizeDelta.y);

        for (int floorIndex = 0; floorIndex < data.floors.Count; floorIndex++)
        {
            MapFloorData floorData = data.floors[floorIndex];
            List<MapNode> floorUINodes = new List<MapNode>();

            for (int nodeIndex = 0; nodeIndex < floorData.nodesInFloor.Count; nodeIndex++)
            {
                MapNodeData nodeData = floorData.nodesInFloor[nodeIndex];

                GameObject nodeObj = Instantiate(nodePrefab, contentPanel);
                nodeObj.name = $"Node_F{floorIndex}_I{nodeIndex}";

                float xPos = leftPadding + (floorIndex * horizontalSpacing);

                int totalNodesInThisFloor = floorData.nodesInFloor.Count;
                float totalHeight = (totalNodesInThisFloor - 1) * verticalSpacing;
                float startY = -totalHeight / 2f;
                float yPos = startY + (nodeIndex * verticalSpacing);

                if (floorIndex > 0 && floorIndex < data.floors.Count - 1)
                {
                    xPos += Random.Range(-randomOffset, randomOffset);
                    yPos += Random.Range(-randomOffset, randomOffset);
                }

                RectTransform rect = nodeObj.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0, 0.5f);
                rect.anchorMax = new Vector2(0, 0.5f);
                rect.anchoredPosition = new Vector2(xPos, yPos);

                MapNode mapNodeScript = nodeObj.GetComponent<MapNode>();
                if (mapNodeScript != null)
                {
                    mapNodeScript.floorIndex = floorIndex;
                    SetupNodeVisualAndTarget(mapNodeScript, nodeData.nodeType);
                    floorUINodes.Add(mapNodeScript);
                }
            }
            uiNodes.Add(floorUINodes);
        }

        DrawConnectionsAndRefreshState();
    }

    private IEnumerator InstantCenterOnCurrentFloor()
    {
        yield return new WaitForEndOfFrame();

        if (RunDataManager.Instance == null || contentPanel == null) yield break;

        int currentFloor = RunDataManager.Instance.currentFloor;

        if (currentFloor <= 0) yield break;

        float targetXPos = leftPadding + (currentFloor * horizontalSpacing);

        RectTransform viewport = contentPanel.parent.GetComponent<RectTransform>();
        float viewportWidth = viewport != null ? viewport.rect.width : 800f;

        float desiredContentX = -(targetXPos - (viewportWidth / 2f));

        float maxScrollX = 0f;
        float minScrollX = -(contentPanel.rect.width - viewportWidth);
        if (minScrollX > 0) minScrollX = 0;

        desiredContentX = Mathf.Clamp(desiredContentX, minScrollX, maxScrollX);

        contentPanel.anchoredPosition = new Vector2(desiredContentX, contentPanel.anchoredPosition.y);
    }

    private void DrawConnectionsAndRefreshState()
    {
        MapData data = mapGenerator.currentMapData;

        for (int f = 0; f < data.floors.Count - 1; f++)
        {
            for (int i = 0; i < data.floors[f].nodesInFloor.Count; i++)
            {
                MapNodeData nodeData = data.floors[f].nodesInFloor[i];
                MapNode currentUINode = uiNodes[f][i];

                foreach (Vector2Int targetPos in nodeData.outgoingPaths)
                {
                    MapNode targetUINode = uiNodes[targetPos.x][targetPos.y];

                    if (!currentUINode.nextNodes.Contains(targetUINode))
                    {
                        currentUINode.nextNodes.Add(targetUINode);
                    }

                    DrawLineBetweenNodes(currentUINode.GetComponent<RectTransform>(), targetUINode.GetComponent<RectTransform>());
                }
            }
        }

        foreach (var floorNodes in uiNodes)
        {
            foreach (var node in floorNodes)
            {
                node.transform.SetAsLastSibling();
            }
        }

        RefreshNodesState();
    }

    private void DrawLineBetweenNodes(RectTransform startRect, RectTransform endRect)
    {
        if (linePrefab == null) return;

        GameObject lineObj = Instantiate(linePrefab, contentPanel);
        drawnLines.Add(lineObj);
        RectTransform lineRect = lineObj.GetComponent<RectTransform>();

        lineRect.anchorMin = new Vector2(0, 0.5f);
        lineRect.anchorMax = new Vector2(0, 0.5f);

        Vector2 startPos = startRect.anchoredPosition;
        Vector2 endPos = endRect.anchoredPosition;

        Vector2 middlePoint = (startPos + endPos) / 2f;
        lineRect.anchoredPosition = middlePoint;

        float distance = Vector2.Distance(startPos, endPos);
        lineRect.sizeDelta = new Vector2(distance, 5f);

        Vector2 direction = endPos - startPos;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        lineRect.localRotation = Quaternion.Euler(0, 0, angle);
    }

    private void RefreshNodesState()
    {
        if (RunDataManager.Instance == null) return;

        int currentFloor = RunDataManager.Instance.currentFloor;
        List<string> availableNext = RunDataManager.Instance.availableNextNodeNames;
        List<string> visited = RunDataManager.Instance.visitedNodeNames;

        foreach (var floorNodes in uiNodes)
        {
            foreach (var node in floorNodes)
            {
                string nodeName = node.gameObject.name;

                node.isVisited = visited.Contains(nodeName);

                if (currentFloor == -1)
                {
                    node.SetInteractable(node.floorIndex == 0);
                }
                else
                {
                    bool canClick = availableNext.Contains(nodeName) && !node.isVisited;
                    node.SetInteractable(canClick);
                }
            }
        }
    }

    private void SetupNodeVisualAndTarget(MapNode nodeScript, MapNodeType type)
    {
        nodeScript.myNodeType = type;

        // ==========================================
        // 【核心修改】：通过 MapNode.cs 提供的 nodeIcon 精准获取子物体的贴图
        // 彻底切断与 Background 的瓜葛
        // ==========================================
        Image img = nodeScript.nodeIcon;

        SceneLoader loader = nodeScript.GetComponent<SceneLoader>();
        UnityEngine.UI.Text uiText = nodeScript.GetComponentInChildren<UnityEngine.UI.Text>();
        TMPro.TextMeshProUGUI tmpText = nodeScript.GetComponentInChildren<TMPro.TextMeshProUGUI>();

        string nodeNameText = "";

        switch (type)
        {
            case MapNodeType.MinorEnemy:
                nodeNameText = "普通战斗";
                if (img != null) img.sprite = minorEnemyIcon;
                if (loader != null) loader.targetSceneName = "2_Battle_Scene";
                break;

            case MapNodeType.EliteEnemy:
                nodeNameText = "精英战斗";
                if (img != null) img.sprite = eliteEnemyIcon;
                if (loader != null) loader.targetSceneName = "2_Battle_Scene";
                break;

            case MapNodeType.Shop:
                nodeNameText = "商店";
                if (img != null) img.sprite = shopIcon;
                if (loader != null) loader.targetSceneName = "2_Battle_Scene";
                break;

            case MapNodeType.Event:
                if (nodeScript.floorIndex == 0)
                {
                    nodeNameText = "起点";
                }
                else
                {
                    nodeNameText = "未知事件";
                }
                if (img != null) img.sprite = eventIcon;
                if (loader != null) loader.targetSceneName = "2_Battle_Scene";
                break;

            case MapNodeType.Boss:
                nodeNameText = "BOSS战";
                if (img != null) img.sprite = bossIcon;
                if (loader != null) loader.targetSceneName = "2_Battle_Scene";
                break;
        }

        if (uiText != null)
        {
            uiText.text = nodeNameText;
        }
        else if (tmpText != null)
        {
            tmpText.text = nodeNameText;
        }
    }
}