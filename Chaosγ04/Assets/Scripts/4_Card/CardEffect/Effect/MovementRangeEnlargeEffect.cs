using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Increases range distance for movement-flagged cards during the current round.
/// </summary>
public class MovementRangeEnlargeEffect : CardEffectCore
{
    private static int activeStackCount;
    private static int activationRound = int.MinValue;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    public static int GetRangeDistanceBonus(CardData card)
    {
        if (card == null || !card.effectFlags.HasFlag(CardEffectType.Movement))
        {
            return 0;
        }

        return IsActiveForCurrentRound() ? activeStackCount : 0;
    }

    public override bool Execute(CardData card, Vector2Int targetGrid)
    {
        int currentRound = GetCurrentRound();
        if (activationRound != currentRound)
        {
            ResetRuntimeState();
        }

        activeStackCount++;
        activationRound = currentRound;
        return true;
    }

    private static bool IsActiveForCurrentRound()
    {
        if (activeStackCount <= 0)
        {
            return false;
        }

        if (activationRound == GetCurrentRound())
        {
            return true;
        }

        ResetRuntimeState();
        return false;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        bool isBattleScene =
            scene.name.IndexOf("Battle", StringComparison.OrdinalIgnoreCase) >= 0;
        CombatStatsManager stats = CombatStatsManager.Instance;
        if (isBattleScene && stats != null && !stats.hasSavedGame)
        {
            ResetRuntimeState();
        }
    }

    private static int GetCurrentRound()
    {
        return TurnManager.Instance != null
            ? TurnManager.Instance.currentRoundCount
            : -1;
    }

    private static void ResetRuntimeState()
    {
        activeStackCount = 0;
        activationRound = int.MinValue;
    }
}
