using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

public enum CellAccessState
{
    Normal = 0,
    SpecialTerrain = 1,
    HardLocked = 2
}

[ExecuteInEditMode]
public class CellManager : MonoBehaviour
{
    public static event Action<CellManager> PlayerEntered;
    public static event Action<CellManager, MonsterIdentityManager> MonsterEntered;

    [Header("格子通行状态")]
    [Tooltip("普通：玩家与怪物均可通行。特殊地形：玩家与普通怪物禁入，特殊怪物可通行，并显示特殊地形颜色。" +
             "真正锁死：所有单位禁入、不可选、无悬停，并恢复为全透明显示。")]
    [SerializeField] private CellAccessState accessState = CellAccessState.Normal;

    [HideInInspector]
    [FormerlySerializedAs("isLocked")]
    [SerializeField] private bool legacyIsLocked = false;

    [HideInInspector]
    [SerializeField] private bool accessStateMigrated = false;

    public CellAccessState AccessState => accessState;
    public bool IsSpecialTerrain => accessState == CellAccessState.SpecialTerrain;
    public bool IsHardLocked => accessState == CellAccessState.HardLocked;
    public bool BlocksPlayer => accessState != CellAccessState.Normal;
    public bool SuppressesHoverHighlight => accessState != CellAccessState.Normal;

    /// <summary>
    /// 判断指定怪物能否进入/经过该格。特殊地形仅允许显式开启该能力的怪物通行。
    /// </summary>
    public bool CanMonsterTraverse(MonsterIdentityManager monster)
    {
        switch (accessState)
        {
            case CellAccessState.Normal:
                return true;
            case CellAccessState.SpecialTerrain:
                return monster != null && monster.CanTraverseSpecialTerrain;
            default:
                return false;
        }
    }

    // ------------------------------------------------------------------
    // 外观缓存：记录最近一次请求的颜色/边框参数，返回 Normal 时恢复。
    // ------------------------------------------------------------------
    private Color lastCellColor = Color.white;
    private Color lastLineColor = Color.white;
    private Color lastIndividualDefaultColor = Color.white;
    private Color lastIndividualOuterColor = Color.white;
    private bool lastUpOuter, lastDownOuter, lastLeftOuter, lastRightOuter;
    private bool hasIndividualLines = false; // 最近一次边框请求是否为逐边模式
    [HideInInspector][SerializeField] private CellAccessState lastAccessState = CellAccessState.Normal;
    [HideInInspector][SerializeField] private Material cellMaterialInstance; // 特殊状态的独立材质实例

    [Header("调试 UI 设置")]
    public Color gizmosTextColor = Color.white;     // 调试文本颜色
    [Range(1, 36)]
    public int fontSize = 8;

    private MeshRenderer meshRenderer;

    // 缓存 4 条边的 LineRenderer 引用：0:左(Left), 1:右(Right), 2:上(Up), 3:下(Down)
    [HideInInspector][SerializeField] private LineRenderer[] lineRenderers = new LineRenderer[4];

    // 标记当前是否有 Tag 为 Player 的物体在 Collider 内
    private bool isPlayerInside = false;

    // 当前在格子内的所有怪物实例（MonsterManager组件，即挂在每个怪物身上的那个）
    // 用引用存储，天然区分"这一只"和"那一只"
    private readonly HashSet<MonsterIdentityManager> monstersInside = new HashSet<MonsterIdentityManager>();

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        CacheLineRenderers();
        EnsureAccessStateMigrated();

