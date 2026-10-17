using System.Collections;
using UnityEngine;

/// <summary>
/// 3004 架势反击：敌人攻击未能击穿玩家护甲，且敌人位于玩家一格范围内时，
/// 玩家转向攻击者并造成反击伤害。
/// </summary>
public sealed class DefenseAbility : AbilityCore
{
    private const int CounterDamage = 10;
    private const int CounterRange = 1;
    private const float AttackImpactDelay = 0.2f;
    private const string PlayerTag = "Player";

    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int HorizontalHash = Animator.StringToHash("Horizontal");
    private static readonly int VerticalHash = Animator.StringToHash("Vertical");

    private PlayerOrientationDamageController damageController;
    private GridManager gridManager;
    private PlayerMoveController playerMoveController;

    protected override void OnActivated()
    {
        damageController = PlayerOrientationDamageController.Instance;
        if (damageController == null)
        {
            damageController = Object.FindFirstObjectByType<PlayerOrientationDamageController>();
        }

        if (damageController != null)
        {
            damageController.AttackFullyBlocked += HandleAttackFullyBlocked;
        }
    }

    protected override void OnDeactivated()
    {
        if (damageController != null)
        {
            damageController.AttackFullyBlocked -= HandleAttackFullyBlocked;
            damageController = null;
        }

        gridManager = null;
        playerMoveController = null;
    }

    private void HandleAttackFullyBlocked(
        MonsterIdentityManager attacker,
        Vector2Int attackerGrid)
    {
        if (!IsActive || attacker == null || !attacker.gameObject.activeInHierarchy)
        {
            return;
        }

        if (!IsWithinCounterRange(attackerGrid))
        {
            return;
        }

        MonoBehaviour runner = PlayerCombatReceiver.Instance;
        if (runner == null)
        {
            runner = CombatStatsManager.Instance;
        }

        if (runner == null)
        {
            ResolveCounterAttack(attacker);
            return;
        }

        runner.StartCoroutine(CounterAttackRoutine(attacker, attackerGrid));
    }

    private bool IsWithinCounterRange(Vector2Int attackerGrid)
    {
        if (!TryEnsurePlayerReferences())
        {
            return false;
        }

        Vector2Int playerGrid = playerMoveController.PlayerGridPos;
        if (!gridManager.IsValidGridPosition(playerGrid.x, playerGrid.y))
        {
            return false;
        }

        int distance =
            Mathf.Abs(attackerGrid.x - playerGrid.x) +
            Mathf.Abs(attackerGrid.y - playerGrid.y);

        return distance > 0 && distance <= CounterRange;
    }

    private IEnumerator CounterAttackRoutine(
        MonsterIdentityManager attacker,
        Vector2Int attackerGrid)
    {
        Animator playerAnimator = GetPlayerAnimator();
        FaceAttacker(playerAnimator, attackerGrid);
        PlayAttackAnimation(playerAnimator);

        if (AttackImpactDelay > 0f)
        {
            yield return new WaitForSeconds(AttackImpactDelay);
        }

        if (!IsActive)
        {
            yield break;
        }

        ResolveCounterAttack(attacker);
    }

    private void ResolveCounterAttack(MonsterIdentityManager attacker)
    {
        if (!IsActive || attacker == null || !attacker.gameObject.activeInHierarchy)
        {
            return;
        }

        MonsterStats targetStats = attacker.GetComponent<MonsterStats>();
        if (targetStats == null)
        {
            targetStats = attacker.GetComponentInChildren<MonsterStats>();
        }

        if (targetStats == null)
        {
            Debug.LogWarning($"[DefenseAbility] 攻击者 [{attacker.name}] 缺少 MonsterStats 组件。");
            return;
        }

        targetStats.TakeDamage(CounterDamage);
        Debug.Log($"[DefenseAbility] 对 [{attacker.name}] 造成 {CounterDamage} 点反击伤害。");
    }

    private void FaceAttacker(Animator playerAnimator, Vector2Int attackerGrid)
    {
        if (playerAnimator == null || !TryEnsurePlayerReferences())
        {
            return;
        }

        Vector2Int playerGrid = playerMoveController.PlayerGridPos;
        int deltaX = attackerGrid.x - playerGrid.x;
        int deltaY = attackerGrid.y - playerGrid.y;

        if (deltaX < 0)
        {
            playerAnimator.SetFloat(HorizontalHash, -1f);
            playerAnimator.SetFloat(VerticalHash, -1f);
        }
        else if (deltaX > 0)
        {
            playerAnimator.SetFloat(HorizontalHash, 1f);
            playerAnimator.SetFloat(VerticalHash, 1f);
        }
        else if (deltaY < 0)
        {
            playerAnimator.SetFloat(HorizontalHash, -1f);
            playerAnimator.SetFloat(VerticalHash, 1f);
        }
        else if (deltaY > 0)
        {
            playerAnimator.SetFloat(HorizontalHash, 1f);
            playerAnimator.SetFloat(VerticalHash, -1f);
        }
    }

    private static void PlayAttackAnimation(Animator playerAnimator)
    {
        if (playerAnimator == null)
        {
            Debug.LogWarning("[DefenseAbility] 找不到 Player Animator，无法播放反击动画。");
            return;
        }

        playerAnimator.ResetTrigger(AttackHash);
        playerAnimator.SetTrigger(AttackHash);
    }

    private bool TryEnsurePlayerReferences()
    {
        if (gridManager == null)
        {
            gridManager = Object.FindFirstObjectByType<GridManager>();
        }

        if (playerMoveController == null)
        {
            playerMoveController = Object.FindFirstObjectByType<PlayerMoveController>();
        }

        return gridManager != null && playerMoveController != null;
    }

    private static Animator GetPlayerAnimator()
    {
        GameObject player = GameObject.FindGameObjectWithTag(PlayerTag);
        return player != null ? player.GetComponentInChildren<Animator>() : null;
    }
}
