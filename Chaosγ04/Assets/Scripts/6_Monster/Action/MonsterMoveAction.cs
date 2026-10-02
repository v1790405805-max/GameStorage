using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster movement action.
///
/// This action only moves the monster. A forced destination can be supplied by
/// MonsterLeaveAction. When no forced destination exists, it chooses an approachable
/// hostile target from the monster's hate state and moves as close as possible to it.
/// </summary>
public class MonsterMoveAction : MonsterActionBase
{
    public bool MonsterIsMoving { get; private set; }

    [Header("移动属性")]
    [Tooltip("怪物每平移一格所消耗的时间（秒）")]
    public float stepDuration = 0.3f;

    [Tooltip("移动结束停顿多久后再转向目标（秒）")]
    public float pauseBeforeFacePlayer = 0.5f;

    [Header("网格管理器引用")]
    public GridManager gridManager;

    [Header("移动动画控制")]
    public Animator monsterAnimator;
    public string moveBoolName = "Move";
    public string horizontalFloatName = "Horizontal";
    public string verticalFloatName = "Vertical";

    private MonsterIdentityManager selfIdentity;
    private MonsterHateSystem hateSystem;
    private MonsterStats monsterStats;
    private Coroutine moveCoroutine;

    private CellManager forcedDestination;
    private bool hasForcedDestination;

    public int Mobility => monsterStats != null ? monsterStats.mobility : 0;

    private void Awake()
    {
        selfIdentity = GetComponent<MonsterIdentityManager>();
        monsterStats = GetComponent<MonsterStats>();
        hateSystem = MonsterHateSystem.EnsureOn(selfIdentity);

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
            gridManager = FindFirstObjectByType<GridManager>();
        }

