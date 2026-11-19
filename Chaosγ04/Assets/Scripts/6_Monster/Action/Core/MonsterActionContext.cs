using System.Collections.Generic;

public sealed class MonsterCombatPlan
{
    public MonsterAttackActionBase SelectedAttack { get; set; }
    public MonsterTarget Target { get; set; }
    public CellManager MoveDestination { get; set; }
    public int PlannedAttackDistance { get; set; }
    public bool CanAttackAfterMove { get; set; }

    public bool HasSelectedAttack =>
        CanAttackAfterMove &&
        SelectedAttack != null &&
        Target != null &&
        Target.Cell != null;
}

/// <summary>
/// Shared execution state for one monster action sequence.
/// </summary>
public sealed class MonsterActionContext
{
    public MonsterIdentityManager Self { get; }
    public GridManager GridManager { get; }
    public MonsterMoveAction MoveAction { get; }
    public IReadOnlyList<MonsterAttackActionBase> AttackActions { get; }
    public MonsterCombatPlan CombatPlan { get; set; }
    public MonsterWarnAction WarnAction { get; }
    public bool IsPatrolling { get; set; }
    public CellManager PatrolDestination { get; set; }

    public MonsterActionContext(
        MonsterIdentityManager self,
        GridManager gridManager,
        MonsterMoveAction moveAction,
        IReadOnlyList<MonsterAttackActionBase> attackActions,
        MonsterWarnAction warnAction)
    {
        Self = self;
        GridManager = gridManager;
        MoveAction = moveAction;
        AttackActions = attackActions;
        WarnAction = warnAction;
    }
}
