using UnityEngine;

/// <summary>
/// Shared execution state for one monster action sequence.
/// </summary>
public sealed class MonsterActionContext
{
    public MonsterIdentityManager Self { get; }
    public GridManager GridManager { get; }
    public MonsterAttackAction AttackAction { get; }
    public bool HostileInAttackRangeAtTurnStart { get; }

    public MonsterActionContext(
        MonsterIdentityManager self,
        GridManager gridManager,
        MonsterAttackAction attackAction,
        bool hostileInAttackRangeAtTurnStart)
    {
        Self = self;
        GridManager = gridManager;
        AttackAction = attackAction;
        HostileInAttackRangeAtTurnStart = hostileInAttackRangeAtTurnStart;
    }

    public bool HasHostileInAttackRangeNow(int attackRange)
    {
        return MonsterTargetManager.HasHostileInAttackRange(
            Self,
            GridManager,
            attackRange);
    }
}
