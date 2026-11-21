using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Marks every cell traversed by the player during the current turn.
/// Until the next player turn starts, an enemy entering a marked cell
/// from another cell takes damage once per entry.
/// </summary>
public class MovementMarkDamageEffect : CardEffectCore
{
    private const int EntryDamage = 6;

    private static readonly Dictionary<GridCoord, int> markedCells =
        new Dictionary<GridCoord, int>();

    private static bool active;
    private static int activeStackCount;
    private static int activationRound = int.MinValue;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    public override bool Execute(CardData card, Vector2Int targetGrid)
    {
        Activate();
        return true;
    }

    private static void Activate()
    {
        int currentRound = GetCurrentRound();
        if (activationRound != currentRound)
        {
            ResetRuntimeState();
        }

        activeStackCount++;
        activationRound = currentRound;
        active = true;
        AddMovementTrace(PlayerMovementRecord.GetCurrentTurnMovementTrace());

        CellManager.PlayerEntered -= HandlePlayerCellEntered;
        CellManager.PlayerEntered += HandlePlayerCellEntered;
        CellManager.MonsterEntered -= HandleMonsterCellEntered;
        CellManager.MonsterEntered += HandleMonsterCellEntered;
    }

    private static void HandlePlayerCellEntered(CellManager enteredCell)
    {
        if (!active || !EnsureActiveForCurrentRound())
        {
            return;
        }

        PlayerMoveController moveController =
            UnityEngine.Object.FindFirstObjectByType<PlayerMoveController>();
        if (moveController == null || !moveController.IsMoving)
        {
            return;
        }

        AddCell(enteredCell);
    }

    private static void HandleMonsterCellEntered(
        CellManager enteredCell,
        MonsterIdentityManager monster)
    {
        if (!active ||
            !EnsureActiveForCurrentRound() ||
            enteredCell == null ||
            monster == null)
        {
            return;
        }

        GridCoord enteredKey = enteredCell.Coord;
        if (!markedCells.TryGetValue(enteredKey, out int markStacks) || markStacks <= 0)
        {
            return;
        }

        MonsterStats stats = monster.GetComponentInChildren<MonsterStats>();
        if (stats != null)
        {
            stats.TakeDamage(EntryDamage * markStacks);
        }
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

    private static void AddMovementTrace(List<(Vector2Int grid, int layer)> trace)
    {
        if (trace == null)
        {
            return;
        }

        foreach ((Vector2Int grid, int layer) in trace)
        {
            AddMark(new GridCoord(grid.x, grid.y, layer));
        }
    }

    private static void AddCell(CellManager cell)
    {
        GridCoord key = cell != null ? cell.Coord : GridCoord.Invalid;
        if (key.IsValid)
        {
            AddMark(key);
        }
    }

    private static void AddMark(GridCoord key)
    {
        if (!key.IsValid)
        {
            return;
        }

        markedCells.TryGetValue(key, out int existingStacks);
        if (activeStackCount > existingStacks)
        {
            markedCells[key] = activeStackCount;
        }
    }

    private static bool EnsureActiveForCurrentRound()
    {
        if (!active)
        {
            return false;
        }

        if (GetCurrentRound() == activationRound)
        {
            return true;
        }

        ResetRuntimeState();
        return false;
    }

    private static void ResetRuntimeState()
    {
        CellManager.PlayerEntered -= HandlePlayerCellEntered;
        CellManager.MonsterEntered -= HandleMonsterCellEntered;

        active = false;
        activeStackCount = 0;
        activationRound = int.MinValue;
        markedCells.Clear();
    }

    private static int GetCurrentRound()
    {
        return TurnManager.Instance != null
            ? TurnManager.Instance.currentRoundCount
            : -1;
    }

}
