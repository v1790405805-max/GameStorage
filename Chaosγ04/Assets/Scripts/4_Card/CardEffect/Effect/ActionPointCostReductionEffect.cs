using UnityEngine;

/// <summary>
/// Passive card effect: every 5 action points spent this turn reduces the
/// card's effective Cost by 1. The cost is resolved before the card is played.
/// </summary>
public class ActionPointCostReductionEffect : CardEffectCore
{
    public const int ActionPointCostInterval = 5;

    public override int GetCostModifier(CardData card)
    {
        if (CombatStatsManager.Instance == null)
        {
            return 0;
        }

        int costReduction =
            CombatStatsManager.Instance.UsedActionPointCount / ActionPointCostInterval;

        return -Mathf.Max(0, costReduction);
    }

    public override bool Execute(CardData card, Vector2Int targetGrid)
    {
        // This is a passive cost modifier resolved by CardData.GetEffectiveCost().
        return true;
    }
}
