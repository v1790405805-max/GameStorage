using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 塌陷格（CollapsedCell）：任意生物（玩家或怪物）进入本格后，从当前回合开始计算，
/// 本格会在"下一个回合结束时"下沉一层：
/// - 若本格下方还有更低层的格子：本格下落至该下方格的层高、名字改为该下方格的名字，并禁用该下方格；
/// - 若本格下方没有更低层格子：本格直接落到 L1 层高（网格基准面），名字后缀改为 _L1。
/// 下沉时本格内的生物随格子一起下落（与格子的相对位置不变，不会悬空）。
/// 挂载位置：与 CellManager 同一个 GameObject（格子的 Trigger 碰撞体在该物体上）。
/// 回合接入：本脚本在运行时创建一个挂有 CollapsedCellTurnListener 的子物体，
/// 由该监听器注册进 TurnManager 的玩家行为列表；下沉在玩家回合结束时结算
/// （ITurnStateListener.OnTurnDeactivated，即玩家点击"结束回合"按钮时触发），
/// 无需等待敌方回合。本脚本自身不注册，因为 TurnManager.SetGroupState 会禁用
/// 注册进玩家行为列表的组件，那样敌方回合期间本格将收不到 Trigger 事件。
/// </summary>
public class CollapsedCell : MonoBehaviour
{
    [Header("网格引用")]
    public GridManager gridManager;

    [Header("下沉规则")]
    [Tooltip("生物进入本格后，经过多少个回合结束时下沉（1 = 下一个回合结束时）")]
    public int sinkDelayRounds = 1;

    [Header("下沉联动")]
    [Tooltip("跟随本格保持相同 Y 坐标并一起下沉的物体（X/Z 保持不变）")]
    public GameObject followTarget;

    /// <summary>预定下沉的回合数（-1 = 未武装）。回合数即 TurnManager.currentRoundCount。</summary>
    private int sinkRound = -1;

    private CellManager selfCellManager;
    private CollapsedCellTurnListener turnListener;

    private void Awake()
    {
        selfCellManager = GetComponent<CellManager>();
    }

    private void Start()
    {
        if (gridManager == null)
        {
            gridManager = FindFirstObjectByType<GridManager>();
        }

        if (Application.isPlaying && TurnManager.Instance != null)
        {
            GameObject listenerObj = new GameObject("CollapsedCell_TurnListener");
            listenerObj.transform.SetParent(transform, false);
            turnListener = listenerObj.AddComponent<CollapsedCellTurnListener>();
            turnListener.Init(this);
            TurnManager.Instance.RegisterPlayerBehaviour(turnListener);
        }
    }

