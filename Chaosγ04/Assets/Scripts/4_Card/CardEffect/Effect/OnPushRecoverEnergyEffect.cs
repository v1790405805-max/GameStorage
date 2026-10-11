using UnityEngine;

/// <summary>
/// Follow-up effect for a push: settles the extra damage / energy recovery that
/// depends on the push outcome.
///
/// This script must be mounted on the same card as PushEffect and listed after it
/// in CardData.extraEffects, otherwise it cannot read the push result and is skipped
/// with a warning.
///
/// Rules:
/// - The landing cell was a wall or pushedLayer greater than targetLayer: recover 1 energy.
/// - Every other outcome (pushed normally, target killed, landing cell blocked by a
///   monster or another object) does nothing here. The damage dealt when a monster
///   blocks the push is settled inside PushEffect.
/// </summary>
public class OnPushRecoverEnergyEffect : CardEffectCore
{
    private const int EnergyRecoverAmount = 1;

    public override bool Execute(CardData card, Vector2Int targetGrid)
    {
        PushEffect.PushResult result = PushEffect.LastResult;
        if (!result.isValid)
        {
            Debug.LogWarning(
                "[OnPushRecoverEnergyEffect] No push result was found; make sure PushEffect is listed before this effect in the card's extraEffects.");
            return false;
        }

        // Consume the result so a later card cannot read a stale push outcome.
        PushEffect.LastResult = default;

        switch (result.outcome)
        {
            case PushEffect.PushOutcome.BlockedByWall:
            case PushEffect.PushOutcome.BlockedByHigherLayer:
                RecoverEnergy();
                Debug.Log(
                    $"[OnPushRecoverEnergyEffect] The push was blocked ({result.outcome}); {EnergyRecoverAmount} energy was recovered.");
                break;

            case PushEffect.PushOutcome.None:
            case PushEffect.PushOutcome.Pushed:
            case PushEffect.PushOutcome.KilledTarget:
            case PushEffect.PushOutcome.BlockedByMonster:
            case PushEffect.PushOutcome.BlockedByObstacle:
            default:
                break;
        }

        return true;
    }

    private static void RecoverEnergy()
    {
        if (CombatStatsManager.Instance == null)
        {
            Debug.LogWarning("[OnPushRecoverEnergyEffect] CombatStatsManager was not found; energy was not recovered.");
            return;
        }

        CombatStatsManager.Instance.ModifyEnergy(EnergyRecoverAmount, allowExceedMax: true);
    }
}