        if (accessState != CellAccessState.Normal)
            ApplyAccessStateVisuals();
    }

    private void OnDisable()
    {
        isPlayerInside = false;
        monstersInside.Clear();
    }

    private void OnValidate()
    {
        EnsureAccessStateMigrated();

        bool accessStateChanged = accessState != lastAccessState;
        lastAccessState = accessState;

        if (accessStateChanged || accessState != CellAccessState.Normal)
            ApplyAccessStateVisuals();
    }

    /// <summary>
    /// 兼容旧场景数据：原 isLocked=true 统一迁移为特殊地形，保留现有显示效果。
    /// </summary>
    private void EnsureAccessStateMigrated()
    {
        if (accessStateMigrated)
            return;

        if (legacyIsLocked && accessState == CellAccessState.Normal)
            accessState = CellAccessState.SpecialTerrain;

        legacyIsLocked = false;
        accessStateMigrated = true;
    }

    /// <summary>
    /// 应用当前状态对应的显示。状态色不会写入请求颜色缓存，保证切回 Normal 时可恢复。
    /// </summary>
    private void ApplyAccessStateVisuals()
    {
        switch (accessState)
        {
            case CellAccessState.SpecialTerrain:
                ApplySpecialTerrainVisuals();
                break;
            case CellAccessState.HardLocked:
                ApplyHardLockedVisuals();
                break;
            default:
                RestoreLastRequestedVisuals();
                break;
        }
    }

    private void ApplySpecialTerrainVisuals()
    {
        GetSpecialTerrainColors(out Color cellColor, out Color lineColor);

        if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
        ApplyCellColor(cellColor, Application.isPlaying, forceInstance: true);

        for (int i = 0; i < 4; i++)
        {
            if (lineRenderers[i] == null) continue;
            lineRenderers[i].startColor = lineColor;
            lineRenderers[i].endColor = lineColor;
        }
    }

    /// <summary>
    /// 真正锁死时恢复旧版表现：格子面片与四条边框全部透明。
    /// </summary>
    private void ApplyHardLockedVisuals()
    {
        if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
        ApplyCellColor(Color.clear, Application.isPlaying, forceInstance: true);

        for (int i = 0; i < 4; i++)
        {
            if (lineRenderers[i] == null) continue;
            lineRenderers[i].startColor = Color.clear;
            lineRenderers[i].endColor = Color.clear;
        }
    }

    private void RestoreLastRequestedVisuals()
    {
        SetCellColor(lastCellColor, Application.isPlaying);
        if (hasIndividualLines)
        {
            SetIndividualLinesColor(
                lastIndividualDefaultColor,
                lastIndividualOuterColor,
                lastUpOuter,
                lastDownOuter,
                lastLeftOuter,
                lastRightOuter);
        }
        else
        {
            SetLineColor(lastLineColor);
        }
    }

    /// <summary>
    /// 从所属 GridManager 读取特殊地形颜色；未找到时回退为透明，兼容独立测试的 Cell。
    /// </summary>
    private void GetSpecialTerrainColors(out Color cellColor, out Color lineColor)
    {
        GridManager gridManager = GetComponentInParent<GridManager>();
        if (gridManager == null)
        {
            cellColor = Color.clear;
            lineColor = Color.clear;
            return;
        }

        cellColor = gridManager.SpecialTerrainCellColor;
        lineColor = gridManager.SpecialTerrainLineColor;
    }

    /// <summary>
    /// 设置面片颜色。编辑模式下需要独立材质时显式创建材质实例，避免修改共享材质影响其他 Cell。
    /// </summary>
    private void ApplyCellColor(Color color, bool isRuntime, bool forceInstance)
    {
        if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null) return;

        if (isRuntime)
        {
            meshRenderer.material.color = color;
            return;
        }

        if (!forceInstance)
        {
            if (meshRenderer.sharedMaterial != null)
                meshRenderer.sharedMaterial.color = color;
            return;
        }

        if (cellMaterialInstance == null || meshRenderer.sharedMaterial != cellMaterialInstance)
        {
            Material sourceMaterial = meshRenderer.sharedMaterial;
            if (sourceMaterial == null) return;

            cellMaterialInstance = new Material(sourceMaterial);
            meshRenderer.sharedMaterial = cellMaterialInstance;
        }

        cellMaterialInstance.color = color;
    }

    /// <summary>
    /// 寻找并缓存子物体中的四条边
    /// </summary>
    public void CacheLineRenderers()
    {
        lineRenderers[0] = transform.Find("Border_L")?.GetComponent<LineRenderer>();
        lineRenderers[1] = transform.Find("Border_R")?.GetComponent<LineRenderer>();
        lineRenderers[2] = transform.Find("Border_U")?.GetComponent<LineRenderer>();
        lineRenderers[3] = transform.Find("Border_D")?.GetComponent<LineRenderer>();
        ConvertBordersToLocalSpace();
    }

    /// <summary>
    /// 兼容旧数据：把边框线从「世界坐标顶点」(useWorldSpace = true) 就地转换为本地坐标，
    /// 转换后线条会随父物体（格子 / Grid_Root / GridManager）一起位移、旋转、缩放。
    /// 已经是本地坐标的线条会被跳过；顶点的世界位置不变，视觉上无变化。
    /// </summary>
    public void ConvertBordersToLocalSpace()
    {
        for (int i = 0; i < lineRenderers.Length; i++)
        {
            LineRenderer lr = lineRenderers[i];
            if (lr == null || !lr.useWorldSpace) continue;

            int count = lr.positionCount;
            if (count <= 0) continue;

            Vector3[] worldPoints = new Vector3[count];
            lr.GetPositions(worldPoints);

            Vector3[] localPoints = new Vector3[count];
            for (int p = 0; p < count; p++)
            {
                localPoints[p] = lr.transform.InverseTransformPoint(worldPoints[p]);
            }

            lr.useWorldSpace = false;
            lr.SetPositions(localPoints);
        }
    }

    #region 玩家/怪物 进入离开检测
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (!isPlayerInside)
            {
                isPlayerInside = true;
                PlayerEntered?.Invoke(this);
            }
            return;
        }

        // 怪物用组件（MonsterManager）识别具体身份，而不是靠字符串Tag区分种类。
        // 用 GetComponentInParent：兼容 MonsterIdentityManager 挂在碰撞体自身或其任意父级（如怪物根物体）的情况。
        MonsterIdentityManager monster = other.GetComponentInParent<MonsterIdentityManager>();
        if (monster != null)
        {
            if (monstersInside.Add(monster))
            {
                MonsterEntered?.Invoke(this, monster);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInside = false;
            return;
        }

        // 与 OnTriggerEnter 对称：同样向上查父级，保证进入/离开配对移除
        MonsterIdentityManager monster = other.GetComponentInParent<MonsterIdentityManager>();
        if (monster != null)
        {
            monstersInside.Remove(monster); // 只移除"这一只"，不影响格子里其他怪物
        }
    }
    #endregion

    /// <summary>当前格子内是否有玩家（Tag 为 Player 的 Collider）。</summary>
    public bool IsPlayerInside => isPlayerInside;

    /// <summary>当前格子内是否有怪物。</summary>
    public bool HasMonsterInside
    {
        get
        {
            PruneInactiveMonsters();
            return monstersInside.Count > 0;
        }
    }

    /// <summary>
    /// 供外部查询：格子内当前的怪物列表（只读）
    /// </summary>
    public IReadOnlyCollection<MonsterIdentityManager> GetMonstersInside()
    {
        PruneInactiveMonsters();
        return monstersInside;
    }

    /// <summary>
    /// 供外部查询：格子内是否存在指定阵营的怪物。
    /// </summary>
    public bool HasMonsterOfFaction(MonsterIdentityManager.MonsterFaction faction)
    {
        PruneInactiveMonsters();
        return monstersInside.Any(m => m != null && m.faction == faction);
    }

    /// <summary>
    /// 清理已销毁或已被禁用的怪物引用。
    /// 死亡怪物会执行 SetActive(false)，但禁用的 Collider 不保证触发 OnTriggerExit。
    /// </summary>
    private void PruneInactiveMonsters()
    {
        monstersInside.RemoveWhere(
            monster => monster == null || !monster.gameObject.activeInHierarchy);
    }

    /// <summary>
    /// 设置格子的面片颜色
    /// </summary>
    public void SetCellColor(Color color, bool isRuntime)
    {
        // 状态覆盖时也缓存外部请求，供返回 Normal 时恢复。
        lastCellColor = color;

        if (accessState == CellAccessState.SpecialTerrain)
        {
            GetSpecialTerrainColors(out Color specialCellColor, out _);
            color = specialCellColor;
        }
        else if (accessState == CellAccessState.HardLocked)
        {
            color = Color.clear;
        }

        ApplyCellColor(color, isRuntime, forceInstance: accessState != CellAccessState.Normal);
    }

    /// <summary>
    /// 统一设置所有边框线的颜色
    /// </summary>
    public void SetLineColor(Color color)
    {
        // 状态覆盖时也缓存外部请求，供返回 Normal 时恢复。
        lastLineColor = color;
        hasIndividualLines = false; // 最近一次为统一边框模式

        if (accessState == CellAccessState.SpecialTerrain)
        {
            GetSpecialTerrainColors(out _, out Color specialLineColor);
            color = specialLineColor;
        }
        else if (accessState == CellAccessState.HardLocked)
        {
            color = Color.clear;
        }
        for (int i = 0; i < 4; i++)
        {
            if (lineRenderers[i] != null)
            {
                lineRenderers[i].startColor = color;
                lineRenderers[i].endColor = color;
            }
        }
    }

    /// <summary>
    /// 精确设置上下左右 4 条边的不同颜色
    /// </summary>
    /// <param name="defaultColor">内侧共用边或常规边的颜色</param>
    /// <param name="outerColor">外包络暴露边的颜色</param>
    public void SetIndividualLinesColor(Color defaultColor, Color outerColor, bool upOuter, bool downOuter, bool leftOuter, bool rightOuter)
    {
        // 状态覆盖时也缓存外部请求，供返回 Normal 时恢复。
        lastIndividualDefaultColor = defaultColor;
        lastIndividualOuterColor = outerColor;
        lastUpOuter = upOuter; lastDownOuter = downOuter;
        lastLeftOuter = leftOuter; lastRightOuter = rightOuter;
        hasIndividualLines = true; // 最近一次为逐边模式

        if (accessState == CellAccessState.SpecialTerrain)
        {
            GetSpecialTerrainColors(out _, out Color specialLineColor);
            defaultColor = specialLineColor;
            outerColor = specialLineColor;
        }
        else if (accessState == CellAccessState.HardLocked)
        {
            defaultColor = Color.clear;
            outerColor = Color.clear;
        }
        // 0: 左
        if (lineRenderers[0] != null)
        {
            Color c = leftOuter ? outerColor : defaultColor;
            lineRenderers[0].startColor = c; lineRenderers[0].endColor = c;
        }
        // 1: 右
        if (lineRenderers[1] != null)
        {
            Color c = rightOuter ? outerColor : defaultColor;
            lineRenderers[1].startColor = c; lineRenderers[1].endColor = c;
        }
        // 2: 上
        if (lineRenderers[2] != null)
        {
            Color c = upOuter ? outerColor : defaultColor;
            lineRenderers[2].startColor = c; lineRenderers[2].endColor = c;
        }
        // 3: 下
        if (lineRenderers[3] != null)
        {
            Color c = downOuter ? outerColor : defaultColor;
            lineRenderers[3].startColor = c; lineRenderers[3].endColor = c;
        }
    }

    /// <summary>
    /// 同步所有单边线宽
    /// </summary>
    public void SetLineWidth(float width)
    {
        for (int i = 0; i < 4; i++)
        {
            if (lineRenderers[i] != null)
            {
                lineRenderers[i].startWidth = width;
                lineRenderers[i].endWidth = width;
            }
        }
    }

    private void OnDrawGizmos()
    {
#if UNITY_EDITOR
        Camera activeCam = Camera.current;
        if (activeCam == null || activeCam.name != "SceneCamera") return;

        GUIStyle labelStyle = new GUIStyle();
        labelStyle.normal.textColor = gizmosTextColor;
        labelStyle.alignment = TextAnchor.MiddleCenter;

        Vector3 cellCenter = transform.position;

        float distance = Vector3.Distance(activeCam.transform.position, cellCenter);
        if (distance < 0.1f) distance = 0.1f;
        float scaleFactor = 15f / distance;
        labelStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(fontSize * scaleFactor), 4, 36);

        string[] parts = gameObject.name.Split('_');
        if (parts.Length >= 3)
        {
            string displayY = parts[1]; // Z + 1
            string displayX = parts[2]; // X + 1

            // 新命名格式 Cell_{z+1}_{x+1}_L{层号}，将 L 层号列在坐标之后，如 "1-1-L1"
            string displayText = $"{displayY}-{displayX}";
            if (parts.Length >= 4 && parts[3].StartsWith("L"))
            {
                displayText += $"-{parts[3]}";
            }

            if (isPlayerInside)
            {
                displayText += "\nC = Player";
            }

            PruneInactiveMonsters();
            if (monstersInside.Count > 0)
            {
                // monsterId 本身就是"类型_序号"格式（如 Skeleton_1），直接读取即可，无需再拼接类型和名字
                foreach (MonsterIdentityManager monster in monstersInside)
                {
                    displayText += $"\nC = {monster.monsterId}";
                }
            }

            Handles.Label(cellCenter, displayText, labelStyle);
        }
#endif
    }
}
