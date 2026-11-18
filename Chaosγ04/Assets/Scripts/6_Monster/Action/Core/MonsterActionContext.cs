/// <summary>
/// Shared execution state for one monster action sequence.
/// </summary>
public sealed class MonsterActionContext
{
    public MonsterIdentityManager Self { get; }
    public GridManager GridManager { get; }
    public MonsterAttackAction AttackAction { get; }

    public MonsterActionContext(
        MonsterIdentityManager self,
        GridManager gridManager,
        MonsterAttackAction attackAction)
    {
        Self = self;
        GridManager = gridManager;
        AttackAction = attackAction;
    }
}
