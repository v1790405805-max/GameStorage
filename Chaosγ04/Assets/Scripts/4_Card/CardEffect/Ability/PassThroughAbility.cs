using UnityEngine;

/// <summary>
/// 3003 星尘实体：
/// 本场战斗中，玩家移动时可以穿过敌人所在格，但不能停留在敌人格上。
/// </summary>
public class PassThroughAbility : AbilityCore
{
    public override bool CanTraverse(CellManager cell)
    {
        return cell != null && cell.HasMonsterInside;
    }

    public override bool CanLandOn(CellManager cell)
    {
        return cell == null || !cell.HasMonsterInside;
    }
}
