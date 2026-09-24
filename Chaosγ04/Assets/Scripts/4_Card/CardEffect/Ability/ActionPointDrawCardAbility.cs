using UnityEngine;

/// <summary>
/// 3002 大步流星：
/// 每累计消耗 5 点行动点，抽 1 张牌。
/// </summary>
public class ActionPointDrawCardAbility : AbilityCore
{
    private const int ActionPointsPerDraw = 5;

    private CombatStatsManager combatStats;
    private int spentActionPoints;

    protected override void OnActivated()
    {
        combatStats = CombatStatsManager.Instance;
        if (combatStats == null)
        {
            return;
        }

        combatStats.OnActionPointSpent += HandleActionPointSpent;
    }

    protected override void OnDeactivated()
    {
        if (combatStats != null)
        {
            combatStats.OnActionPointSpent -= HandleActionPointSpent;
        }

        combatStats = null;
    }

    private void HandleActionPointSpent(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        spentActionPoints += amount;
        int cardsToDraw = spentActionPoints / ActionPointsPerDraw;
        if (cardsToDraw <= 0)
        {
            return;
        }

        spentActionPoints %= ActionPointsPerDraw;
        if (CardManager.Instance != null)
        {
            CardManager.Instance.DrawCards(cardsToDraw);
        }
    }
}