    private void OnDestroy()
    {
        if (turnListener != null)
        {
            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.UnregisterPlayerBehaviour(turnListener);
            }
            if (turnListener.gameObject != null)
            {
                Destroy(turnListener.gameObject);
            }
            turnListener = null;
        }
    }

    // ==================================================================
    // 触发检测：任意生物进入 → 武装倒计时（已武装不重置；离开不取消）
    // ==================================================================

    private void OnTriggerEnter(Collider other)
    {
        if (other == null || !IsCreature(other))
        {
            return;
        }

        ArmIfNeeded();
    }

    private bool IsCreature(Collider other)
    {
        return other.CompareTag("Player")
            || other.GetComponentInParent<MonsterIdentityManager>() != null;
    }

    private void ArmIfNeeded()
    {
        if (sinkRound >= 0)
        {
            return; // 已武装，不重置
        }

        int currentRound = TurnManager.Instance != null ? TurnManager.Instance.currentRoundCount : 0;
        // 第 R 回合进入、sinkDelayRounds=1 → sinkRound = R+1，即第 R+1 回合玩家回合结束时（点击结束回合按钮时）下沉
        sinkRound = currentRound + sinkDelayRounds;
    }

    // ==================================================================
    // 回合回调（由子物体上的 CollapsedCellTurnListener 转发）
    // ==================================================================

    internal void OnPlayerTurnEnded()
    {
        if (!Application.isPlaying || TurnManager.Instance == null || sinkRound < 0)
        {
            return;
        }

        if (TurnManager.Instance.currentRoundCount >= sinkRound)
        {
            Sink();
        }
    }

    // ==================================================================
    // 下沉执行
    // ==================================================================

    private void Sink()
    {
        sinkRound = -1; // 先解除武装，防止重复触发

        if (gridManager == null || selfCellManager == null)
        {
            return;
        }

        (int x, int z) = gridManager.GetCellGridPosition(selfCellManager);
        if (x < 0 || z < 0)
        {
            return;
        }

        List<CellManager> column = gridManager.GetCellManagersInColumn(x, z);
        int myIndex = column.IndexOf(selfCellManager);
        if (myIndex < 0)
        {
            return; // 不在列内（异常状态），放弃
        }

        float oldY = transform.position.y;
        float newY = oldY;

        if (myIndex + 1 < column.Count)
        {
            // 情况 1：下方还有更低层的格子 → 落到该格层高，并禁用该格
            CellManager lowerCell = column[myIndex + 1];
            if (lowerCell == null || lowerCell == selfCellManager)
            {
                return;
            }

            newY = lowerCell.transform.position.y;
            string lowerName = lowerCell.name;

            lowerCell.gameObject.SetActive(false); // 禁用较低层格（其 OnDisable 会清理自身登记）
            column.RemoveAt(myIndex + 1);          // 从列列表中移除

            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
            gameObject.name = lowerName;           // Inspector 名字随层高变化
        }
        else
        {
            // 情况 2：没有更低层格 → 直接落到 L1 层高（网格基准面）
            if (gridManager.GetCellLayer(selfCellManager) <= 1)
            {
                return; // 已是 L1，守卫（不会沉到基准面以下）
            }

            newY = gridManager.transform.position.y + gridManager.cellOffset.y;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
            gameObject.name = ApplyLayerSuffix(gameObject.name, 1);
        }

        // 生物随格下沉（与格子相对位置不变，避免悬空）
        float deltaY = newY - oldY;
        if (Mathf.Abs(deltaY) > 0.0001f)
        {
            MoveBordersBy(deltaY); // 边框线顶点是世界坐标（useWorldSpace），父物体移动不带线条，需手动平移
            MoveOccupantsBy(deltaY);
        }

        // 联动物体与本格保持相同 Y 坐标，一起下沉（X/Z 保持不变）
        if (followTarget != null)
        {
            Vector3 followPos = followTarget.transform.position;
            followPos.y = transform.position.y;
            followTarget.transform.position = followPos;
        }
    }

    /// <summary>
    /// 下沉时同步移动边框子物体（Border_L/R/U/D）。
    /// GridManager 创建边框时 useWorldSpace = true，顶点存的是世界坐标，
    /// 父物体（格子）移动不会带动线条，需手动平移每个顶点的 Y。
    /// </summary>
    private void MoveBordersBy(float deltaY)
    {
        string[] borderNames = { "Border_L", "Border_R", "Border_U", "Border_D" };
        for (int i = 0; i < borderNames.Length; i++)
        {
            Transform border = transform.Find(borderNames[i]);
            if (border == null)
            {
                continue;
            }

            LineRenderer lr = border.GetComponent<LineRenderer>();
            if (lr == null || !lr.useWorldSpace)
            {
                continue; // 本地坐标的线会随父物体自动移动，无需处理
            }

            Vector3[] points = new Vector3[lr.positionCount];
            lr.GetPositions(points);
            for (int p = 0; p < points.Length; p++)
            {
                points[p].y += deltaY;
            }
            lr.SetPositions(points);
        }
    }

    private void MoveOccupantsBy(float deltaY)
    {
        Vector3 offset = new Vector3(0f, deltaY, 0f);

        foreach (MonsterIdentityManager monster in selfCellManager.GetMonstersInside())
        {
            if (monster != null)
            {
                monster.transform.position += offset;
            }
        }

        if (selfCellManager.IsPlayerInside)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerObj.transform.root.position += offset;
            }
        }
    }

    /// <summary>
    /// 把格子名 "Cell_{z+1}_{x+1}_L{n}" 的层号后缀替换为指定层号。
    /// 无后缀的旧版名字（按 GetCellLayer 规则视为 L1）不做修改。
    /// </summary>
    private static string ApplyLayerSuffix(string cellName, int layerNumber)
    {
        int lastUnderscore = cellName.LastIndexOf('_');
        if (lastUnderscore <= 0 || lastUnderscore + 1 >= cellName.Length)
        {
            return cellName;
        }
        return cellName.Substring(0, lastUnderscore + 1) + "L" + layerNumber;
    }
}

/// <summary>
/// CollapsedCell 的回合监听器：挂在格子下的子物体上，注册进 TurnManager 的玩家行为列表。
/// 注册进该列表的组件在敌方回合会被 TurnManager 禁用（SetGroupState），
/// 因此监听器只负责转发回合回调；CollapsedCell 本体保持启用，
/// 以便在敌方回合期间也能持续接收 Trigger 事件（怪物踩入也能武装）。
/// OnTurnDeactivated 在玩家点击"结束回合"时触发，此时结算下沉（不等待敌方回合）。
/// </summary>
public class CollapsedCellTurnListener : MonoBehaviour, ITurnStateListener
{
    private CollapsedCell owner;

    public void Init(CollapsedCell cell)
    {
        owner = cell;
    }

    public void OnTurnActivated()
    {
        // 无需处理：下沉只在玩家回合结束时（OnTurnDeactivated）结算
    }

    public void OnTurnDeactivated()
    {
        if (owner != null)
        {
            owner.OnPlayerTurnEnded();
        }
    }
}
