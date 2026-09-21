using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class CellManager : MonoBehaviour
{
    [Header("状态锁")]
    [Tooltip("启用后该格子对玩家与怪物全部失效：无法移动上去、不在范围判定内、无法选中、悬停无效果，" +
             "格子上色与边框上色均为全透明。注意：仅代表逻辑失效，格子本身并未被禁用（组件与 GameObject 保持 active）。")]
    [SerializeField] private bool isLocked = false;

    /// <summary>
    /// 状态锁：true 表示该格子逻辑失效（禁入、范围外、不可选中、透明）。
    /// 注意：仅逻辑失效，不代表格子被禁用（组件/对象保持 active）。
    /// </summary>
    public bool IsLocked => isLocked;

    // ------------------------------------------------------------------
    // 外观缓存：记录最近一次请求的颜色/边框参数，状态锁解锁时恢复（保证锁定/解锁可逆）
    // ------------------------------------------------------------------
    private Color lastCellColor = Color.white;
    private Color lastLineColor = Color.white;
    private Color lastIndividualDefaultColor = Color.white;
    private Color lastIndividualOuterColor = Color.white;
    private bool lastUpOuter, lastDownOuter, lastLeftOuter, lastRightOuter;
    private bool hasIndividualLines = false; // 最近一次边框请求是否为逐边模式
    [HideInInspector][SerializeField] private bool wasLocked = false; // 上一次锁状态（供 OnValidate 检测变化）

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
    }

    private void OnDisable()
    {
        isPlayerInside = false;
        monstersInside.Clear();
    }

    private void OnValidate()
    {
        bool lockedChanged = (isLocked != wasLocked);
        wasLocked = isLocked;

        if (isLocked)
        {
            // 勾选锁定：立即全透明
            ApplyLockedColors();
        }
        else if (lockedChanged)
        {
            // 取消锁定：恢复锁定前最近一次请求的外观（可逆，不残留透明色）
            SetCellColor(lastCellColor, Application.isPlaying);
            if (hasIndividualLines)
                SetIndividualLinesColor(lastIndividualDefaultColor, lastIndividualOuterColor,
                    lastUpOuter, lastDownOuter, lastLeftOuter, lastRightOuter);
            else
                SetLineColor(lastLineColor);
        }
    }

    /// <summary>
    /// 勾选状态锁时，立即把面片与四条边框置为全透明。
    /// 这里直接改渲染对象，不走 SetCellColor / SetLineColor / SetIndividualLinesColor，
    /// 避免把"透明"写进 lastCellColor / lastLineColor / 逐边参数等缓存——
    /// 否则取消勾选时拿到的"上次请求颜色"已经被改成透明，格子就不会重新出现。
    /// </summary>
    private void ApplyLockedColors()
    {
        if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            if (Application.isPlaying)
                meshRenderer.material.color = Color.clear;
            else
                meshRenderer.sharedMaterial.color = Color.clear;
        }

        for (int i = 0; i < 4; i++)
        {
            if (lineRenderers[i] == null) continue;
            lineRenderers[i].startColor = Color.clear;
            lineRenderers[i].endColor = Color.clear;
        }
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
            isPlayerInside = true;
            return;
        }

        // 怪物用组件（MonsterManager）识别具体身份，而不是靠字符串Tag区分种类。
        // 用 GetComponentInParent：兼容 MonsterIdentityManager 挂在碰撞体自身或其任意父级（如怪物根物体）的情况。
        MonsterIdentityManager monster = other.GetComponentInParent<MonsterIdentityManager>();
        if (monster != null)
        {
            monstersInside.Add(monster); // 引用存储，即使多个同类型怪物也互不干扰
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
    public bool HasMonsterInside => monstersInside.Count > 0;

    /// <summary>
    /// 供外部查询：格子内当前的怪物列表（只读）
    /// </summary>
    public IReadOnlyCollection<MonsterIdentityManager> GetMonstersInside() => monstersInside;

    /// <summary>
    /// 供外部查询：格子内是否存在某种类型的怪物，比如只想知道有没有Boss
    /// </summary>
    public bool HasMonsterOfType(MonsterIdentityManager.MonsterType type)
    {
        return monstersInside.Any(m => m.type == type);
    }

    /// <summary>
    /// 设置格子的面片颜色
    /// </summary>
    public void SetCellColor(Color color, bool isRuntime)
    {
        // 缓存最近一次请求的颜色（锁定时也记录请求值，供解锁恢复）
        lastCellColor = color;

        // 状态锁：无论外部请求什么颜色，一律强制全透明（基础上色/范围高亮/悬停全部失效）
        if (isLocked) color = Color.clear;

        if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            if (isRuntime)
                meshRenderer.material.color = color;
            else
                meshRenderer.sharedMaterial.color = color;
        }
    }

    /// <summary>
    /// 统一设置所有边框线的颜色
    /// </summary>
    public void SetLineColor(Color color)
    {
        // 缓存最近一次请求的边框颜色（锁定时也记录请求值，供解锁恢复）
        lastLineColor = color;
        hasIndividualLines = false; // 最近一次为统一边框模式

        if (isLocked) color = Color.clear; // 状态锁：边框强制全透明
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
        // 缓存最近一次请求的逐边参数（锁定时也记录请求值，供解锁恢复）
        lastIndividualDefaultColor = defaultColor;
        lastIndividualOuterColor = outerColor;
        lastUpOuter = upOuter; lastDownOuter = downOuter;
        lastLeftOuter = leftOuter; lastRightOuter = rightOuter;
        hasIndividualLines = true; // 最近一次为逐边模式

        if (isLocked) { defaultColor = Color.clear; outerColor = Color.clear; } // 状态锁：4 条边强制全透明
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
