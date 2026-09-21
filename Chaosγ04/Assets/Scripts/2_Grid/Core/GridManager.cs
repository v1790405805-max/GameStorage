using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class GridManager : MonoBehaviour
{
    [Header("棋盘格数")]
    public int width = 8;
    public int height = 8;

    [Header("格子尺寸 (米)")]
    public float cellWidth = 1f;
    [FormerlySerializedAs("cellHeight")] // 旧版 cellHeight（格子纵深）迁移到 cellLength
    public float cellLength = 1f;

    [Header("层高 (米)：铺设完成后按此丈量层号 (0~1 格高 = L1，1~2 格高 = L2 ...)")]
    public float cellHeight = 1f;

    [Header("格子间距 (米)")]
    public float cellGap = 0.1f;

    [Header("格子 [Cell] 预制体引用")]
    public GameObject cellPrefab;

    [Header("格子 [Cell] 相对逻辑中心的坐标偏移值")]
    public Vector3 cellOffset = new Vector3(0.007f, 0.01f, 0.007f);

    [Header("地形贴合 (Map)")]
    public GameObject mapObject;
    [Tooltip("贴合后整体抬升量，防止与地表穿模 / Z-fighting")]
    public float terrainFitOffset = 0.02f;
    public bool enableTerrainFit = true;
    [Tooltip("射线向下穿透时最多检测的表面层数（同一 XZ 下会为每一层表面都铺设格子）")]
    public int maxSurfaceLayers = 8;

    [Header("格子 [Cell] 的默认颜色设置")]
    public Color cellNormalColor = new Color(1f, 1f, 1f, 0f);       // 常态颜色 (0%透明度白色)
    public Color lineNormalColor = new Color(1f, 1f, 1f, 0.1f);     // 默认线框颜色 (10%透明度白色)
    public float lineWidth = 0.05f;                                  // 线条宽度

    [HideInInspector]
    public int cellLayer = 0; // 取消 Header 标记，由 Custom Editor 统一接管渲染位置

    [HideInInspector]
    [SerializeField] private GameObject gridRoot;

    private GridSystem gridSystem;
    /// <summary>
    /// 每列 (x, z) 的格子列表：索引 0 为最顶层表面，向下逐层增加。
    /// 与 GetCellManagerAt(x, z) 的兼容层：该方法仍返回顶层格子。
    /// </summary>
    private List<CellManager>[,] cellManagers;

    private void Start()
    {
        InitGridSystem();

        if (Application.isPlaying)
        {
            RebuildReferencesFromHierarchy();
        }
    }

    private void InitGridSystem()
    {
        gridSystem = new GridSystem(width, height, cellWidth, cellLength, cellGap, cellHeight, transform.position);
    }

    public void EnsureGridSystemInitialized()
    {
        if (gridSystem == null) InitGridSystem();
    }

    public (int, int) GetGridPosition(Vector3 position)
    {
        return gridSystem.GetGridPosition(position);
    }

    public bool IsValidGridPosition(int x, int z)
    {
        return gridSystem != null && gridSystem.IsValidGridPosition(x, z);
    }

    private void OnValidate()
    {
        if (Application.isPlaying) return;

        if (cellManagers == null || cellManagers.GetLength(0) != width || cellManagers.GetLength(1) != height)
        {
            RebuildReferencesFromHierarchy();
        }

        if (cellManagers == null) return;

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                List<CellManager> column = cellManagers[x, z];
                if (column == null) continue;

                foreach (CellManager cellMgr in column)
                {
                    if (cellMgr == null) continue;

                    cellMgr.SetCellColor(cellNormalColor, isRuntime: false);
                    cellMgr.SetLineColor(lineNormalColor);
                    cellMgr.SetLineWidth(lineWidth);
                }
            }
        }
    }

    /// <summary>
    /// 生成格子的命名规则：按实际铺设高度用层高 (cellHeight) 丈量层号，
    /// 0~1 格高 = L1，1~2 格高 = L2，以此类推；不再使用表面序号（第一层/第二层）方式。
    /// 命名格式：Cell_{z+1}_{x+1}_L{层号}（例如 Cell_2_10_L1）。
    /// </summary>
    private string GetCellName(int x, int z, Vector3 cellPhysicsPosition)
    {
        // 以网格逻辑平面（GridManager 自身高度）为基准丈量实际铺设高度
        float logicalHeight = cellPhysicsPosition.y - transform.position.y;
        int layerNumber = gridSystem.GetLayerNumberFromHeight(logicalHeight);
        return $"Cell_{z + 1}_{x + 1}_L{layerNumber}";
    }

    // ------------------------------------------------------------------
    // 层号 (L) 划分：先把整张地图的格子全部测量/摆放完，再从最低的层高往上归类，
    // 判断每个格子属于哪一层。保证「同一层号 = 同一层高」（最低的那层为 L1）。
    // ------------------------------------------------------------------

    /// <summary>本张地图实际存在的层高（相对 GridManager 逻辑平面，自下而上排序）。</summary>
    private List<float> layerHeights = new List<float>();

    /// <summary>
    /// 全部格子测量并摆放完成后调用：收集所有格子的实际高度，从最低往上归类成"层"，
    /// 再按所属层重新命名。这样层号不再依赖 GridManager 的摆放高度、也不要求
    /// cellHeight 与地图实际层距完全吻合（cellHeight 只作为"还算同一层"的合并容差）。
    /// </summary>
    private void RebuildLayerNames()
    {
        if (cellManagers == null) return;

        List<float> measuredHeights = new List<float>();
        for (int x = 0; x < cellManagers.GetLength(0); x++)
        {
            for (int z = 0; z < cellManagers.GetLength(1); z++)
            {
                List<CellManager> column = cellManagers[x, z];
                if (column == null) continue;

                foreach (CellManager cellMgr in column)
                {
                    if (cellMgr == null) continue;
                    measuredHeights.Add(cellMgr.transform.position.y - transform.position.y);
                }
            }
        }

        layerHeights = BuildLayerHeights(measuredHeights);

        for (int x = 0; x < cellManagers.GetLength(0); x++)
        {
            for (int z = 0; z < cellManagers.GetLength(1); z++)
            {
                List<CellManager> column = cellManagers[x, z];
                if (column == null) continue;

                foreach (CellManager cellMgr in column)
                {
                    if (cellMgr == null) continue;

                    float logicalHeight = cellMgr.transform.position.y - transform.position.y;
                    int layerNumber = GetLayerNumberByMeasuredHeights(logicalHeight);
                    string newName = $"Cell_{z + 1}_{x + 1}_L{layerNumber}";
                    if (cellMgr.gameObject.name == newName) continue;

                    cellMgr.gameObject.name = newName;
#if UNITY_EDITOR
                    if (!Application.isPlaying) EditorUtility.SetDirty(cellMgr.gameObject);
#endif
                }
            }
        }
    }

    /// <summary>
    /// 把测得的全部高度从小到大归类成层：与上一层代表高度的差值超过容差（层高的一半）
    /// 就视为新的一层。返回每一层的代表高度（自下而上），最低的那层即 L1。
    /// </summary>
    private List<float> BuildLayerHeights(List<float> measuredHeights)
    {
        List<float> result = new List<float>();
        if (measuredHeights == null || measuredHeights.Count == 0) return result;

        measuredHeights.Sort();

        float tolerance = Mathf.Max(0.01f, Mathf.Abs(cellHeight) * 0.5f);

        result.Add(measuredHeights[0]);
        for (int i = 1; i < measuredHeights.Count; i++)
        {
            if (measuredHeights[i] - result[result.Count - 1] > tolerance)
            {
                result.Add(measuredHeights[i]);
            }
        }

        return result;
    }

    /// <summary>返回某高度所属的层号：与哪一层的代表高度最接近就属于哪一层，最低层为 L1。</summary>
    private int GetLayerNumberByMeasuredHeights(float logicalHeight)
    {
        if (layerHeights == null || layerHeights.Count == 0) return 1;

        int nearestLayerIndex = 0;
        float nearestDelta = Mathf.Abs(logicalHeight - layerHeights[0]);

        for (int i = 1; i < layerHeights.Count; i++)
        {
            float delta = Mathf.Abs(logicalHeight - layerHeights[i]);
            if (delta < nearestDelta)
            {
                nearestDelta = delta;
                nearestLayerIndex = i;
            }
        }

        return nearestLayerIndex + 1;
    }

    public void GenerateGridToHierarchy()
    {
        if (cellPrefab == null)
        {
            Debug.LogError("[GridManager] 未关联 Cell Prefab！");
            return;
        }

        ClearGridFromHierarchy();
        InitGridSystem();

        warnedAboutMiss = false;
        mapCollider = null;
        mapColliders = null;
        if (enableTerrainFit && mapObject != null) Physics.SyncTransforms();

        gridRoot = new GameObject("Grid_Root");
        gridRoot.transform.SetParent(this.transform);
        gridRoot.transform.localPosition = Vector3.zero;
        gridRoot.transform.localRotation = Quaternion.identity;

#if UNITY_EDITOR
        Undo.RegisterCreatedObjectUndo(gridRoot, "Generate Grid");
#endif

        cellManagers = new List<CellManager>[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                cellManagers[x, z] = new List<CellManager>();

                Vector3 cellLogicCenter = gridSystem.GetWorldPosition(x, z);
                List<Vector3> cellPhysicsPositions = GetFittedPhysicsPositions(cellLogicCenter);

                // 同一 (x, z) 下穿透所有朝上表面，为每一层都铺设一个格子
                for (int layer = 0; layer < cellPhysicsPositions.Count; layer++)
                {
                    CellManager cellMgr = CreateCellInstance(x, z, cellPhysicsPositions[layer]);
                    cellManagers[x, z].Add(cellMgr);
                }
            }
        }

        // 全部格子测量/摆放完成后，再按实测层高统一划分层号
        RebuildLayerNames();

