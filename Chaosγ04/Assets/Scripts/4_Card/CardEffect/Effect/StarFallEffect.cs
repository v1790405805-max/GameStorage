using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Card-specific effect for 1005 "Star Fall".
///
/// It compares the layer of the player's cell with the layer of the target monster's cell:
/// - Player layer not above the target layer: the card is a plain single-target attack. No push
///   and no cross damage - the card's regular attack damage is settled by CardManager.
/// - Player layer above the target layer: PushEffect runs, and the card also deals cross-shaped
///   damage - every monster on the Manhattan cross of radius 2 centred on the target monster's
///   cell takes 15 damage. The target itself already takes the card's regular attack damage, so
///   it only receives the remainder (15 - 9 = 6) from the cross, for 15 in total. When the normal
///   push is blocked, the monster is force pushed instead: the landing cell behind the target if
///   it is only one layer higher, otherwise the left cell, then the right cell (a forced push may
///   also raise the monster by one layer). After a successful push the player immediately moves
///   into the cell the monster used to occupy.
///
/// PushEffect keeps the generic push rules, including treating a landing cell two or more
/// layers higher as a wall. This script therefore drives PushEffect directly instead of
/// listing it as a separate extra effect, so the push never runs twice.
/// </summary>
public class StarFallEffect : CardEffectCore, ICardDragPreviewProvider
{
    private const string PlayerTag = "Player";
    private const int CrossDamage = 15;
    private const int CrossRadius = 2;

    private struct StarFallContext
    {
        public MonsterIdentityManager monster;
        public CellManager targetCell;
        public Vector2Int targetGrid;
        public int targetLayer;
        public int playerLayer;
        public Vector2Int direction;
    }

    public override bool CanPlay(CardData card, Vector2Int targetGrid)
    {
        if (card == null || !card.effectFlags.HasFlag(CardEffectType.Attack))
        {
            return false;
        }

        GridManager gridManager = Object.FindFirstObjectByType<GridManager>();
        if (gridManager == null)
        {
            return false;
        }

        // Hovering the player's own cell has no monster; that is not a real cast, so it must
        // not turn the card red.
        if (!TryGetContext(gridManager, targetGrid, out StarFallContext context))
        {
            return true;
        }

        // Without a height advantage the card is a plain single-target attack: no push and no
        // cross damage, but it is still usable.
        if (context.playerLayer <= context.targetLayer)
        {
            return true;
        }

        return HasForcedPushDestination(gridManager, context);
    }

    /// <summary>
    /// 拖拽预览：高度优势分支下高亮十字范围、把 Player_WhenMove 显示在玩家将到达的落点，
    /// 并假设性移动将被推动的怪物。无任何副作用（推击走 dry run）。
    /// </summary>
    public bool TryGetDragPreview(
        GridManager gridManager,
        CardData card,
        Vector2Int targetGrid,
        out CardDragPreview preview)
    {
        preview = default;

        if (!TryGetPreview(gridManager, card, targetGrid, out StarFallPreview starFall))
        {
            return false;
        }

        preview = new CardDragPreview
        {
            highlightCells = starFall.rangeCells,
            playerPreviewCell = starFall.playerDestinationCell,
            monsterToMove = starFall.monster,
            monsterDestinationCell = starFall.monsterDestinationCell
        };
        return true;
    }

    /// <summary>Drag-preview information for the height-advantage branch.</summary>
    public struct StarFallPreview
    {
        public MonsterIdentityManager monster;
        public CellManager monsterOriginCell;
        public CellManager monsterDestinationCell;
        public CellManager playerDestinationCell;
        public List<CellManager> rangeCells;
    }

