using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tracks the player's actual cell-to-cell movement during the current turn.
/// The state is event-driven and survives scene changes for save-and-continue.
/// </summary>
public static class PlayerMovementRecord
{
    private static MovementTraceTracker trackerInstance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (trackerInstance != null)
        {
            return;
        }

        GameObject trackerObject = new GameObject("[PlayerMovementRecord]");
        UnityEngine.Object.DontDestroyOnLoad(trackerObject);
        trackerInstance = trackerObject.AddComponent<MovementTraceTracker>();
    }

    public static List<(Vector2Int grid, int layer)> GetCurrentTurnMovementTrace()
    {
        return trackerInstance != null
            ? trackerInstance.GetMovementTraceSnapshot()
            : new List<(Vector2Int grid, int layer)>();
    }

    public static bool TryGetDestination(
        GridManager gridManager,
        GameObject player,
        int maxCellsBack,
        out CellManager destinationCell,
        out Vector2Int destinationGrid)
    {
        destinationCell = null;
        destinationGrid = new Vector2Int(-1, -1);

        return trackerInstance != null &&
               trackerInstance.TryGetDestination(
                   gridManager,
                   player,
                   maxCellsBack,
                   out destinationCell,
                   out destinationGrid);
    }

    [Serializable]
    private struct MovementTraceCell
    {
        public int x;
        public int z;
        public int layer;
    }

    private sealed class MovementTraceTracker : MonoBehaviour
    {
        private readonly List<MovementTraceCell> movementHistory =
            new List<MovementTraceCell>();

        private int trackedRound = int.MinValue;
        private MovementTraceCell turnStartCell;
        private MovementTraceCell currentCell;
        private bool hasTurnStartCell;
        private bool hasCurrentCell;

        private void Awake()
        {
            if (trackerInstance != null && trackerInstance != this)
            {
                Destroy(gameObject);
                return;
            }

            trackerInstance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SubscribeToCellEvents();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnsubscribeFromCellEvents();

            if (trackerInstance == this)
            {
                trackerInstance = null;
            }
        }

        public List<(Vector2Int grid, int layer)> GetMovementTraceSnapshot()
        {
            List<(Vector2Int grid, int layer)> result =
                new List<(Vector2Int grid, int layer)>(movementHistory.Count);
            foreach (MovementTraceCell cell in movementHistory)
            {
                result.Add((new Vector2Int(cell.x, cell.z), cell.layer));
            }

            return result;
        }

        public bool TryGetDestination(
            GridManager gridManager,
            GameObject player,
            int maxCellsBack,
            out CellManager destinationCell,
            out Vector2Int destinationGrid)
        {
            destinationCell = null;
            destinationGrid = new Vector2Int(-1, -1);

            int currentRound = GetCurrentRound();
            if (movementHistory.Count == 0 || trackedRound != currentRound)
            {
                ResetHistory();
                return false;
            }

            if (!TryGetCurrentPlayerCell(gridManager, player, out MovementTraceCell currentCell) ||
                !SameCell(movementHistory[movementHistory.Count - 1], currentCell))
            {
                ResetHistory();
                return false;
            }

            int availableCellsToMoveBack = movementHistory.Count - 1;
            MovementTraceCell savedDestination;
            if (availableCellsToMoveBack <= maxCellsBack && hasTurnStartCell)
            {
                savedDestination = turnStartCell;
            }
            else
            {
                int cellsToMoveBack = Mathf.Min(maxCellsBack, availableCellsToMoveBack);
                savedDestination =
                    movementHistory[movementHistory.Count - 1 - cellsToMoveBack];
            }

            destinationCell = ResolveCell(gridManager, savedDestination);
            if (destinationCell == null)
            {
                ResetHistory();
                return false;
            }

            destinationGrid = new Vector2Int(savedDestination.x, savedDestination.z);
            return true;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            bool isBattleScene =
                scene.name.IndexOf("Battle", StringComparison.OrdinalIgnoreCase) >= 0;
            CombatStatsManager stats = CombatStatsManager.Instance;
            if (isBattleScene && stats != null && !stats.hasSavedGame)
            {
                ResetHistory();
            }

            CaptureCurrentPlayerCell();
        }

        private void SubscribeToCellEvents()
        {
            CellManager.PlayerEntered -= HandlePlayerCellEntered;
            CellManager.PlayerEntered += HandlePlayerCellEntered;
        }

        private void UnsubscribeFromCellEvents()
        {
            CellManager.PlayerEntered -= HandlePlayerCellEntered;
        }

        private void HandlePlayerCellEntered(CellManager enteredCell)
        {
            if (!TryCreateTraceCell(enteredCell, out MovementTraceCell enteredTrace))
            {
                return;
            }

            int currentRound = GetCurrentRound();
            if (trackedRound != currentRound)
            {
                ResetHistory();
            }

            trackedRound = currentRound;

            PlayerMoveController moveController = FindFirstObjectByType<PlayerMoveController>();
            bool isMovement = moveController != null && moveController.IsMoving;
            if (!isMovement)
            {
                ResetHistory();
                trackedRound = currentRound;
                currentCell = enteredTrace;
                hasCurrentCell = true;
                return;
            }

            if (hasCurrentCell && movementHistory.Count == 0)
            {
                turnStartCell = currentCell;
                hasTurnStartCell = true;
                movementHistory.Add(currentCell);
            }

            AppendCell(enteredCell);
            currentCell = enteredTrace;
            hasCurrentCell = true;
        }

        private void CaptureCurrentPlayerCell()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            GridManager gridManager = FindFirstObjectByType<GridManager>();
            if (player == null || gridManager == null)
            {
                return;
            }

            if (TryGetCurrentPlayerCell(gridManager, player, out MovementTraceCell playerCell))
            {
                currentCell = playerCell;
                hasCurrentCell = true;
            }
        }

        private void AppendCell(CellManager cell)
        {
            if (!TryCreateTraceCell(cell, out MovementTraceCell traceCell))
            {
                return;
            }

            if (movementHistory.Count > 0 &&
                SameCell(movementHistory[movementHistory.Count - 1], traceCell))
            {
                return;
            }

            movementHistory.Add(traceCell);
        }

        private static bool TryCreateTraceCell(
            CellManager cell,
            out MovementTraceCell traceCell)
        {
            traceCell = default;
            if (cell == null)
            {
                return false;
            }

            GridManager gridManager = UnityEngine.Object.FindFirstObjectByType<GridManager>();
            if (gridManager == null)
            {
                return false;
            }

            var (x, z) = gridManager.GetCellGridPosition(cell);
            traceCell = new MovementTraceCell
            {
                x = x,
                z = z,
                layer = gridManager.GetCellLayer(cell)
            };
            return true;
        }

        private bool TryGetCurrentPlayerCell(
            GridManager gridManager,
            GameObject player,
            out MovementTraceCell traceCell)
        {
            traceCell = default;

            gridManager.EnsureGridSystemInitialized();
            Vector3 logicalPosition = player.transform.position - gridManager.cellOffset;
            var (x, z) = gridManager.GetGridPosition(logicalPosition);

            CellManager nearestCell = null;
            float nearestHeightDifference = float.MaxValue;
            foreach (CellManager cell in gridManager.GetCellManagersInColumn(x, z))
            {
                if (cell == null)
                {
                    continue;
                }

                float heightDifference =
                    Mathf.Abs(cell.transform.position.y - player.transform.position.y);
                if (heightDifference < nearestHeightDifference)
                {
                    nearestCell = cell;
                    nearestHeightDifference = heightDifference;
                }
            }

            if (nearestCell == null)
            {
                return false;
            }

            traceCell = new MovementTraceCell
            {
                x = x,
                z = z,
                layer = gridManager.GetCellLayer(nearestCell)
            };
            return true;
        }

        private static CellManager ResolveCell(
            GridManager gridManager,
            MovementTraceCell savedCell)
        {
            CellManager cell = gridManager.GetCellManagerAt(
                savedCell.x,
                savedCell.z,
                savedCell.layer);
            if (cell != null)
            {
                return cell;
            }

            CellManager topCell = gridManager.GetCellManagerAt(savedCell.x, savedCell.z);
            return topCell != null && gridManager.GetCellLayer(topCell) == savedCell.layer
                ? topCell
                : null;
        }

        private static bool SameCell(MovementTraceCell a, MovementTraceCell b)
        {
            return a.x == b.x && a.z == b.z && a.layer == b.layer;
        }

        private static int GetCurrentRound()
        {
            return TurnManager.Instance != null
                ? TurnManager.Instance.currentRoundCount
                : -1;
        }

        private void ResetHistory()
        {
            movementHistory.Clear();
            turnStartCell = default;
            hasTurnStartCell = false;
            trackedRound = GetCurrentRound();
        }
    }
}