#if UNITY_EDITOR
        EditorUtility.SetDirty(gameObject);
        if (gridRoot != null) EditorUtility.SetDirty(gridRoot);
#endif
    }

    /// <summary>
    /// 创建单个格子实例并完成初始化（生成与 Refit 共用）。
    /// </summary>
    private CellManager CreateCellInstance(int x, int z, Vector3 cellPhysicsPosition)
    {
        Quaternion flatRotation = Quaternion.Euler(90f, 0f, 0f);

        GameObject cellInstance;
#if UNITY_EDITOR
        cellInstance = PrefabUtility.InstantiatePrefab(cellPrefab) as GameObject;
        cellInstance.transform.position = cellPhysicsPosition;
        cellInstance.transform.rotation = flatRotation;
        cellInstance.transform.SetParent(gridRoot.transform);
        Undo.RegisterCreatedObjectUndo(cellInstance, "Generate Grid");
#else
        cellInstance = Instantiate(cellPrefab, cellPhysicsPosition, flatRotation, gridRoot.transform);
#endif
        cellInstance.name = GetCellName(x, z, cellPhysicsPosition);
        cellInstance.transform.localScale = new Vector3(cellWidth, cellLength, 1f);

        SetLayerRecursively(cellInstance, cellLayer);

        CellManager cellMgr = cellInstance.GetComponent<CellManager>();
        if (cellMgr == null)
        {
            cellMgr = cellInstance.AddComponent<CellManager>();
        }

        cellMgr.SetCellColor(cellNormalColor, isRuntime: false);

        Collider cellCollider = cellInstance.GetComponent<Collider>();
        if (cellCollider != null)
        {
            cellCollider.isTrigger = true;
        }

        Transform oldBorder = cellInstance.transform.Find("Cell_Border");
        if (oldBorder != null) DestroyImmediate(oldBorder.gameObject);

        CreateFourLineRenderersForCell(x, z, cellPhysicsPosition, cellMgr);

        return cellMgr;
    }

    public void ClearGridFromHierarchy()
    {
        if (gridRoot != null)
        {
#if UNITY_EDITOR
            Undo.DestroyObjectImmediate(gridRoot);
#else
            DestroyImmediate(gridRoot);
#endif
            gridRoot = null;
        }

        Transform residual = transform.Find("Grid_Root");
        if (residual != null)
        {
#if UNITY_EDITOR
            Undo.DestroyObjectImmediate(residual.gameObject);
#else
            DestroyImmediate(residual.gameObject);
#endif
        }

        cellManagers = null;
    }

    private void RebuildReferencesFromHierarchy()
    {
        cellManagers = new List<CellManager>[width, height];

        if (gridRoot == null)
        {
            Transform t = transform.Find("Grid_Root");
            if (t != null) gridRoot = t.gameObject;
        }

        if (gridRoot == null) return;

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                cellManagers[x, z] = new List<CellManager>();
            }
        }

        // 扫描 Grid_Root 下所有格子，解析名字 Cell_{z+1}_{x+1}[_L{层号}]
        // 兼容新旧命名；按实际世界高度从高到低排序（索引 0 = 最顶层）
        foreach (Transform cellTransform in gridRoot.transform)
        {
            if (cellTransform == null) continue;

            string[] parts = cellTransform.name.Split('_');
            if (parts.Length < 3 || parts[0] != "Cell") continue;
            if (!int.TryParse(parts[1], out int zPlusOne)) continue;
            if (!int.TryParse(parts[2], out int xPlusOne)) continue;

            int x = xPlusOne - 1;
            int z = zPlusOne - 1;
            if (x < 0 || x >= width || z < 0 || z >= height) continue;

            CellManager cellMgr = cellTransform.GetComponent<CellManager>();
            if (cellMgr == null) continue;

            cellManagers[x, z].Add(cellMgr);
            cellMgr.CacheLineRenderers();
        }

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                cellManagers[x, z].Sort((a, b) => b.transform.position.y.CompareTo(a.transform.position.y));
            }
        }
    }

    private void CreateFourLineRenderersForCell(int x, int z, Vector3 cellSurfacePosition, CellManager cellMgr)
    {
        float hWidth = cellWidth * 0.5f;
        float hLength = cellLength * 0.5f;
        // 边框线与格子同平面：表面高度 + 极小偏移，避免与格子面片 Z-fighting
        float lineY = cellSurfacePosition.y + 0.002f;

        Vector3 leftDown = new Vector3(cellSurfacePosition.x - hWidth, lineY, cellSurfacePosition.z - hLength);
        Vector3 leftUp = new Vector3(cellSurfacePosition.x - hWidth, lineY, cellSurfacePosition.z + hLength);
        Vector3 rightUp = new Vector3(cellSurfacePosition.x + hWidth, lineY, cellSurfacePosition.z + hLength);
        Vector3 rightDown = new Vector3(cellSurfacePosition.x + hWidth, lineY, cellSurfacePosition.z - hLength);

        System.Action<string, Vector3, Vector3> createSingleLine = (name, start, end) =>
        {
            GameObject lineObj = new GameObject(name);
            lineObj.transform.SetParent(cellMgr.transform);
            lineObj.transform.localPosition = Vector3.zero;

            lineObj.layer = cellLayer;

            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            // 顶点使用本地坐标：线条随格子 / Grid_Root / GridManager 的位移、旋转、缩放一起移动
            lr.useWorldSpace = false;
            lr.loop = false;
            lr.positionCount = 2;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            lr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            lr.SetPositions(new Vector3[]
            {
                lineObj.transform.InverseTransformPoint(start),
                lineObj.transform.InverseTransformPoint(end)
            });
        };

        createSingleLine("Border_L", leftDown, leftUp);
        createSingleLine("Border_R", rightDown, rightUp);
        createSingleLine("Border_U", leftUp, rightUp);
        createSingleLine("Border_D", leftDown, rightDown);

        cellMgr.CacheLineRenderers();
        cellMgr.SetLineColor(lineNormalColor);
    }

    #region 地形贴合 (Map)

    private Collider mapCollider;
    /// <summary>
    /// 地形贴合的检测来源：Map 自身 + 其所有子物体（含未激活子物体）上的 Collider。
    /// </summary>
    private Collider[] mapColliders;
    private bool warnedAboutMiss;

    /// <summary>
    /// 收集并缓存 Map 自身以及其所有子物体上的 Collider。
    /// 保持原有语义：只检测 Map 层级，不使用 LayerMask，也不会误中场景其他物体。
    /// </summary>
    private void CacheMapColliders()
    {
        if (mapColliders != null && mapColliders.Length > 0) return;
        if (mapObject == null) return;

        // 包含未激活的子物体，保证层级内所有 Collider 都被纳入检测范围
        mapColliders = mapObject.GetComponentsInChildren<Collider>(true);
        mapCollider = mapColliders.Length > 0 ? mapColliders[0] : null;
        if (mapCollider == null) mapColliders = null;
    }

    /// <summary>
    /// 对 Map 自身及其所有子物体的 Collider 做一次向下投射，取其中最高的命中点
    /// （等价于"从上方最先碰到的表面"）。全部未命中时返回 false。
    /// </summary>
    private bool RaycastMapTopmost(Ray ray, float maxDistance, out RaycastHit topmostHit)
    {
        topmostHit = default;
        bool hasHit = false;

        if (mapColliders == null) return false;

        for (int i = 0; i < mapColliders.Length; i++)
        {
            Collider collider = mapColliders[i];
            if (collider == null) continue;
            if (!collider.Raycast(ray, out RaycastHit hit, maxDistance)) continue;

            if (!hasHit || hit.point.y > topmostHit.point.y)
            {
                topmostHit = hit;
                hasHit = true;
            }
        }

        return hasHit;
    }

    /// <summary>Map 层级内所有 Collider 包围盒的最高点，用于确定射线起始高度。</summary>
    private float GetMapCollidersMaxY()
    {
        float maxY = float.NegativeInfinity;
        if (mapColliders == null) return maxY;

        for (int i = 0; i < mapColliders.Length; i++)
        {
            Collider collider = mapColliders[i];
            if (collider == null) continue;
            if (collider.bounds.max.y > maxY) maxY = collider.bounds.max.y;
        }

        return maxY;
    }

    /// <summary>
    /// 从 Map 正上方垂直向下穿透采样：每命中一个「朝上」的表面就记录高度，
    /// 然后从命中点下方继续向下投射，直到无命中或达到 maxSurfaceLayers。
    /// 查询 mapObject 自身以及其所有子物体上的 Collider（无需 LayerMask，也不会误中场景其他物体）。
    /// 返回的高度列表自上而下排序；全部未命中返回空列表。
    /// </summary>
    private List<float> SampleMapHeightsAt(Vector3 worldXZ)
    {
        List<float> heights = new List<float>();

        CacheMapColliders();

        if (mapColliders == null || mapColliders.Length == 0)
        {
            if (!warnedAboutMiss)
            {
                warnedAboutMiss = true;
                Debug.LogWarning("[GridManager] mapObject 上没有找到 Collider，地形贴合已跳过，格子使用平面高度。");
            }
            return heights;
        }

        float rayStartY = GetMapCollidersMaxY() + 10f;
        float probeY = rayStartY;

        for (int i = 0; i < maxSurfaceLayers; i++)
        {
            Ray ray = new Ray(new Vector3(worldXZ.x, probeY, worldXZ.z), Vector3.down);
            if (!RaycastMapTopmost(ray, 1000f, out RaycastHit hit)) break;

            // 只接受朝上的表面（法线朝上），避免在悬挑底面 / 侧壁铺设格子
            if (hit.normal.y > 0.3f)
            {
                // 与上一个命中点几乎同高视为同一表面（避免重复铺设）
                if (heights.Count == 0 || Mathf.Abs(hit.point.y - heights[heights.Count - 1]) > 0.05f)
                {
                    heights.Add(hit.point.y);
                }
            }

            // 从命中点下方继续下探，实现射线穿透
            probeY = hit.point.y - 0.02f;
        }

        return heights;
    }

    /// <summary>
    /// 五点采样（中心 + 四角），对每一层表面取五点中的最高值。
    /// 保证该层格子浮在该层地表最高点之上、永不陷入地形。
    /// 某层只在部分采样点命中时（如格子在台阶边缘），该层按未命中处理，不铺设。
    /// 全部未命中返回空列表。
    /// </summary>
    private List<float> SampleCellSurfaceHeights(Vector3 center)
    {
        float hw = cellWidth * 0.5f;
        float hl = cellLength * 0.5f;

        Vector3[] samplePoints =
        {
            center,
            center + new Vector3(-hw, 0f, -hl),
            center + new Vector3( hw, 0f, -hl),
            center + new Vector3(-hw, 0f,  hl),
            center + new Vector3( hw, 0f,  hl),
        };

        List<List<float>> perPointHeights = new List<List<float>>();
        foreach (Vector3 p in samplePoints)
        {
            perPointHeights.Add(SampleMapHeightsAt(p));
        }

        // 层数取五点中的最小命中层数：保证该层在格子的整个覆盖范围内都存在
        int layerCount = int.MaxValue;
        foreach (List<float> h in perPointHeights)
        {
            layerCount = Mathf.Min(layerCount, h.Count);
        }

        if (layerCount == int.MaxValue || layerCount <= 0) return new List<float>();

        List<float> result = new List<float>(layerCount);
        for (int layer = 0; layer < layerCount; layer++)
        {
            float peak = float.NegativeInfinity;
            foreach (List<float> h in perPointHeights)
            {
                peak = Mathf.Max(peak, h[layer]);
            }
            result.Add(peak);
        }
        return result;
    }

    /// <summary>
    /// 计算格子的实际摆放位置列表：X/Z 保持平面网格坐标，Y 为每层 Map 表面峰值 + 抬升量。
    /// 未启用贴合 / 未指定 Map / 采样未命中时回退到原平面行为（仅一层）。
    /// </summary>
    private List<Vector3> GetFittedPhysicsPositions(Vector3 cellLogicCenter)
    {
        List<Vector3> positions = new List<Vector3>();
        float fallbackY = cellLogicCenter.y + cellOffset.y;

        if (enableTerrainFit && mapObject != null)
        {
            List<float> surfaceHeights = SampleCellSurfaceHeights(cellLogicCenter);
            if (surfaceHeights.Count > 0)
            {
                foreach (float surfaceHeight in surfaceHeights)
                {
                    positions.Add(new Vector3(cellLogicCenter.x, surfaceHeight + terrainFitOffset, cellLogicCenter.z));
                }
                return positions;
            }
            else if (!warnedAboutMiss)
            {
                warnedAboutMiss = true;
                Debug.LogWarning("[GridManager] 采样未命中 Map 地表（格子可能超出地图范围），已回退到平面高度。");
            }
        }

        positions.Add(new Vector3(cellLogicCenter.x, fallbackY, cellLogicCenter.z));
        return positions;
    }

    /// <summary>
    /// 重新贴合：对已生成的每层格子重新采样 Map 高度并更新位置与边框线。
    /// 供运行时地图移动/变形后调用；编辑器下也可手动触发。
    /// </summary>
    public void RefitToMap()
    {
        EnsureGridSystemInitialized();

        if (cellManagers == null || cellManagers.GetLength(0) != width || cellManagers.GetLength(1) != height)
        {
            RebuildReferencesFromHierarchy();
        }
        if (cellManagers == null) return;

        warnedAboutMiss = false;
        mapCollider = null;
        mapColliders = null;
        if (enableTerrainFit && mapObject != null) Physics.SyncTransforms();

        for (int x = 0; x < cellManagers.GetLength(0); x++)
        {
            for (int z = 0; z < cellManagers.GetLength(1); z++)
            {
                List<CellManager> column = cellManagers[x, z];
                if (column == null) continue;

                Vector3 cellLogicCenter = gridSystem.GetWorldPosition(x, z);
                List<Vector3> cellPhysicsPositions = GetFittedPhysicsPositions(cellLogicCenter);

                // 层数变化：补建缺失的层，或销毁多余的层
                while (column.Count < cellPhysicsPositions.Count)
                {
                    int layer = column.Count;
                    CellManager cellMgr = CreateCellInstance(x, z, cellPhysicsPositions[layer]);
                    column.Add(cellMgr);
                }
                while (column.Count > cellPhysicsPositions.Count)
                {
                    CellManager extra = column[column.Count - 1];
                    if (extra != null) DestroyImmediate(extra.gameObject);
                    column.RemoveAt(column.Count - 1);
                }

                // 重新贴合已有格子
                for (int layer = 0; layer < column.Count; layer++)
                {
                    CellManager cellMgr = column[layer];
                    if (cellMgr == null) continue;

                    Vector3 cellPhysicsPosition = cellPhysicsPositions[layer];
                    cellMgr.transform.position = cellPhysicsPosition;

                    // 高度变化后按层高重新丈量层号，同步更新格子名字
                    cellMgr.gameObject.name = GetCellName(x, z, cellPhysicsPosition);

                    // 重建边框线，使其与新高度同平面（显式 Refit 操作，编辑器/运行时均可 DestroyImmediate）
                    string[] lineNames = { "Border_L", "Border_R", "Border_U", "Border_D" };
                    foreach (string lineName in lineNames)
                    {
                        Transform line = cellMgr.transform.Find(lineName);
                        if (line != null) DestroyImmediate(line.gameObject);
                    }

                    CreateFourLineRenderersForCell(x, z, cellPhysicsPosition, cellMgr);
                }
            }
        }

        // 重新贴合后高度可能变化，全部处理完再按实测层高统一划分层号
        RebuildLayerNames();
    }

    #endregion

    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null) return;

        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            if (child != null)
            {
                SetLayerRecursively(child.gameObject, newLayer);
            }
        }
    }

    /// <summary>获取某列格子的层数（顶层为 0 层）。</summary>
    public int GetLayerCount(int x, int z)
    {
        if (cellManagers == null || x < 0 || x >= width || z < 0 || z >= height) return 0;
        List<CellManager> column = cellManagers[x, z];
        return column == null ? 0 : column.Count;
    }

    /// <summary>
    /// 获取某列的全部格子（从最高层到最低层）。用于跨层范围计算。
    /// </summary>
    public List<CellManager> GetCellManagersInColumn(int x, int z)
    {
        if (cellManagers == null || x < 0 || x >= width || z < 0 || z >= height)
        {
            return new List<CellManager>();
        }
        return cellManagers[x, z] ?? new List<CellManager>();
    }

    /// <summary>
    /// 从格子名字解析该格子的列坐标 (x, z)。
    /// 名字格式：Cell_{z+1}_{x+1}[_L{层号}]，例如 Cell_2_10_L1 → (9, 1)。
    /// </summary>
    public (int x, int z) GetCellGridPosition(CellManager cell)
    {
        if (cell == null) return (-1, -1);

        string[] parts = cell.gameObject.name.Split('_');
        if (parts.Length >= 3 &&
            int.TryParse(parts[1], out int zPlusOne) &&
            int.TryParse(parts[2], out int xPlusOne))
        {
            return (xPlusOne - 1, zPlusOne - 1);
        }
        return (-1, -1);
    }

    /// <summary>
    /// 从格子名字解析该格子的 L 层号（Cell_{z+1}_{x+1}_L{层号}）。
    /// 无 _L 后缀的旧版格子视为 L1。
    /// </summary>
    public int GetCellLayer(CellManager cell)
    {
        if (cell == null) return 1;

        string[] parts = cell.gameObject.name.Split('_');
        if (parts.Length >= 4 && parts[3].StartsWith("L") &&
            int.TryParse(parts[3].Substring(1), out int layer))
        {
            return layer;
        }
        return 1;
    }

    /// <summary>
    /// 获取指定列的顶层格子（兼容旧调用方：悬停、拖牌、移动、怪物 AI 均默认使用顶层）。
    /// </summary>
    public CellManager GetCellManagerAt(int x, int z)
    {
        if (cellManagers != null && x >= 0 && x < width && z >= 0 && z < height)
        {
            List<CellManager> column = cellManagers[x, z];
            if (column != null && column.Count > 0)
            {
                return column[0];
            }
        }
        return null;
    }

    /// <summary>
    /// 获取指定列指定 L 层号的格子（层号由铺设高度按 cellHeight 丈量，如 L1 / L2 / L3）。
    /// 同一列存在多个同层号格子时返回第一个；找不到返回 null。
    /// </summary>
    public CellManager GetCellManagerAt(int x, int z, int layer)
    {
        if (cellManagers != null && x >= 0 && x < width && z >= 0 && z < height)
        {
            List<CellManager> column = cellManagers[x, z];
            if (column != null)
            {
                string layerTag = $"L{layer}";
                foreach (CellManager cellMgr in column)
                {
                    if (cellMgr == null) continue;
                    string[] parts = cellMgr.gameObject.name.Split('_');
                    if (parts.Length >= 4 && parts[3] == layerTag)
                    {
                        return cellMgr;
                    }
                }
            }
        }
        return null;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(GridManager))]
