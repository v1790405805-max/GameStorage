using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class MonsterAttackPriorityEntry
{
    public MonsterAttackActionBase attackAction;
    public int priority;
}

/// <summary>
/// Builds one combat plan for the current turn. It only makes a decision and
/// completes immediately; movement and attacks remain separate action steps.
/// </summary>
[DisallowMultipleComponent]
public class MonsterCombatPlannerAction : MonsterActionBase
{
    [Header("攻击优先级")]
    [Tooltip("Planner 是攻击优先级的唯一配置入口；未登记的攻击使用优先级 0。")]
    [SerializeField]
    private List<MonsterAttackPriorityEntry> attackPriorities =
        new List<MonsterAttackPriorityEntry>();

    private MonsterHateSystem hateSystem;

    public override MonsterActionCategory Category => MonsterActionCategory.Planning;
    public override float PostActionDelay => 0f;

    public override bool CanExecute(MonsterActionContext context)
    {
        if (context == null ||
            context.Self == null ||
            context.GridManager == null ||
            context.AttackActions == null ||
            context.AttackActions.Count == 0)
        {
            return false;
        }

        if (context.IsPatrolling ||
            (context.WarnAction != null &&
             context.WarnAction.IsRestricting))
        {
            return false;
        }

        if (context.MoveAction != null &&
            context.MoveAction.HasForcedDestination)
        {
            return false;
        }

        return true;
    }

    protected override void OnStart()
    {
        MonsterActionContext context = Context;
        if (context == null ||
            context.Self == null ||
            context.GridManager == null)
        {
            CompleteAction();
            return;
        }

        if (context.IsPatrolling ||
            (context.WarnAction != null &&
             context.WarnAction.IsRestricting))
        {
            CompleteAction();
            return;
        }

        context.GridManager.EnsureGridSystemInitialized();
        hateSystem = MonsterHateSystem.EnsureOn(context.Self);
        if (hateSystem == null)
        {
            CompleteAction();
            return;
        }

        CellManager startCell = MonsterGridNavigation.FindMonsterCell(
            context.GridManager,
            context.Self);
        if (startCell == null)
        {
            CompleteAction();
            return;
        }

        List<MonsterTarget> targets =
            hateSystem.GetHostileTargets(context.GridManager);
        if (targets == null || targets.Count == 0)
        {
            CompleteAction();
            return;
        }

        int mobility = context.MoveAction != null
            ? context.MoveAction.Mobility
            : 0;
        HashSet<CellManager> reachableCells =
            MonsterGridNavigation.CalculateReachableCells(
                context.GridManager,
                context.Self,
                startCell,
                mobility,
                blockByMonsters: true,
                includePlayerCell: false);
        reachableCells.Add(startCell);

        CombatCandidate bestAttack = null;
        CombatCandidate bestApproach = null;

        foreach (MonsterTarget target in targets)
        {
            if (target == null || target.Cell == null)
            {
                continue;
            }

            int targetPathDistance = MonsterGridNavigation.GetPathDistance(
                context.GridManager,
                context.Self,
                startCell,
                target.Cell,
                ignoreUnitBlockers: true);
            int targetHate = hateSystem.GetTotalHate(
                target,
                targetPathDistance);

            foreach (CellManager destination in reachableCells)
            {
                if (destination == null)
                {
                    continue;
                }

                bool playerVisible = !target.IsPlayer ||
                    ConcealmentCell.CanMonsterSeePlayer(destination);
                if (target.IsPlayer && !playerVisible)
                {
                    continue;
                }

                int movementSteps = GetMovementSteps(
                    context,
                    startCell,
                    destination);

                foreach (MonsterAttackActionBase attack in context.AttackActions)
                {
                    if (attack == null)
                    {
                        continue;
                    }

                    int attackPriority = GetAttackPriority(attack);
                    if (attack.TryEvaluateAttackFrom(
                            destination,
                            target,
                            playerVisible,
                            out int attackDistance))
                    {
                        CombatCandidate candidate = new CombatCandidate
                        {
                            Attack = attack,
                            Target = target,
                            Destination = destination,
                            AttackPriority = attackPriority,
                            TargetHate = targetHate,
                            AttackDistance = attackDistance,
                            MovementSteps = movementSteps
                        };

                        if (IsBetterAttackCandidate(candidate, bestAttack))
                        {
                            bestAttack = candidate;
                        }

                        continue;
                    }

                    int targetDistance = MonsterGridNavigation.GetPathDistance(
                        context.GridManager,
                        context.Self,
                        destination,
                        target.Cell,
                        ignoreUnitBlockers: true);
                    int approachGap = targetDistance == int.MaxValue
                        ? int.MaxValue
                        : Mathf.Max(
                            0,
                            targetDistance - attack.PreferredMaxRange);
                    CombatCandidate approachCandidate = new CombatCandidate
                    {
                        Attack = attack,
                        Target = target,
                        Destination = destination,
                        AttackPriority = attackPriority,
                        TargetHate = targetHate,
                        MovementSteps = movementSteps,
                        ApproachGap = approachGap
                    };

                    if (IsBetterApproachCandidate(
                            approachCandidate,
                            bestApproach))
                    {
                        bestApproach = approachCandidate;
                    }
                }
            }
        }

        CombatCandidate selected = bestAttack ?? bestApproach;
        if (selected == null)
        {
            CompleteAction();
            return;
        }

        context.CombatPlan = new MonsterCombatPlan
        {
            SelectedAttack = bestAttack != null ? selected.Attack : null,
            Target = selected.Target,
            MoveDestination = selected.Destination,
            PlannedAttackDistance = selected.AttackDistance,
            CanAttackAfterMove = bestAttack != null
        };

        CompleteAction();
    }