    /// <summary>
    /// Drag preview: when the hovered target is a valid height-advantage target, returns the cross
    /// range to highlight and where the monster / player would end up. Produces no side effects
    /// (the push runs as a dry run).
    /// </summary>
    public static bool TryGetPreview(
        GridManager gridManager,
        CardData card,
        Vector2Int targetGrid,
        out StarFallPreview preview)
    {
        preview = default;

        if (gridManager == null || card == null)
        {
            return false;
        }

        if (!TryGetContext(gridManager, targetGrid, out StarFallContext context))
        {
            return false;
        }

        // The cross range and the forced push only exist with a height advantage.
        if (context.playerLayer <= context.targetLayer)
        {
            return false;
        }

        PushEffect push = ScriptableObject.CreateInstance<PushEffect>();
        PushEffect.PushResult result =
            push.ResolvePush(card, targetGrid, applyBlockerDamage: false, dryRun: true);
        Object.Destroy(push);

        if (!result.isValid)
        {
            return false;
        }

        TryResolveDestination(gridManager, context, result, out CellManager destination, out bool pushed);

        preview = new StarFallPreview
        {
            monster = context.monster,
            monsterOriginCell = context.targetCell,
            monsterDestinationCell = pushed ? destination : null,
            playerDestinationCell = pushed ? context.targetCell : null,
            rangeCells = CollectCrossCells(gridManager, targetGrid, context.targetLayer)
        };
        return true;
    }

    private static List<CellManager> CollectCrossCells(
        GridManager gridManager,
        Vector2Int centerGrid,
        int layer)
    {
        List<CellManager> cells = new List<CellManager>();
        foreach (Vector2Int grid in GetCrossGrids(centerGrid, CrossRadius))
        {
            CellManager cell = PushEffect.ResolveLandingCell(gridManager, grid, layer);
            if (cell != null)
            {
                cells.Add(cell);
            }
        }

        return cells;
    }

    public override bool Execute(CardData card, Vector2Int targetGrid)
    {
        if (card == null || !card.effectFlags.HasFlag(CardEffectType.Attack))
        {
            Debug.LogWarning("[StarFallEffect] The card is not configured as an attack.");
            return false;
        }

        GridManager gridManager = Object.FindFirstObjectByType<GridManager>();
        if (gridManager == null)
        {
            Debug.LogError("[StarFallEffect] GridManager was not found.");
            return false;
        }

        if (!TryGetContext(gridManager, targetGrid, out StarFallContext context))
        {
            Debug.Log($"[StarFallEffect] No enemy was found at target cell [{targetGrid}].");
            return false;
        }

        if (context.playerLayer <= context.targetLayer)
        {
            // No height advantage: no push and no cross damage. The card's regular attack
            // damage is settled by CardManager.
            Debug.Log($"[StarFallEffect] Player L{context.playerLayer} is not above target L{context.targetLayer}; regular attack only.");
            return true;
        }

        // Height advantage: push the monster, then damage the cross centred on its old cell.
        PushEffect push = ScriptableObject.CreateInstance<PushEffect>();
        PushEffect.PushResult result = push.ResolvePush(card, context.targetGrid, applyBlockerDamage: false);
        Object.Destroy(push);

        // Consume the shared result so a later card cannot read a stale push outcome.
        PushEffect.LastResult = default;

        if (!result.isValid)
        {
            Debug.LogWarning($"[StarFallEffect] PushEffect did not resolve a push at [{context.targetGrid}].");
            return false;
        }

        TryResolveDestination(gridManager, context, result, out CellManager destination, out bool pushed);
        if (pushed && destination != null)
        {
            context.monster.transform.position = destination.transform.position;
            MovePlayerToCell(gridManager, context.targetCell, context.targetGrid);
        }

        ApplyCrossDamage(gridManager, context.targetGrid, context.monster, card.damage);
        return true;
    }

    /// <summary>
    /// Turns a PushEffect result into the final landing cell of the height-advantage branch.
    /// Returns false only when the outcome cannot be interpreted. <paramref name="pushed"/> stays
    /// false when the monster dies or cannot be relocated (the player then stays put).
    /// Shared by Execute and the drag preview, so both agree on where things end up.
    /// </summary>
    private static bool TryResolveDestination(
        GridManager gridManager,
        StarFallContext context,
        PushEffect.PushResult result,
        out CellManager destination,
        out bool pushed)
    {
        destination = null;
        pushed = false;

        switch (result.outcome)
        {
            case PushEffect.PushOutcome.Pushed:
                // The regular push already moved the monster to this cell.
                destination = PushEffect.ResolveLandingCell(
                    gridManager, result.pushedGrid, context.targetLayer);
                pushed = destination != null;
                return true;

            case PushEffect.PushOutcome.KilledTarget:
                // The target died from the push; the player does not advance.
                return true;

            case PushEffect.PushOutcome.BlockedByHigherLayer:
                // Only one layer higher: force the monster onto that cell.
                destination = PushEffect.ResolveLandingCell(
                    gridManager, context.targetGrid + context.direction, context.targetLayer);
                pushed = destination != null;
                return true;

            case PushEffect.PushOutcome.BlockedByWall:
            case PushEffect.PushOutcome.BlockedByMonster:
            case PushEffect.PushOutcome.BlockedByObstacle:
                // The cell behind is unusable: force the monster left, then right.
                pushed = TryResolveForced(
                        gridManager, context,
                        context.targetGrid + GetLeftDirection(context.direction), out destination)
                    || TryResolveForced(
                        gridManager, context,
                        context.targetGrid + GetRightDirection(context.direction), out destination);
                return true;

            default:
                return false;
        }
    }