public class GridManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        GridManager manager = (GridManager)target;

        // 1. 绘制除 cellLayer、gridRoot 以外的所有普通变量
        DrawPropertiesExcluding(serializedObject, "cellLayer", "gridRoot");

        // 2. 紧接着在下面手动绘制带标题的 Layer 选择下拉框框
        GUILayout.Space(10);
        EditorGUILayout.LabelField("格子 Layer 层级", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        int selectedLayer = EditorGUILayout.LayerField("Cell Layer", manager.cellLayer);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(manager, "Change Cell Layer");
            manager.cellLayer = selectedLayer;
            EditorUtility.SetDirty(manager);
        }

        // 3. 绘制生成/清空控制按钮
        GUILayout.Space(15);
        GUILayout.Label("网格生成控制", EditorStyles.boldLabel);

        if (GUILayout.Button("生成网格至 Hierarchy", GUILayout.Height(30)))
        {
            manager.GenerateGridToHierarchy();
        }

        if (GUILayout.Button("清空 Hierarchy 中的网格", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("确认清空", "确定要删除 Hierarchy 下的所有格子吗？（删除后需重新生成）", "确定", "取消"))
            {
                manager.ClearGridFromHierarchy();
            }
        }

        if (GUILayout.Button("重新贴合地形 (Refit)", GUILayout.Height(30)))
        {
            manager.RefitToMap();
        }

        // 保证 Inspector 状态正常同步更新
        serializedObject.ApplyModifiedProperties();
    }
}
#endif