        if (gridManager != null)
        {
            gridManager.EnsureGridSystemInitialized();
        }
    }

    public override bool CanExecute(MonsterActionContext context)
    {
        return true;
    }

    public override void OnSkipped()
    {
        MonsterIsMoving = false;
        SetMoveAnimationState(false);
    }

    protected override void OnStart()
    {
        EnsureGridManager();
        EnsureMonsterStats();
        EnsureHateSystem();

        if (gridManager == null || selfIdentity == null || monsterStats == null)
        {
            Debug.LogError($"[{name}] 缺少 GridManager / MonsterIdentityManager / MonsterStats，无法执行移动！");
            SetMoveAnimationState(false);
            CompleteAction();
            return;
        }

        CellManager monsterCell = MonsterPathfinding.FindMonsterCell(gridManager, selfIdentity);
        if (monsterCell == null)
        {
            Debug.LogWarning($"[{name}] 未在任何 CellManager 中匹配到当前怪物，取消移动。");
            SetMoveAnimationState(false);
            CompleteAction();
            return;
        }

        if (hasForcedDestination && forcedDestination != null)
        {
            ExecuteForcedMove(monsterCell, forcedDestination);
            return;
        }

        ExecuteDefaultMove(monsterCell);
    }

    public override void CancelAction()
    {
        base.CancelAction();

        MonsterIsMoving = false;

        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
            moveCoroutine = null;
        }

        SetMoveAnimationState(false);
    }

    public void SetForcedDestination(CellManager destination)
    {
        forcedDestination = destination;
        hasForcedDestination = destination != null;
    }

    public void ClearForcedDestination()
    {
        forcedDestination = null;
        hasForcedDestination = false;
    }

    public void ResetTurnState()
    {
        ClearForcedDestination();
    }

    private void ExecuteForcedMove(CellManager monsterCell, CellManager destinationCell)
    {
        if (destinationCell == monsterCell)
        {
            moveCoroutine = StartCoroutine(StationaryTurnRoutine(monsterCell, null));
            return;
        }

        List<CellManager> path = FindMovementPath(
            monsterCell,
            destinationCell,
            includeTargetCellInBlockedCheck: true);

        if (path == null || path.Count <= 1)
        {
            moveCoroutine = StartCoroutine(StationaryTurnRoutine(monsterCell, null));
            return;
        }

        int actualSteps = Mathf.Min(Mobility, path.Count - 1);
        if (actualSteps <= 0)
        {
            moveCoroutine = StartCoroutine(StationaryTurnRoutine(monsterCell, null));
            return;
        }

        List<CellManager> actualPath = path.GetRange(0, actualSteps + 1);

        SetMoveAnimationState(true);
        moveCoroutine = StartCoroutine(MoveRoutine(actualPath, null));
    }

    private void ExecuteDefaultMove(CellManager monsterCell)
    {
        bool includePlayer =
            !TargetLossEffect.MonstersLosePlayerTargetThisRound &&
            ConcealmentCell.CanMonsterSeePlayer(monsterCell);

        if (!TrySelectDefaultTarget(includePlayer, out MonsterTarget target, out _))
        {
            moveCoroutine = StartCoroutine(StationaryTurnRoutine(monsterCell, null));
            return;
        }

        List<CellManager> path = FindMovementPath(
            monsterCell,
            target.Cell,
            includeTargetCellInBlockedCheck: false);

        if (path == null || path.Count <= 1)
        {
            moveCoroutine = StartCoroutine(StationaryTurnRoutine(monsterCell, target.Cell));
            return;
        }

        bool pathReachesTarget = path[path.Count - 1] == target.Cell;
        int targetIndexInPath = pathReachesTarget
            ? path.Count - 2
            : path.Count - 1;
        if (targetIndexInPath <= 0)
        {
            moveCoroutine = StartCoroutine(StationaryTurnRoutine(monsterCell, target.Cell));
            return;
        }

        int actualSteps = Mathf.Min(Mobility, targetIndexInPath);
        List<CellManager> actualPath = path.GetRange(0, actualSteps + 1);

        SetMoveAnimationState(true);
        moveCoroutine = StartCoroutine(MoveRoutine(actualPath, target.Cell));
    }

    private bool TrySelectDefaultTarget(
        bool includePlayer,
        out MonsterTarget target,
        out int distance)
    {
        target = null;
        distance = int.MaxValue;
        if (hateSystem == null)
        {
            return false;
        }

        if (hateSystem.TrySelectTarget(
                gridManager,
                int.MaxValue,
                includePlayer,
                out target,
                out distance,
                out _,
                ignoreUnitBlockers: true))
        {
            return true;
        }

        if (includePlayer)
        {
            return hateSystem.TrySelectTarget(
                gridManager,
                int.MaxValue,
                false,
                out target,
                out distance,
                out _,
                ignoreUnitBlockers: true);
        }

        return false;
    }

    private List<CellManager> FindMovementPath(
        CellManager startCell,
        CellManager targetCell,
        bool includeTargetCellInBlockedCheck)
    {
        // Other units are temporary traffic, not terrain that makes the target unreachable.
        // Plan the ideal route first, then use a blocker-aware route if execution would stop.
        List<CellManager> preferredPath = MonsterPathfinding.FindPath(
            gridManager,
            selfIdentity,
            startCell,
            targetCell,
            ignoreUnitBlockers: true);

        if (preferredPath == null || preferredPath.Count <= 1)
        {
            return preferredPath;
        }

        if (!HasBlockedStep(
                preferredPath,
                includeTargetCellInBlockedCheck))
        {
            return preferredPath;
        }

        List<CellManager> blockedAwarePath =
            MonsterPathfinding.FindPathToClosestReachableCell(
                gridManager,
                selfIdentity,
                startCell,
                targetCell);

        return blockedAwarePath != null && blockedAwarePath.Count > 0
            ? blockedAwarePath
            : preferredPath;
    }

    private bool HasBlockedStep(
        List<CellManager> path,
        bool includeTargetCellInBlockedCheck)
    {
        if (path == null || path.Count <= 1)
        {
            return false;
        }

        int lastIndex = includeTargetCellInBlockedCheck
            ? path.Count - 1
            : path.Count - 2;

        for (int i = 1; i <= lastIndex; i++)
        {
            if (IsCellBlockedForMonster(path[i]))
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerator MoveRoutine(List<CellManager> path, CellManager faceTarget)
    {
        MonsterIsMoving = true;
        CellManager reachedCell = path[0];

        for (int i = 1; i < path.Count; i++)
        {
            CellManager currentCell = path[i - 1];
            CellManager targetCell = path[i];
            if (currentCell == null || targetCell == null)
            {
                continue;
            }

            if (IsCellBlockedForMonster(targetCell))
            {
                break;
            }

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
            reachedCell = targetCell;
        }

        SetMoveAnimationState(false);

        if (pauseBeforeFacePlayer > 0f)
        {
            yield return new WaitForSeconds(pauseBeforeFacePlayer);
        }

        FaceTowardsTarget(reachedCell, faceTarget);

        moveCoroutine = null;
        MonsterIsMoving = false;
        CompleteAction();
    }

    private IEnumerator StationaryTurnRoutine(CellManager currentCell, CellManager faceTarget)
    {
        MonsterIsMoving = true;
        SetMoveAnimationState(false);

        if (pauseBeforeFacePlayer > 0f)
        {
            yield return new WaitForSeconds(pauseBeforeFacePlayer);
        }

        FaceTowardsTarget(currentCell, faceTarget);

        moveCoroutine = null;
        MonsterIsMoving = false;
        CompleteAction();
    }

    private void FaceTowardsTarget(CellManager currentCell, CellManager targetCell)
    {
        if (currentCell == null || targetCell == null)
        {
            return;
        }

        UpdateDirectionAnimation(currentCell, targetCell);
    }

    private void UpdateDirectionAnimation(CellManager current, CellManager next)
    {
        if (monsterAnimator == null || current == null || next == null)
        {
            return;
        }

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

    private void SetMoveAnimationState(bool isMoving)
    {
        if (monsterAnimator != null)
        {
            monsterAnimator.SetBool(moveBoolName, isMoving);
        }
    }

    private bool IsCellBlockedForMonster(CellManager cell)
    {
        if (cell == null || !cell.CanMonsterTraverse(selfIdentity))
        {
            return true;
        }

        if (cell.IsPlayerInside)
        {
            return true;
        }

        foreach (MonsterIdentityManager monster in cell.GetMonstersInside())
        {
            if (monster == null || monster == selfIdentity)
            {
                continue;
            }

            if (monster.gameObject.activeInHierarchy)
            {
                return true;
            }
        }

        return false;
    }

    private void EnsureHateSystem()
    {
        if (hateSystem == null)
        {
            hateSystem = MonsterHateSystem.EnsureOn(selfIdentity);
        }
    }

    private void EnsureMonsterStats()
    {
        if (monsterStats == null)
        {
            monsterStats = GetComponent<MonsterStats>();
        }
    }
}