    private static bool TryResolveForced(
        GridManager gridManager,
        StarFallContext context,
        Vector2Int grid,
        out CellManager destination)
    {
        CellManager cell = PushEffect.ResolveLandingCell(gridManager, grid, context.targetLayer);
        if (IsUsableForcedLanding(gridManager, cell, context.monster, context.targetLayer))
        {
            destination = cell;
            return true;
        }

        destination = null;
        return false;
    }

    private static bool IsUsableForcedLanding(
        GridManager gridManager,
        CellManager cell,
        MonsterIdentityManager monster,
        int targetLayer)
    {
        if (cell == null || cell.IsHardLocked || cell.IsWall)
        {
            return false;
        }

        // Two or more layers higher counts as a wall (mirrors PushEffect).
        if (gridManager.GetCellLayer(cell) - targetLayer >= 2)
        {
            return false;
        }

        if (cell.IsWater && (monster == null || !monster.CanTraverseWater))
        {
            return false;
        }

        if (cell.IsPlayerInside)
        {
            return false;
        }

        return PushEffect.FindOtherMonsterInside(cell, monster) == null;
    }

    private static bool HasForcedPushDestination(GridManager gridManager, StarFallContext context)
    {
        CellManager behind = PushEffect.ResolveLandingCell(
            gridManager, context.targetGrid + context.direction, context.targetLayer);

        // A missing / hard-locked / non-traversable water cell behind kills the target, which
        // is still a valid use of the card.
        if (behind == null || behind.IsHardLocked)
        {
            return true;
        }

        if (behind.IsWater && (context.monster == null || !context.monster.CanTraverseWater))
        {
            return true;
        }

        if (IsUsableForcedLanding(gridManager, behind, context.monster, context.targetLayer))
        {
            return true;
        }

        CellManager left = PushEffect.ResolveLandingCell(
            gridManager, context.targetGrid + GetLeftDirection(context.direction), context.targetLayer);
        if (IsUsableForcedLanding(gridManager, left, context.monster, context.targetLayer))
        {
            return true;
        }

        CellManager right = PushEffect.ResolveLandingCell(
            gridManager, context.targetGrid + GetRightDirection(context.direction), context.targetLayer);
        return IsUsableForcedLanding(gridManager, right, context.monster, context.targetLayer);
    }