    private int GetAttackPriority(MonsterAttackActionBase attack)
    {
        if (attackPriorities != null)
        {
            foreach (MonsterAttackPriorityEntry entry in attackPriorities)
            {
                if (entry != null && entry.attackAction == attack)
                {
                    return entry.priority;
                }
            }
        }

        return 0;
    }

    private int GetMovementSteps(
        MonsterActionContext context,
        CellManager startCell,
        CellManager destination)
    {
        if (startCell == destination)
        {
            return 0;
        }

        int distance = MonsterGridNavigation.GetPathDistance(
            context.GridManager,
            context.Self,
            startCell,
            destination,
            ignoreUnitBlockers: true);
        return distance == int.MaxValue ? 0 : distance;
    }

    private bool IsBetterAttackCandidate(
        CombatCandidate candidate,
        CombatCandidate currentBest)
    {
        if (currentBest == null)
        {
            return true;
        }

        if (candidate.TargetHate != currentBest.TargetHate)
        {
            return candidate.TargetHate > currentBest.TargetHate;
        }

        if (candidate.AttackPriority != currentBest.AttackPriority)
        {
            return candidate.AttackPriority > currentBest.AttackPriority;
        }

        bool candidateStays =
            candidate.MovementSteps == 0;
        bool currentStays =
            currentBest.MovementSteps == 0;
        if (candidateStays != currentStays)
        {
            return candidateStays;
        }

        int candidateRangeGap = Mathf.Abs(
            candidate.Attack.PreferredMaxRange -
            candidate.AttackDistance);
        int currentRangeGap = Mathf.Abs(
            currentBest.Attack.PreferredMaxRange -
            currentBest.AttackDistance);
        if (candidateRangeGap != currentRangeGap)
        {
            return candidateRangeGap < currentRangeGap;
        }

        if (candidate.AttackDistance != currentBest.AttackDistance)
        {
            return candidate.AttackDistance > currentBest.AttackDistance;
        }

        if (candidate.MovementSteps != currentBest.MovementSteps)
        {
            return candidate.MovementSteps < currentBest.MovementSteps;
        }

        return IsEarlierCell(candidate.Destination, currentBest.Destination);
    }

    private bool IsBetterApproachCandidate(
        CombatCandidate candidate,
        CombatCandidate currentBest)
    {
        if (currentBest == null)
        {
            return true;
        }

        if (candidate.ApproachGap != currentBest.ApproachGap)
        {
            return candidate.ApproachGap < currentBest.ApproachGap;
        }

        if (candidate.TargetHate != currentBest.TargetHate)
        {
            return candidate.TargetHate > currentBest.TargetHate;
        }

        if (candidate.AttackPriority != currentBest.AttackPriority)
        {
            return candidate.AttackPriority > currentBest.AttackPriority;
        }

        if (candidate.MovementSteps != currentBest.MovementSteps)
        {
            return candidate.MovementSteps < currentBest.MovementSteps;
        }

        return IsEarlierCell(candidate.Destination, currentBest.Destination);
    }

    private bool IsEarlierCell(CellManager candidate, CellManager currentBest)
    {
        MonsterActionContext context = Context;
        if (context == null ||
            context.GridManager == null ||
            candidate == null ||
            currentBest == null)
        {
            return false;
        }

        var (candidateX, candidateZ) =
            context.GridManager.GetCellGridPosition(candidate);
        var (bestX, bestZ) =
            context.GridManager.GetCellGridPosition(currentBest);
        return candidateX < bestX ||
               (candidateX == bestX && candidateZ < bestZ);
    }

    private sealed class CombatCandidate
    {
        public MonsterAttackActionBase Attack;
        public MonsterTarget Target;
        public CellManager Destination;
        public int AttackPriority;
        public int TargetHate;
        public int AttackDistance;
        public int MovementSteps;
        public int ApproachGap;
    }
}
