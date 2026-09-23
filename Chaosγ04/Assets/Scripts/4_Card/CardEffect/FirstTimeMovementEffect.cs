using UnityEngine;

/// <summary>
/// Passive card effect: if this is the first card played this turn,
/// increase the card's movement distance by 1.
/// </summary>
public class FirstTimeMovementEffect : CardEffectCore
{
    private const int MoveDistanceBonus = 1;

    public override int GetMoveDistanceModifier(CardData card)
    {
        if (card == null || !card.effectFlags.HasFlag(CardEffectType.Movement))
        {
            return 0;
        }

        if (CardManager.Instance == null || CardManager.Instance.HasPlayedCardThisTurn)
        {
            return 0;
        }

        return MoveDistanceBonus;
    }

    public override bool Execute(CardData card, Vector2Int targetGrid)
    {
        // This is a passive movement-range modifier resolved before drag validation.
        return true;
    }
}