    /// <summary>
    /// Damages every monster on the Manhattan cross of radius <see cref="CrossRadius"/> centred on
    /// <paramref name="centerGrid"/>. The target monster already takes the card's regular attack
    /// damage, so it only receives the remainder up to <see cref="CrossDamage"/> (9 + 6 = 15);
    /// every other monster inside the cross takes the full 15.
    /// </summary>
    private static void ApplyCrossDamage(
        GridManager gridManager,
        Vector2Int centerGrid,
        MonsterIdentityManager target,
        int baseAttackDamage)
    {
        HashSet<Vector2Int> crossGrids = GetCrossGrids(centerGrid, CrossRadius);
        int targetDamage = Mathf.Max(0, CrossDamage - baseAttackDamage);

        // Damage lands on the attack animation's hit frame, like the other attack effects.
        PlayerCombatReceiver.ExecuteOnHit(() =>
        {
            if (MonsterIdentitySystem.Instance == null)
            {
                return;
            }

            List<MonsterIdentityManager> monsters =
                new List<MonsterIdentityManager>(MonsterIdentitySystem.Instance.GetAllMonsters());

            foreach (MonsterIdentityManager monster in monsters)
            {
                if (monster == null || !monster.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var (mx, mz) = gridManager.GetGridPosition(monster.transform.position);
                if (!crossGrids.Contains(new Vector2Int(mx, mz)))
                {
                    continue;
                }

                int damage = monster == target ? targetDamage : CrossDamage;
                if (damage <= 0)
                {
                    continue;
                }

                MonsterStats stats = monster.GetComponent<MonsterStats>();
                stats?.TakeDamage(damage);
            }
        });
    }

    /// <summary>
    /// Grid cells of the Manhattan cross of the given radius around the centre, including the
    /// centre cell (the target may still stand there when the push does not move it).
    /// </summary>
    private static HashSet<Vector2Int> GetCrossGrids(Vector2Int center, int radius)
    {
        HashSet<Vector2Int> cells = new HashSet<Vector2Int> { center };
        for (int step = 1; step <= radius; step++)
        {
            cells.Add(center + new Vector2Int(step, 0));
            cells.Add(center + new Vector2Int(-step, 0));
            cells.Add(center + new Vector2Int(0, step));
            cells.Add(center + new Vector2Int(0, -step));
        }

        return cells;
    }

    private static Vector2Int GetLeftDirection(Vector2Int forward)
    {
        return new Vector2Int(-forward.y, forward.x);
    }

    private static Vector2Int GetRightDirection(Vector2Int forward)
    {
        return new Vector2Int(forward.y, -forward.x);
    }

    private static bool TryGetContext(
        GridManager gridManager,
        Vector2Int targetGrid,
        out StarFallContext context)
    {
        context = default;

        MonsterIdentityManager monster =
            PushEffect.FindMonsterAtGrid(gridManager, targetGrid, includeInactive: true);
        if (monster == null)
        {
            return false;
        }

        CellManager targetCell = PushEffect.FindCellContainingMonster(gridManager, monster, targetGrid);
        if (targetCell == null)
        {
            return false;
        }

        // Read the player's live grid / layer rather than CardManager.playStartGrid, which is
        // stale while hovering a card during target selection.
        if (!TryGetPlayerGridAndLayer(gridManager, out Vector2Int startGrid, out int playerLayer))
        {
            return false;
        }

        Vector2Int direction = PushEffect.GetCardinalDirection(startGrid, targetGrid);
        if (direction == Vector2Int.zero)
        {
            return false;
        }

        context = new StarFallContext
        {
            monster = monster,
            targetCell = targetCell,
            targetGrid = targetGrid,
            targetLayer = gridManager.GetCellLayer(targetCell),
            playerLayer = playerLayer,
            direction = direction
        };
        return true;
    }

    private static bool TryGetPlayerGridAndLayer(
        GridManager gridManager,
        out Vector2Int grid,
        out int layer)
    {
        grid = new Vector2Int(-1, -1);
        layer = -1;

        GameObject player = GameObject.FindGameObjectWithTag(PlayerTag);
        if (player == null)
        {
            return false;
        }

        gridManager.EnsureGridSystemInitialized();
        Vector3 logicalPosition = player.transform.position - gridManager.cellOffset;
        var (x, z) = gridManager.GetGridPosition(logicalPosition);
        grid = new Vector2Int(x, z);

        CellManager playerCell = null;
        float nearestDistance = float.MaxValue;
        foreach (CellManager cell in gridManager.GetCellManagersInColumn(x, z))
        {
            if (cell == null)
            {
                continue;
            }

            float distance = Mathf.Abs(cell.transform.position.y - player.transform.position.y);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                playerCell = cell;
            }
        }

        if (playerCell == null)
        {
            return false;
        }

        layer = gridManager.GetCellLayer(playerCell);
        return layer >= 0;
    }

    private static void MovePlayerToCell(
        GridManager gridManager,
        CellManager targetCell,
        Vector2Int targetGrid)
    {
        GameObject player = GameObject.FindGameObjectWithTag(PlayerTag);
        if (player == null || targetCell == null)
        {
            return;
        }

        Transform playerRoot = player.transform.root;
        Vector3 targetPosition = targetCell.transform.position;
        targetPosition.x -= gridManager.cellOffset.x;
        targetPosition.z -= gridManager.cellOffset.z;
        playerRoot.position = targetPosition;

        PlayerMoveController moveController = Object.FindFirstObjectByType<PlayerMoveController>();
        if (moveController != null)
        {
            moveController.SyncGridPosition(targetGrid);
        }
    }
}
