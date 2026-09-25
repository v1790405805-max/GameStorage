using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 怪物移动动作单元 (继承自 MonsterActionBase)
/// 可直接拖入 MonsterActionManager 的动作序列列表中
/// </summary>
public class MonsterMoveAction : MonsterActionBase
{
    // 【新增】对外暴露怪物是否处于移动动作中的状态
    public bool MonsterIsMoving { get; private set; } = false;

    [Header("移动属性")]
    [Tooltip("怪物在一回合内最多可以移动的格子数")]
    public int mobility = 3;

    [Tooltip("怪物每平移一格所消耗的时间（秒）")]
    public float stepDuration = 0.3f;

    [Tooltip("移动结束停顿多久后再转头面向玩家（秒）")]
    public float pauseBeforeFacePlayer = 0.5f;

    [Header("网格管理器引用")]
    public GridManager gridManager;

    [Header("移动动画控制")]
    [Tooltip("Animator引用")]
    public Animator monsterAnimator;

    [Tooltip("控制行走动画状态的 Bool 参数名称")]
    public string walkBoolName = "Walk";

    [Tooltip("控制水平朝向的 Float 参数名称")]
    public string horizontalFloatName = "Horizontal";

    [Tooltip("控制垂直朝向的 Float 参数名称")]
    public string verticalFloatName = "Vertical";

    private MonsterIdentityManager selfIdentity;
    private Coroutine moveCoroutine;

    private void Awake()
    {
        selfIdentity = GetComponent<MonsterIdentityManager>();

        if (monsterAnimator == null)
        {
            monsterAnimator = GetComponent<Animator>();
        }
    }

    private void Start()
    {
        EnsureGridManager();
    }

    private void EnsureGridManager()
    {
        if (gridManager == null)
        {
            gridManager = FindObjectOfType<GridManager>();
        }
        if (gridManager != null)
        {
            gridManager.EnsureGridSystemInitialized();
        }
    }

    // ==================================================================
    // 重写 MonsterActionBase 核心接口
    // ==================================================================

    protected override void OnStart()
    {
        EnsureGridManager();

        if (TargetLossEffect.MonstersLosePlayerTargetThisRound)
        {
            SetWalkAnimationState(false);
            CompleteAction();
            return;
        }

        if (gridManager == null || selfIdentity == null)
        {
            Debug.LogError($"[{name}] 缺少 GridManager 或自身未挂载 MonsterIdentityManager，无法执行移动！");
            SetWalkAnimationState(false);
            CompleteAction();
            return;
        }

        // 1. 获取怪物与玩家所在的具体格子（含层）
        if (!TryGetMonsterCell(out CellManager monsterCell))
        {
            Debug.LogWarning($"[{name}] 未在任何 CellManager 中匹配到当前怪物的标记，取消移动。");
            SetWalkAnimationState(false);
            CompleteAction();
            return;
        }

        if (!TryGetPlayerCell(out CellManager playerCell))
        {
            Debug.LogWarning($"[{name}] 未在任何 CellManager 中匹配到 C = Player 标记，取消移动。");
            SetWalkAnimationState(false);
            CompleteAction();
            return;
        }

        // 玩家在隐蔽片内且怪物不在同片：丢失视野，跳过追击（同片内的怪物可看到全片）
        if (!ConcealmentCell.CanMonsterSeePlayer(monsterCell))
        {
            Debug.Log($"[{name}] Player hidden in a concealment patch, monster outside, skip chase.");
            SetWalkAnimationState(false);
            CompleteAction();
            return;
        }

        // 2. A* 寻路计算完整路径（格子级，跨层规则：层差 ≤ 1 可连，≥ 2 不可连）
        List<CellManager> fullPath = FindPathAStarCells(monsterCell, playerCell);

        if (fullPath == null || fullPath.Count <= 1)
        {
            // 无有效路径，停顿后转头
            moveCoroutine = StartCoroutine(StationaryTurnRoutine(monsterCell, playerCell));
            return;
        }

        // 3. 目标为玩家相邻格
        int targetIndexInPath = fullPath.Count - 2;

        if (targetIndexInPath <= 0)
        {
            // 当前已处于玩家相邻格，无需移动，停顿后转头
            moveCoroutine = StartCoroutine(StationaryTurnRoutine(monsterCell, playerCell));
            return;
        }

        // 4. 根据行动力截取实际路径
        int actualSteps = Mathf.Min(mobility, targetIndexInPath);
        List<CellManager> actualPathWithStart = fullPath.GetRange(0, actualSteps + 1);

        // 5. 开启行走动画并启动移动协程
        SetWalkAnimationState(true);
        moveCoroutine = StartCoroutine(MoveRoutine(actualPathWithStart, playerCell));
    }

    public override void CancelAction()
    {
        base.CancelAction();

        MonsterIsMoving = false; // 【状态切换】中断时解除锁定

        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
            moveCoroutine = null;
        }

