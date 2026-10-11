using UnityEngine;

/// <summary>
/// Combo cost effect: when this card would be the third or later card played
/// this turn, its effective Cost is reduced to 0 before it is played.
/// </summary>
public class ComboCostReductionEffect : CardEffectCore
{
    public const int ComboCardThreshold = 3;

    public override int GetCostModifier(CardData card)
    {
        if (card == null || card.IsVariableCost || CardManager.Instance == null)
        {
            return 0;
        }

        bool isComboActive =
            CardManager.Instance.CardsPlayedThisTurn >= ComboCardThreshold - 1;

        return isComboActive ? -card.cost : 0;
    }

    public override bool Execute(CardData card, Vector2Int targetGrid)
    {
        // This is a passive cost modifier resolved by CardData.GetEffectiveCost().
        return true;
    }
}
