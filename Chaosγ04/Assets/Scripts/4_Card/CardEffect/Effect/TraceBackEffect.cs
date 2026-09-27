using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resolves the player's movement path backward by up to seven cells.
/// If that destination is not occupied by an enemy, the player teleports there
/// and deals damage to the four orthogonal neighboring cells.
/// </summary>
public class TraceBackEffect : CardEffectCore
{
    private const string PlayerTag = "Player";
    private const int BackwardCellCount = 7;
    private const int CrossDamage = 9;

    private static readonly Vector2Int[] CrossDirections =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    public override bool CanPlay(CardData card, Vector2Int targetGrid)
    {
        return TryResolveDestination(
                   out _,
                   out _,
                   out _,
                   out CellManager destinationCell,
                   out _)
               && !destinationCell.HasMonsterInside;
    }

    public override bool Execute(CardData card, Vector2Int targetGrid)
    {
        if (!TryResolveDestination(
                out GridManager gridManager,
                out GameObject player,
                out PlayerMoveController moveController,
                out CellManager destinationCell,
                out Vector2Int destinationGrid))
        {
            return false;
        }

        if (destinationCell.HasMonsterInside)
        {
            Debug.Log($"[TraceBackEffect] Destination [{destinationGrid}] is blocked by an enemy.");
            return false;
        }

        MovePlayerToCell(gridManager, player, moveController, destinationCell, destinationGrid);
        int hitCount = ApplyCrossDamage(gridManager, destinationGrid);

        Debug.Log($"[TraceBackEffect] Player teleported to [{destinationGrid}] and the cross attack hit {hitCount} enemies for {CrossDamage} damage.");
        return true;
    }

    private static bool TryResolveDestination(
        out GridManager gridManager,
        out GameObject player,
        out PlayerMoveController moveController,
        out CellManager destinationCell,
        out Vector2Int destinationGrid)
    {
        gridManager = Object.FindFirstObjectByType<GridManager>();
        player = null;
        moveController = null;
        destinationCell = null;
        destinationGrid = new Vector2Int(-1, -1);

        if (gridManager == null)
        {
            Debug.LogError("[TraceBackEffect] GridManager was not found.");
            return false;
        }

        player = GameObject.FindGameObjectWithTag(PlayerTag);
        if (player == null)
        {
            Debug.LogError($"[TraceBackEffect] No object with tag [{PlayerTag}] was found.");
            return false;
        }

        moveController = Object.FindFirstObjectByType<PlayerMoveController>();
        if (moveController == null)
        {
            Debug.LogWarning("[TraceBackEffect] PlayerMoveController was not found.");
            return false;
        }

        if (!PlayerMovementRecord.TryGetDestination(
                gridManager,
                player,
                BackwardCellCount,
                out destinationCell,
                out destinationGrid))
        {
            Debug.LogWarning("[TraceBackEffect] The player has no valid movement trace for this turn.");
            return false;
        }

        return true;
    }

    private static void MovePlayerToCell(
        GridManager gridManager,
        GameObject player,
        PlayerMoveController moveController,
        CellManager destinationCell,
        Vector2Int destinationGrid)
    {
        Transform playerRoot = player.transform.root;
        Quaternion originalRotation = playerRoot.rotation;
        Animator animator = player.GetComponentInChildren<Animator>();
        float originalHorizontal = animator != null ? animator.GetFloat("Horizontal") : 0f;
        float originalVertical = animator != null ? animator.GetFloat("Vertical") : 0f;

        Vector3 destinationPosition = destinationCell.transform.position;
        destinationPosition.x -= gridManager.cellOffset.x;
        destinationPosition.z -= gridManager.cellOffset.z;
        playerRoot.position = destinationPosition;
        playerRoot.rotation = originalRotation;

        if (animator != null)
        {
            animator.SetFloat("Horizontal", originalHorizontal);
            animator.SetFloat("Vertical", originalVertical);
        }

        moveController.SyncGridPosition(destinationGrid);
    }

    private static int ApplyCrossDamage(GridManager gridManager, Vector2Int centerGrid)
    {
        if (MonsterIdentitySystem.Instance == null)
        {
            return 0;
        }

        HashSet<Vector2Int> damageGrids = new HashSet<Vector2Int>();
        foreach (Vector2Int direction in CrossDirections)
        {
            Vector2Int damageGrid = centerGrid + direction;
            if (gridManager.IsValidGridPosition(damageGrid.x, damageGrid.y))
            {
                damageGrids.Add(damageGrid);
            }
        }

        int hitCount = 0;
        List<MonsterIdentityManager> monsters =
            new List<MonsterIdentityManager>(MonsterIdentitySystem.Instance.GetAllMonsters());
        foreach (MonsterIdentityManager monster in monsters)
        {
            if (monster == null || !monster.gameObject.activeInHierarchy)
            {
                continue;
            }

            var (x, z) = gridManager.GetGridPosition(monster.transform.position);
            if (!damageGrids.Contains(new Vector2Int(x, z)))
            {
                continue;
            }

            MonsterStats stats = monster.GetComponent<MonsterStats>();
            if (stats == null)
            {
                continue;
            }

            stats.TakeDamage(CrossDamage);
            hitCount++;
        }

        return hitCount;
    }
}
