using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Makes monsters lose the player as a movement target during the current round.
/// </summary>
public class TargetLossEffect : CardEffectCore
{
    private static bool active;
    private static int activationRound = int.MinValue;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    public static bool MonstersLosePlayerTargetThisRound
    {
        get { return IsActiveForCurrentRound(); }
    }

    public override bool Execute(CardData card, Vector2Int targetGrid)
    {
        active = true;
        activationRound = GetCurrentRound();
        return true;
    }

    private static bool IsActiveForCurrentRound()
    {
        if (!active)
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
        active = false;
        activationRound = int.MinValue;
    }
}
