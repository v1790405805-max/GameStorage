using System.Collections.Generic;
using UnityEngine;

public enum MonsterWarnRangeType
{
    Straight,
    Diamond,
    Square
}

/// <summary>
/// Controls whether a monster is still in its warning/patrol phase.
/// Once any hostile target enters warning range, the restriction is permanently
/// removed for this component instance.
/// </summary>
[DisallowMultipleComponent]
public sealed class MonsterWarnAction : MonsterActionBase
{
    [Header("网格管理器引用")]
    [SerializeField] private GridManager gridManager;

    [Header("是否启用警戒")]
    [Tooltip("关闭后，本动作不会限制 Planner、Move 或 Attack。")]
    [SerializeField] private bool warningEnabled = true;

    [Header("警戒范围")]
    [SerializeField] private MonsterWarnRangeType rangeType =
        MonsterWarnRangeType.Diamond;
    [SerializeField, Min(0)] private int rangeDistance = 4;

    [Header("巡逻点")]
    [SerializeField] private CellManager patrolPointA;
    [SerializeField] private CellManager patrolPointB;

    private MonsterIdentityManager selfIdentity;
    private MonsterHateSystem hateSystem;
    private CellManager activePatrolDestination;
    private bool hasDetectedHostile;

    public bool IsWarningEnabled => warningEnabled;
    public bool HasDetectedHostile => hasDetectedHostile;
    public bool IsRestricting =>
        warningEnabled && !hasDetectedHostile;

    public override MonsterActionCategory Category =>
        MonsterActionCategory.Planning;

    public override float PostActionDelay => 0f;

    public override bool CanExecute(MonsterActionContext context)
    {
        return warningEnabled &&
               !hasDetectedHostile &&
               context != null &&
               context.Self != null;
    }

    public override void ResetTurnState()
    {
        if (!warningEnabled)
        {
            activePatrolDestination = null;
        }
    }

    protected override void OnStart()
    {
        MonsterActionContext context = Context;
        ResolveReferences(context);

        if (!warningEnabled || hasDetectedHostile)
        {
            CompleteAction();
            return;
        }

        if (context == null ||
            selfIdentity == null ||
            gridManager == null ||
            hateSystem == null)
        {
            CompleteAction();
            return;
        }

        gridManager.EnsureGridSystemInitialized();

        CellManager monsterCell = MonsterGridNavigation.FindMonsterCell(
            gridManager,
            selfIdentity);
        if (monsterCell == null)
        {
            CompleteAction();
            return;
        }

        if (HasHostileTargetInWarningRange(monsterCell))
        {
            hasDetectedHostile = true;
            CompleteAction();
            return;
        }

        context.IsPatrolling = true;
        context.PatrolDestination =
            SelectNextPatrolDestination(monsterCell);
        CompleteAction();
    }

    private bool HasHostileTargetInWarningRange(CellManager monsterCell)
    {
        List<MonsterTarget> targets =
            hateSystem.GetHostileTargets(gridManager);
        if (targets == null)
        {
            return false;
        }

        foreach (MonsterTarget target in targets)
        {
            if (target == null || target.Cell == null)
            {
                continue;
            }

            if (target.IsPlayer &&
                !ConcealmentCell.CanMonsterSeePlayer(monsterCell))
            {
                continue;
            }

            if (IsCellWithinWarningRange(monsterCell, target.Cell))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsCellWithinWarningRange(
        CellManager originCell,
        CellManager targetCell)
    {
        if (originCell == null || targetCell == null)
        {
            return false;
        }

        if (originCell == targetCell)
        {
            return true;
        }

        var (originX, originZ) =
            gridManager.GetCellGridPosition(originCell);
        var (targetX, targetZ) =
            gridManager.GetCellGridPosition(targetCell);
        int deltaX = Mathf.Abs(targetX - originX);
        int deltaZ = Mathf.Abs(targetZ - originZ);

        bool isInsideShape;
        switch (rangeType)
        {
            case MonsterWarnRangeType.Straight:
                isInsideShape =
                    (deltaX == 0 || deltaZ == 0) &&
                    deltaX + deltaZ <= rangeDistance;
                break;
            case MonsterWarnRangeType.Square:
                isInsideShape =
                    Mathf.Max(deltaX, deltaZ) <= rangeDistance;
                break;
            default:
                isInsideShape = deltaX + deltaZ <= rangeDistance;
                break;
        }

        if (!isInsideShape)
        {
            return false;
        }

        int pathDistance = MonsterGridNavigation.GetPathDistance(
            gridManager,
            selfIdentity,
            originCell,
            targetCell,
            ignoreUnitBlockers: true);
        return pathDistance != int.MaxValue &&
               pathDistance <= rangeDistance;
    }

    private CellManager SelectNextPatrolDestination(
        CellManager monsterCell)
    {
        if (activePatrolDestination == monsterCell)
        {
            activePatrolDestination =
                GetOtherPatrolPoint(activePatrolDestination);
        }

        if (!IsPatrolDestinationAvailable(
                activePatrolDestination,
                monsterCell))
        {
            activePatrolDestination = null;
        }

        if (activePatrolDestination == null)
        {
            if (monsterCell == patrolPointA)
            {
                activePatrolDestination = patrolPointB;
            }
            else if (monsterCell == patrolPointB)
            {
                activePatrolDestination = patrolPointA;
            }
            else if (IsPatrolDestinationAvailable(
                         patrolPointB,
                         monsterCell))
            {
                activePatrolDestination = patrolPointB;
            }
            else if (IsPatrolDestinationAvailable(
                         patrolPointA,
                         monsterCell))
            {
                activePatrolDestination = patrolPointA;
            }
        }

        if (!IsPatrolDestinationAvailable(
                activePatrolDestination,
                monsterCell))
        {
            activePatrolDestination = null;
        }

        return activePatrolDestination;
    }

    private CellManager GetOtherPatrolPoint(CellManager point)
    {
        if (point == patrolPointA)
        {
            return patrolPointB != patrolPointA
                ? patrolPointB
                : null;
        }

        if (point == patrolPointB)
        {
            return patrolPointA != patrolPointB
                ? patrolPointA
                : null;
        }

        return null;
    }

    private bool IsPatrolDestinationAvailable(
        CellManager destination,
        CellManager monsterCell)
    {
        if (destination == null ||
            destination == monsterCell ||
            !destination.gameObject.activeInHierarchy ||
            destination.IsPlayerInside ||
            !destination.CanMonsterTraverse(selfIdentity))
        {
            return false;
        }

        foreach (MonsterIdentityManager monster in
                 destination.GetMonstersInside())
        {
            if (monster != null &&
                monster != selfIdentity &&
                monster.gameObject.activeInHierarchy)
            {
                return false;
            }
        }

        int pathDistance = MonsterGridNavigation.GetPathDistance(
            gridManager,
            selfIdentity,
            monsterCell,
            destination,
            ignoreUnitBlockers: true);
        return pathDistance != int.MaxValue;
    }

    private void ResolveReferences(MonsterActionContext context)
    {
        if (selfIdentity == null && context != null)
        {
            selfIdentity = context.Self;
        }

        if (selfIdentity == null)
        {
            selfIdentity = GetComponent<MonsterIdentityManager>();
        }

        if (gridManager == null && context != null)
        {
            gridManager = context.GridManager;
        }

        if (gridManager == null)
        {
            gridManager = FindFirstObjectByType<GridManager>();
        }

        if (hateSystem == null)
        {
            hateSystem = MonsterHateSystem.EnsureOn(selfIdentity);
        }
    }
}
