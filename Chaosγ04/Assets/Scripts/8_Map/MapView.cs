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

    [Header("通关结算弹窗 (可选)")]
    [Tooltip("如果你在大地图场景做了一个全屏通关结算弹窗，拖到这里；如果没有可留空")]
    public GameObject victorySummaryPanel;
    [Tooltip("通关弹窗里的重新开始/返回主菜单按钮")]
    public Button restartRunButton;

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

    [Header("--- 场景跳转配置 (支持多场景组随机筛选) ---")]
    [Tooltip("商店对应的场景名称")]
    public string shopSceneName = "3_Shop_Scene";

    [Tooltip("随机事件场景池（有多个事件场景时在此追加，生成时随机筛选）")]
    public List<string> eventScenes = new List<string>() { "4_Event_Scene" };

    [Tooltip("普通战斗地图池（若后期有多张小怪地图，全填在这里，随机抽取）")]
    public List<string> minorEnemyScenes = new List<string>() { "2_Battle_Scene" };

    [Tooltip("精英怪地图池（若有专属精英战地图填在这里）")]
    public List<string> eliteEnemyScenes = new List<string>() { "2_Battle_Scene" };

    [Tooltip("Boss 关地图池")]
    public List<string> bossScenes = new List<string>() { "2_Battle_Scene" };

    private List<List<MapNode>> uiNodes = new List<List<MapNode>>();
    private List<GameObject> drawnLines = new List<GameObject>();

    private void Start()
    {
        // ==========================================
        // 【核心新增】：检测是否刚刚击败了 Boss 通关回来
        // ==========================================
        CheckBossVictorySummary();

        if (mapGenerator != null && (mapGenerator.currentMapData == null || mapGenerator.currentMapData.floors.Count == 0))
        {
            mapGenerator.GenerateNewMap();
        }

        DrawMapUI();
        StartCoroutine(InstantCenterOnCurrentFloor());
    }

    /// <summary>
    /// 处理打死 Boss 回到大地图后的通关结算流程
    /// </summary>
    private void CheckBossVictorySummary()
    {
        if (RunDataManager.Instance != null && RunDataManager.Instance.pendingVictorySummary)
        {
            // 消耗掉通关标记，防止下次反复触发
            RunDataManager.Instance.pendingVictorySummary = false;

            Debug.Log($"<color=yellow>【通关大获全胜！】总击杀数: {RunDataManager.Instance.totalKills}, 总造成伤害: {RunDataManager.Instance.totalDamageDealt}</color>");

            // 如果你在大地图 Canvas 里拖了通关弹窗，就激活弹窗
            if (victorySummaryPanel != null)
            {
                victorySummaryPanel.SetActive(true);

                if (restartRunButton != null)
                {
                    restartRunButton.onClick.RemoveAllListeners();
                    restartRunButton.onClick.AddListener(() =>
                    {
                        // 清理单局存档并回到主菜单或新开一局
                        RunDataManager.Instance.ResetRunData();
                        UnityEngine.SceneManagement.SceneManager.LoadScene("0_MainMenu_Scene");
                    });
                }
            }
        }
        else
        {
            // 平常状态隐藏通关弹窗
            if (victorySummaryPanel != null)
            {
                victorySummaryPanel.SetActive(false);
            }
        }
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

        Image img = nodeScript.nodeIcon;
        SceneLoader loader = nodeScript.GetComponent<SceneLoader>();
        UnityEngine.UI.Text uiText = nodeScript.GetComponentInChildren<UnityEngine.UI.Text>();
        TMPro.TextMeshProUGUI tmpText = nodeScript.GetComponentInChildren<TMPro.TextMeshProUGUI>();

        string nodeNameText = "";
        string targetScene = "2_Battle_Scene";

        switch (type)
        {
            case MapNodeType.MinorEnemy:
                nodeNameText = "普通战斗";
                if (img != null) img.sprite = minorEnemyIcon;
                targetScene = GetRandomSceneFromList(minorEnemyScenes, "2_Battle_Scene");
                break;

            case MapNodeType.EliteEnemy:
                nodeNameText = "精英战斗";
                if (img != null) img.sprite = eliteEnemyIcon;
                targetScene = GetRandomSceneFromList(eliteEnemyScenes, "2_Battle_Scene");
                break;

            case MapNodeType.Shop:
                nodeNameText = "商店";
                if (img != null) img.sprite = shopIcon;
                targetScene = shopSceneName;
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
                targetScene = GetRandomSceneFromList(eventScenes, "4_Event_Scene");
                break;

            case MapNodeType.Boss:
                nodeNameText = "BOSS战";
                if (img != null) img.sprite = bossIcon;
                targetScene = GetRandomSceneFromList(bossScenes, "2_Battle_Scene");
                break;
        }

        if (loader != null)
        {
            loader.targetSceneName = targetScene;
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

    private string GetRandomSceneFromList(List<string> sceneList, string fallbackScene)
    {
        if (sceneList != null && sceneList.Count > 0)
        {
            int randIndex = Random.Range(0, sceneList.Count);
            if (!string.IsNullOrEmpty(sceneList[randIndex]))
            {
                return sceneList[randIndex];
            }
        }
        return fallbackScene;
    }
}