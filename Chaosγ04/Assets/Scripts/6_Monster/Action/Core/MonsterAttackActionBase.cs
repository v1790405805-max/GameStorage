/// <summary>
/// Shared contract for monster attacks. Movement planning evaluates attacks through
/// this contract without depending on a concrete attack implementation.
/// </summary>
public abstract class MonsterAttackActionBase : MonsterActionBase
{
    public override MonsterActionCategory Category => MonsterActionCategory.Attack;
    public override MonsterActionExclusionGroup ExclusionGroup =>
        MonsterActionExclusionGroup.Attack;

    public abstract int PreferredMaxRange { get; }

    public abstract bool TryEvaluateAttackFrom(
        CellManager originCell,
        MonsterTarget target,
        bool playerVisible,
        out int attackDistance);
}
