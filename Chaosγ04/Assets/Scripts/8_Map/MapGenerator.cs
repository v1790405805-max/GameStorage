using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 地图节点的类型枚举
/// </summary>
[System.Serializable]
public enum MapNodeType
{
    MinorEnemy,  // 普通小怪
    EliteEnemy,  // 精英怪
    Shop,        // 商店
    Event,       // 随机事件
    Boss         // 关底 Boss
}

/// <summary>
/// 纯数据类：描述地图上的一个节点（图纸上的一个点）
/// </summary>
[System.Serializable]
public class MapNodeData
{
    public int floor;
    public int index;
    public MapNodeType nodeType;
    public List<Vector2Int> outgoingPaths;

    public MapNodeData(int f, int i)
    {
        floor = f;
        index = i;
        outgoingPaths = new List<Vector2Int>();
        nodeType = MapNodeType.MinorEnemy;
    }
}

/// <summary>
/// 【新增】用于包装每一层的节点列表，这样 Unity Inspector 就能正常显示了
/// </summary>
[System.Serializable]
public class MapFloorData
{
    public List<MapNodeData> nodesInFloor = new List<MapNodeData>();
}

/// <summary>
/// 纯数据类：描述整张地图的数据集合
/// </summary>
[System.Serializable]
public class MapData
{
    // 【修改】把 List<List<...>> 改成 List<MapFloorData>
    public List<MapFloorData> floors;

    public MapData()
    {
        floors = new List<MapFloorData>();
    }
}

/// <summary>
/// 地图生成器核心引擎
/// </summary>
public class MapGenerator : MonoBehaviour
{
    [Header("地图生成参数")]
    public int totalFloors = 15;        // 总层数
    public int minNodesPerFloor = 2;    // 每层最少节点数
    public int maxNodesPerFloor = 4;    // 每层最多节点数

    [Header("生成的图纸（供外部读取）")]
    public MapData currentMapData;

    // ... 前面的枚举和数据类保持不变 ...

    private void Start()
    {
        // 【修改】游戏启动时，不再无脑生成新地图，而是先检查是否有存档
        if (RunDataManager.Instance != null && RunDataManager.Instance.savedMapData != null)
        {
            // 发现保险箱里有图纸（说明是从战斗场景回来的）
            Debug.Log("[MapGenerator] 检测到现存的地图图纸，直接加载存档！");
            currentMapData = RunDataManager.Instance.savedMapData;
        }
        else
        {
            // 没找到图纸（说明是刚开启新一局），生成一张全新的
            GenerateNewMap();
        }
    }

    public void GenerateNewMap()
    {
        currentMapData = new MapData();

        GenerateNodes();
        GeneratePaths();
        AssignNodeTypes();

        Debug.Log($"[MapGenerator] 成功生成一张 {totalFloors} 层的地图图纸！");

        // 【新增】生成完新图纸后，立刻把它复印一份放进 RunDataManager 的保险箱里！
        if (RunDataManager.Instance != null)
        {
            RunDataManager.Instance.savedMapData = currentMapData;
        }
    }

    // ... 后面的 GenerateNodes, GeneratePaths, AssignNodeTypes 方法全部保持不变 ...

    private void GenerateNodes()
    {
        for (int f = 0; f < totalFloors; f++)
        {
            MapFloorData currentFloor = new MapFloorData();

            // 【修改核心】
            // 如果是第 0 层（起点）或者最后一层（Boss），只生成 1 个节点
            if (f == 0 || f == totalFloors - 1)
            {
                currentFloor.nodesInFloor.Add(new MapNodeData(f, 0));
            }
            else
            {
                // 中间的层级，按设定的最大最小值随机生成节点数量
                int nodeCount = Random.Range(minNodesPerFloor, maxNodesPerFloor + 1);
                for (int i = 0; i < nodeCount; i++)
                {
                    currentFloor.nodesInFloor.Add(new MapNodeData(f, i));
                }
            }
            currentMapData.floors.Add(currentFloor);
        }
    }

    private void GeneratePaths()
    {
        for (int f = 0; f < totalFloors - 1; f++)
        {
            // 【修改】通过 nodesInFloor 访问
            List<MapNodeData> currentFloor = currentMapData.floors[f].nodesInFloor;
            List<MapNodeData> nextFloor = currentMapData.floors[f + 1].nodesInFloor;

            for (int nextIndex = 0; nextIndex < nextFloor.Count; nextIndex++)
            {
                int closestCurrentIndex = Mathf.Clamp(nextIndex, 0, currentFloor.Count - 1);
                currentFloor[closestCurrentIndex].outgoingPaths.Add(new Vector2Int(f + 1, nextIndex));
            }

            for (int currIndex = 0; currIndex < currentFloor.Count; currIndex++)
            {
                if (currentFloor[currIndex].outgoingPaths.Count == 0)
                {
                    int targetNextIndex = Mathf.Clamp(currIndex, 0, nextFloor.Count - 1);
                    currentFloor[currIndex].outgoingPaths.Add(new Vector2Int(f + 1, targetNextIndex));
                }
            }
        }
    }

    private void AssignNodeTypes()
    {
        for (int f = 0; f < totalFloors; f++)
        {
            List<MapNodeData> currentFloorNodes = currentMapData.floors[f].nodesInFloor;

            foreach (var node in currentFloorNodes)
            {
                // 【修改这里】强制第 0 层为事件节点（Event）
                if (f == 0)
                {
                    node.nodeType = MapNodeType.Event;
                    continue;
                }
                // 最后一层强制为 Boss
                if (f == totalFloors - 1)
                {
                    node.nodeType = MapNodeType.Boss;
                    continue;
                }

                // 其他层随机分配
                node.nodeType = GetRandomNodeType();
            }
        }
    }

    private MapNodeType GetRandomNodeType()
    {
        float roll = Random.value;
        if (roll < 0.45f) return MapNodeType.MinorEnemy;
        if (roll < 0.65f) return MapNodeType.Event;
        if (roll < 0.80f) return MapNodeType.EliteEnemy;
        return MapNodeType.Shop;
    }
}