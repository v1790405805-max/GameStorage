using System.Collections;
using UnityEngine;

/// <summary>
/// 当怪物的攻击未能使玩家 HP 发生变化（完全格挡）时，对怪物发起反击。
/// </summary>
[DisallowMultipleComponent]
public sealed class DefenseCounterAttack : MonoBehaviour
{
    [Header("反击设置")]
    [Tooltip("反击造成的伤害值。")]
    [SerializeField, Min(0)] private int counterDamage = 10;

    [Tooltip("应用反击伤害前的延迟时间（秒），用于播放攻击动画。")]
    [SerializeField, Min(0f)] private float attackImpactDelay = 0.2f;

    [Tooltip("可以对已被格挡的攻击者发起反击的最大曼哈顿网格距离。")]
    [SerializeField, Min(1)] private int counterRange = 1;

    [Tooltip("用于触发玩家攻击动画的 Animator Trigger 参数名称。")]
    [SerializeField] private string attackTriggerName = "Attack";

    [Header("组件引用")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private PlayerMoveController playerMoveController;
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private PlayerOrientationDamageController orientationDamageController;

    private PlayerOrientationDamageController subscribedDamageController;
    private int attackTriggerHash;

    private void Awake()
    {
        attackTriggerHash = Animator.StringToHash(attackTriggerName);
        ResolveReferences();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void ResolveReferences()
    {
        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();
        if (playerMoveController == null)
            playerMoveController = FindFirstObjectByType<PlayerMoveController>();
        if (playerAnimator == null)
            playerAnimator = GetComponentInChildren<Animator>();
        if (orientationDamageController == null)
            orientationDamageController = GetComponent<PlayerOrientationDamageController>();
    }

    private void Subscribe()
    {
        if (orientationDamageController == null)
            orientationDamageController = GetComponent<PlayerOrientationDamageController>();

        if (subscribedDamageController == orientationDamageController)
            return;

        Unsubscribe();

        if (orientationDamageController == null)
            return;

        orientationDamageController.AttackFullyBlocked += HandleAttackFullyBlocked;
        subscribedDamageController = orientationDamageController;
    }

    private void Unsubscribe()
    {
        if (subscribedDamageController == null)
            return;

        subscribedDamageController.AttackFullyBlocked -= HandleAttackFullyBlocked;
        subscribedDamageController = null;
    }

    private void HandleAttackFullyBlocked(MonsterIdentityManager attacker, Vector2Int attackerGrid)
    {
        if (attacker == null || !attacker.gameObject.activeInHierarchy)
            return;

        if (!IsWithinCounterRange(attackerGrid))
            return;

        StartCoroutine(CounterAttackRoutine(attacker, attackerGrid));
    }

    private bool IsWithinCounterRange(Vector2Int attackerGrid)
    {
        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();
        if (playerMoveController == null)
            playerMoveController = FindFirstObjectByType<PlayerMoveController>();

        if (gridManager == null || playerMoveController == null)
            return false;

        Vector2Int playerGrid = playerMoveController.PlayerGridPos;
        if (!gridManager.IsValidGridPosition(playerGrid.x, playerGrid.y))
            return false;

        int distance =
            Mathf.Abs(attackerGrid.x - playerGrid.x) +
            Mathf.Abs(attackerGrid.y - playerGrid.y);

        return distance > 0 && distance <= counterRange;
    }

    private IEnumerator CounterAttackRoutine(MonsterIdentityManager attacker, Vector2Int attackerGrid)
    {
        FaceAttacker(attackerGrid);
        PlayAttackAnimation();

        if (attackImpactDelay > 0f)
            yield return new WaitForSeconds(attackImpactDelay);

        if (attacker == null || !attacker.gameObject.activeInHierarchy)
            yield break;

        MonsterStats targetStats = attacker.GetComponent<MonsterStats>();
        if (targetStats == null)
            targetStats = attacker.GetComponentInChildren<MonsterStats>();

        if (targetStats == null)
        {
            Debug.LogWarning($"[DefenseCounterAttack] 攻击者 [{attacker.name}] 缺少 MonsterStats 组件。");
            yield break;
        }

        targetStats.TakeDamage(counterDamage);
        Debug.Log($"[DefenseCounterAttack] 已对 [{attacker.name}] 发起反击，造成 {counterDamage} 点伤害。");
    }

    private void FaceAttacker(Vector2Int attackerGrid)
    {
        if (playerAnimator == null || playerMoveController == null)
            return;

        Vector2Int playerGrid = playerMoveController.PlayerGridPos;
        int deltaX = attackerGrid.x - playerGrid.x;
        int deltaY = attackerGrid.y - playerGrid.y;

        float horizontal = 0f;
        float vertical = 0f;

        if (deltaX < 0)
        {
            horizontal = -1f;
            vertical = -1f;
        }
        else if (deltaX > 0)
        {
            horizontal = 1f;
            vertical = 1f;
        }
        else if (deltaY < 0)
        {
            horizontal = -1f;
            vertical = 1f;
        }
        else if (deltaY > 0)
        {
            horizontal = 1f;
            vertical = -1f;
        }

        if (deltaX == 0 && deltaY == 0)
            return;

        playerAnimator.SetFloat("Horizontal", horizontal);
        playerAnimator.SetFloat("Vertical", vertical);
    }

    private void PlayAttackAnimation()
    {
        if (playerAnimator == null)
        {
            Debug.LogWarning("[DefenseCounterAttack] 找不到 Player Animator 组件，未播放反击动画。");
            return;
        }

        playerAnimator.ResetTrigger(attackTriggerHash);
        playerAnimator.SetTrigger(attackTriggerHash);
    }
}