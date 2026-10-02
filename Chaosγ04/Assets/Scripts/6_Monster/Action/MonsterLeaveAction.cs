using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Leave action. It scans the monster's movement range for hostile units and, when at
/// least three hostile units are inside that range, scores reachable cells by their global
/// layered-graph distance from all hostile threats. Enemy traversal and occupancy are not
/// considered when scoring candidates. It only writes a forced destination into
/// MonsterMoveAction; MoveAction remains responsible for the movement.
/// </summary>
[DisallowMultipleComponent]
public class MonsterLeaveAction : MonsterActionBase
{
    private const int UnreachableThreatDistance = int.MaxValue / 2;

    [Header("远离阈值")]
    [Tooltip("移动范围内存在多少个敌对单位时触发远离。")]
    [SerializeField, Min(1)] private int hostileCountThreshold = 3;

    [Header("引用")]
    public MonsterMoveAction moveAction;
    public MonsterAttackAction attackAction;
    public GridManager gridManager;

    private MonsterIdentityManager selfIdentity;
    private MonsterHateSystem hateSystem;
    private MonsterActionManager actionManager;

    private void Awake()
    {
        ResolveReferences();
    }

    public override bool CanExecute(MonsterActionContext context)
    {
        ResolveReferences();

        if (moveAction == null || actionManager == null)
        {
            return false;
        }

        int leaveIndex = actionManager.GetActionIndex(this);
        int moveIndex = actionManager.GetActionIndex(moveAction);

        if (leaveIndex < 0 || moveIndex < 0 || leaveIndex >= moveIndex)
        {
            Debug.LogWarning(
                $"[{name}] MonsterLeaveAction 必须排在 MonsterMoveAction 之前，当前配置无效。");
            return false;
        }

        if (attackAction != null)
        {
            int attackIndex = actionManager.GetActionIndex(attackAction);
            if (attackIndex >= 0 && leaveIndex >= attackIndex)
            {
                Debug.LogWarning(
                    $"[{name}] MonsterLeaveAction 必须排在 MonsterAttackAction 之前，当前配置无效。");
                return false;
            }
        }

        return true;
    }

    public override void OnSkipped()
    {
        ClearTurnState();
    }

    protected override void OnStart()
    {
        ResolveReferences();
        EnsureGridManager();

        if (gridManager == null || selfIdentity == null || hateSystem == null || moveAction == null)
        {
            Debug.LogWarning($"[{name}] MonsterLeaveAction 缺少必要引用，跳过。");
            CompleteAction();
            return;
        }

        gridManager.EnsureGridSystemInitialized();

        CellManager monsterCell = MonsterPathfinding.FindMonsterCell(gridManager, selfIdentity);
        if (monsterCell == null)
        {
            ClearTurnState();
            CompleteAction();
            return;
        }

        List<MonsterTarget> hostileTargets = hateSystem.GetHostileTargets(gridManager);
        List<MonsterTarget> hostilesInRange = GetHostilesInMoveRange(monsterCell, hostileTargets);
        if (hostilesInRange.Count < hostileCountThreshold)
        {
            ClearTurnState();
            CompleteAction();
            return;
        }

        CellManager leaveDestination = CalculateLeaveDestination(monsterCell, hostileTargets);
        if (leaveDestination == null)
        {
            ClearTurnState();
            CompleteAction();
            return;
        }

        moveAction.SetForcedDestination(leaveDestination);
        if (attackAction != null)
        {
            attackAction.SetSuppressedForTurn(true);
        }

        CompleteAction();
    }

    private List<MonsterTarget> GetHostilesInMoveRange(
        CellManager monsterCell,
        List<MonsterTarget> hostileTargets)
    {
        List<MonsterTarget> result = new List<MonsterTarget>();
        if (hostileTargets == null)
        {
            return result;
        }

        foreach (MonsterTarget target in hostileTargets)
        {
            if (target.Cell == null)
            {
                continue;
            }

            if (target.IsPlayer && !ConcealmentCell.CanMonsterSeePlayer(monsterCell))
            {
                continue;
            }

            int distance = MonsterPathfinding.GetPathDistance(
                gridManager,
                selfIdentity,
                monsterCell,
                target.Cell);

            if (distance != int.MaxValue && distance <= moveAction.Mobility)
            {
                result.Add(target);
            }
        }

        return result;
    }

    private CellManager CalculateLeaveDestination(
        CellManager monsterCell,
        List<MonsterTarget> threats)
    {
        HashSet<CellManager> landableCells = MonsterPathfinding.CalculateReachableCells(
            gridManager,
            selfIdentity,
            monsterCell,
            moveAction.Mobility,
            blockByMonsters: true,
            includePlayerCell: false);

        landableCells.Remove(monsterCell);

        CellManager bestCell = null;
        int bestMinDistance = int.MinValue;
        long bestTotalDistance = long.MinValue;
        int bestSelfDistance = int.MinValue;

        foreach (CellManager candidate in landableCells)
        {
            if (candidate == null || candidate.IsPlayerInside)
            {
                continue;
            }

            long totalDistance = 0;
            int minDistance = int.MaxValue;

            foreach (MonsterTarget threat in threats)
            {
                int distance = MonsterPathfinding.GetGlobalPathDistance(
                    gridManager,
                    threat.Cell,
                    candidate);

                if (distance == int.MaxValue)
                {
                    distance = UnreachableThreatDistance;
                }

                totalDistance += distance;
                if (distance < minDistance)
                {
                    minDistance = distance;
                }
            }

            int selfDistance = MonsterPathfinding.GetPathDistance(
                gridManager,
                selfIdentity,
                monsterCell,
                candidate);

            bool better =
                minDistance > bestMinDistance ||
                (minDistance == bestMinDistance && totalDistance > bestTotalDistance) ||
                (minDistance == bestMinDistance &&
                 totalDistance == bestTotalDistance &&
                 selfDistance > bestSelfDistance);

            if (better)
            {
                bestMinDistance = minDistance;
                bestTotalDistance = totalDistance;
                bestSelfDistance = selfDistance;
                bestCell = candidate;
            }
        }

        return bestCell;
    }

    private void ClearTurnState()
    {
        if (moveAction != null)
        {
            moveAction.ClearForcedDestination();
        }

        if (attackAction != null)
        {
            attackAction.SetSuppressedForTurn(false);
        }
    }

    private void ResolveReferences()
    {
        if (selfIdentity == null)
        {
            selfIdentity = GetComponent<MonsterIdentityManager>();
        }

        if (hateSystem == null)
        {
            hateSystem = MonsterHateSystem.EnsureOn(selfIdentity);
        }

        if (actionManager == null)
        {
            actionManager = GetComponent<MonsterActionManager>();
        }

        if (moveAction == null)
        {
            moveAction = GetComponent<MonsterMoveAction>();
            if (moveAction == null && actionManager != null)
            {
                moveAction = actionManager.GetActionOfType<MonsterMoveAction>();
            }
        }

        if (attackAction == null)
        {
            attackAction = GetComponent<MonsterAttackAction>();
            if (attackAction == null && actionManager != null)
            {
                attackAction = actionManager.GetActionOfType<MonsterAttackAction>();
            }
        }
    }

    private void EnsureGridManager()
    {
        if (gridManager == null)
        {
            gridManager = FindFirstObjectByType<GridManager>();
        }
    }
}