        SetWalkAnimationState(false);
    }

    // ==================================================================
    // 移动与朝向逻辑
    // ==================================================================

    /// <summary>
    /// 逐格平移协程，移动结束后停顿再转向。
    /// 跨层移动时目标高度取目标格子的实际高度（Y 随层变化）。
    /// </summary>
    private IEnumerator MoveRoutine(List<CellManager> path, CellManager playerCell)
    {
        MonsterIsMoving = true; // 【状态切换】开始移动时加锁

        for (int i = 1; i < path.Count; i++)
        {
            CellManager currentCell = path[i - 1];
            CellManager targetCell = path[i];
            if (currentCell == null || targetCell == null) continue;

            // 实时设置每一步的行走朝向
            UpdateDirectionAnimation(currentCell, targetCell);

            Vector3 targetPos = targetCell.transform.position;
            Vector3 startPos = transform.position;

            float elapsed = 0f;
            while (elapsed < stepDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / stepDuration);
                transform.position = Vector3.Lerp(startPos, targetPos, t);
                yield return null;
            }

            transform.position = targetPos;
        }

        // 1. 移动完成，先停止行走动画，保持停顿
        SetWalkAnimationState(false);

        // 2. 停顿指定的秒数（例如 0.5s）
        if (pauseBeforeFacePlayer > 0f)
        {
            yield return new WaitForSeconds(pauseBeforeFacePlayer);
        }

        // 3. 停顿结束后，转向玩家所在格
        CellManager finalCell = path[path.Count - 1];
        FaceTowardsTarget(finalCell, playerCell);

        moveCoroutine = null;
        MonsterIsMoving = false; // 【状态切换】彻底结束时解除锁定

        // 4. 动作完成
        CompleteAction();
    }

    /// <summary>
    /// 当怪物不需要移动时，停顿一会儿再转向玩家
    /// </summary>
    private IEnumerator StationaryTurnRoutine(CellManager currentCell, CellManager playerCell)
    {
        MonsterIsMoving = true; // 【状态切换】原地行为也视为移动中加锁
        SetWalkAnimationState(false);

        if (pauseBeforeFacePlayer > 0f)
        {
            yield return new WaitForSeconds(pauseBeforeFacePlayer);
        }

        FaceTowardsTarget(currentCell, playerCell);

        moveCoroutine = null;
        MonsterIsMoving = false; // 【状态切换】解除锁定
        CompleteAction();
    }

    /// <summary>
    /// 将朝向转向目标格（玩家所在格）
    /// </summary>
    private void FaceTowardsTarget(CellManager currentCell, CellManager targetCell)
    {
        if (currentCell == null || targetCell == null) return;

        var (curX, curZ) = gridManager.GetCellGridPosition(currentCell);
        var (tgtX, tgtZ) = gridManager.GetCellGridPosition(targetCell);
        int deltaX = tgtX - curX;
        int deltaY = tgtZ - curZ;

        if (deltaX == 0 && deltaY == 0) return;

        if (deltaX != 0 && deltaY == 0)
        {
            UpdateDirectionAnimation(currentCell, targetCell);
        }
        else if (deltaY != 0 && deltaX == 0)
        {
            UpdateDirectionAnimation(currentCell, targetCell);
        }
        else
        {
            UpdateDirectionAnimation(currentCell, targetCell);
        }
    }

    /// <summary>
    /// 根据相对位置更新 Animator 参数
    /// </summary>
    private void UpdateDirectionAnimation(CellManager current, CellManager next)
    {
        if (monsterAnimator == null) return;
        if (current == null || next == null) return;

        var (curX, curZ) = gridManager.GetCellGridPosition(current);
        var (nextX, nextZ) = gridManager.GetCellGridPosition(next);
        int deltaX = nextX - curX;
        int deltaY = nextZ - curZ;

        float h = 0f;
        float v = 0f;

        if (deltaX < 0)
        {
            h = -1f;
            v = -1f;
        }
        else if (deltaX > 0)
        {
            h = 1f;
            v = 1f;
        }
        else if (deltaY < 0)
        {
            h = -1f;
            v = 1f;
        }
        else if (deltaY > 0)
        {
            h = 1f;
            v = -1f;
        }

        monsterAnimator.SetFloat(horizontalFloatName, h);
        monsterAnimator.SetFloat(verticalFloatName, v);
    }

    private void SetWalkAnimationState(bool isWalking)
    {
        if (monsterAnimator != null)
        {
            monsterAnimator.SetBool(walkBoolName, isWalking);
        }
    }

    /// <summary>
    /// 查找怪物所在的具体格子（含层，遍历所有列的所有层）。
    /// </summary>
    private bool TryGetMonsterCell(out CellManager cell)
    {
        cell = null;
        for (int x = 0; x < gridManager.width; x++)
        {
            for (int z = 0; z < gridManager.height; z++)
            {
                foreach (CellManager c in gridManager.GetCellManagersInColumn(x, z))
                {
                    if (c != null && c.GetMonstersInside().Contains(selfIdentity))
                    {
                        cell = c;
                        return true;
                    }
                }
            }
        }
        return false;
    }

    /// <summary>
    /// 查找玩家所在的具体格子（含层，遍历所有列的所有层）。
    /// </summary>
    private bool TryGetPlayerCell(out CellManager cell)
    {
        cell = null;
        for (int x = 0; x < gridManager.width; x++)
        {
            for (int z = 0; z < gridManager.height; z++)
            {
                foreach (CellManager c in gridManager.GetCellManagersInColumn(x, z))
                {
                    if (c == null) continue;

                    Collider[] colliders = Physics.OverlapBox(c.transform.position, Vector3.one * 0.1f);
                    foreach (var col in colliders)
                    {
                        if (col.CompareTag("Player"))
                        {
                            cell = c;
                            return true;
                        }
                    }
                }
            }
        }
        return false;
    }

    // ==================================================================
    // A* 寻路算法（格子级，跨层）
    // ==================================================================

    private class PathCellNode
    {
        public CellManager cell;
        public int gCost;
        public int hCost;
        public PathCellNode parent;

        public int FCost => gCost + hCost;

        public PathCellNode(CellManager cell)
        {
            this.cell = cell;
        }
    }

    /// <summary>
    /// 格子级 A* 寻路：节点为具体格子（含层）。
    /// 邻居判定：四方向相邻列中，与该格层差 ≤ 1 的格子可通行
    /// （L2 ↔ L1 / L3 可连，L1 ↔ L3 不可连）。
    /// 会绕开悬崖/高台，只要存在连通路径即可找到最短路径。
    /// </summary>
    private List<CellManager> FindPathAStarCells(CellManager startCell, CellManager targetCell)
    {
        if (startCell == null || targetCell == null) return null;

        List<PathCellNode> openSet = new List<PathCellNode>();
        HashSet<CellManager> closedSet = new HashSet<CellManager>();
        Dictionary<CellManager, PathCellNode> nodeLookup = new Dictionary<CellManager, PathCellNode>();

        PathCellNode startNode = new PathCellNode(startCell);
        openSet.Add(startNode);
        nodeLookup[startCell] = startNode;

        var (startX, startZ) = gridManager.GetCellGridPosition(startCell);
        var (targetX, targetZ) = gridManager.GetCellGridPosition(targetCell);

        Vector2Int[] neighborDirections = new Vector2Int[]
        {
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
            new Vector2Int(-1, 0),
            new Vector2Int(1, 0)
        };

        while (openSet.Count > 0)
        {
            PathCellNode currentNode = openSet[0];
            for (int i = 1; i < openSet.Count; i++)
            {
                if (openSet[i].FCost < currentNode.FCost ||
                   (openSet[i].FCost == currentNode.FCost && openSet[i].hCost < currentNode.hCost))
                {
                    currentNode = openSet[i];
                }
            }

            openSet.Remove(currentNode);
            closedSet.Add(currentNode.cell);

            if (currentNode.cell == targetCell)
            {
                return RetraceCellPath(startNode, currentNode);
            }

            var (curX, curZ) = gridManager.GetCellGridPosition(currentNode.cell);

            foreach (Vector2Int dir in neighborDirections)
            {
                Vector2Int neighborCol = new Vector2Int(curX + dir.x, curZ + dir.y);

                if (!gridManager.IsValidGridPosition(neighborCol.x, neighborCol.y)) continue;

                // 遍历邻居列所有层，只连接层差 ≤ 1 的格子
                foreach (CellManager neighborCell in gridManager.GetCellManagersInColumn(neighborCol.x, neighborCol.y))
                {
                    if (neighborCell == null) continue;
                    if (closedSet.Contains(neighborCell)) continue;
                    if (neighborCell.IsLocked) continue; // 状态锁：怪物不可走上/穿过
                    // 跨层连接规则：层差 ≤ 1 可走（≥ 2 视为悬崖/高台，不可通行）
                    if (!RangeSystem.CanConnectAcrossLayers(gridManager, currentNode.cell, neighborCell)) continue;

                    int newCostToNeighbor = currentNode.gCost + 1;

                    PathCellNode neighborNode;
                    if (!nodeLookup.TryGetValue(neighborCell, out neighborNode))
                    {
                        neighborNode = new PathCellNode(neighborCell);
                        nodeLookup[neighborCell] = neighborNode;
                    }

                    if (newCostToNeighbor < neighborNode.gCost || !openSet.Contains(neighborNode))
                    {
                        var (nx, nz) = gridManager.GetCellGridPosition(neighborCell);
                        neighborNode.gCost = newCostToNeighbor;
                        neighborNode.hCost = Mathf.Abs(nx - targetX) + Mathf.Abs(nz - targetZ);
                        neighborNode.parent = currentNode;

                        if (!openSet.Contains(neighborNode))
                        {
                            openSet.Add(neighborNode);
                        }
                    }
                }
            }
        }

        return null;
    }

    private List<CellManager> RetraceCellPath(PathCellNode startNode, PathCellNode endNode)
    {
        List<CellManager> path = new List<CellManager>();
        PathCellNode curr = endNode;
        while (curr != null)
        {
            path.Add(curr.cell);
            curr = curr.parent;
        }
        path.Reverse();
        return path;
    }
}
