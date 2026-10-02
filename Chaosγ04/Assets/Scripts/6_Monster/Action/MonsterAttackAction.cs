using UnityEngine;

/// <summary>
/// Monster attack action. The attack target is selected by MonsterHateSystem and may be
/// the player or any other hostile-faction monster. Leave decisions suppress this action.
/// </summary>
public class MonsterAttackAction : MonsterActionBase
{
    [Header("网格管理器引用")]
    public GridManager gridManager;

    [Header("Animator引用")]
    public Animator monsterAnimator;

    [Header("动画参数配置")]
    public string attackTriggerName = "Attack";

    private MonsterIdentityManager selfIdentity;
    private MonsterHateSystem hateSystem;
    private MonsterStats monsterStats;
    private bool suppressedForTurn;

    public int AttackRange => monsterStats != null ? monsterStats.attackRange : 0;
    public int AttackDamage => monsterStats != null ? monsterStats.attackDamage : 0;

    private void Awake()
    {
        selfIdentity = GetComponent<MonsterIdentityManager>();
        monsterStats = GetComponent<MonsterStats>();
        hateSystem = MonsterHateSystem.EnsureOn(selfIdentity);
        EnsureGridManager();
    }

    public override bool CanExecute(MonsterActionContext context)
    {
        if (suppressedForTurn || context == null)
        {
            return false;
        }

        EnsureGridManager();
        EnsureHateSystem();
        EnsureMonsterStats();
        if (gridManager == null ||
            selfIdentity == null ||
            hateSystem == null ||
            monsterStats == null ||
            AttackRange <= 0)
        {
            return false;
        }

        return TrySelectAttackTarget(out _, out _);
    }

    public override void OnSkipped()
    {
        if (monsterAnimator != null)
        {
            monsterAnimator.ResetTrigger(Animator.StringToHash(attackTriggerName));
        }
    }

    protected override void OnStart()
    {
        EnsureGridManager();
        EnsureHateSystem();
        EnsureMonsterStats();

        if (suppressedForTurn)
        {
            CompleteAction();
            return;
        }

        if (gridManager == null || selfIdentity == null || hateSystem == null || monsterStats == null)
        {
            Debug.LogError($"[{name}] MonsterAttackAction 缺少 GridManager / MonsterIdentityManager / MonsterHateSystem / MonsterStats！");
            CompleteAction();
            return;
        }

        gridManager.EnsureGridSystemInitialized();

        if (!TrySelectAttackTarget(out MonsterTarget target, out _))
        {
            CompleteAction();
            return;
        }

        PlayAttackAnimation();
        ResolveAttackDamage(target);
        CompleteAction();
    }

    public void SetSuppressedForTurn(bool suppressed)
    {
        suppressedForTurn = suppressed;
    }

    public void ResetTurnState()
    {
        suppressedForTurn = false;
        if (monsterAnimator != null)
        {
            monsterAnimator.ResetTrigger(Animator.StringToHash(attackTriggerName));
        }
    }

    private bool TrySelectAttackTarget(out MonsterTarget target, out int distance)
    {
        target = null;
        distance = int.MaxValue;
        if (gridManager == null || selfIdentity == null || hateSystem == null || monsterStats == null)
        {
            return false;
        }

        CellManager selfCell = MonsterPathfinding.FindMonsterCell(gridManager, selfIdentity);
        if (selfCell == null)
        {
            return false;
        }

        bool includePlayer = ConcealmentCell.CanMonsterSeePlayer(selfCell);
        if (hateSystem.TrySelectTarget(
            gridManager,
            AttackRange,
            includePlayer,
            out target,
            out distance,
            out _))
        {
            return true;
        }

        if (includePlayer)
        {
            return hateSystem.TrySelectTarget(
              gridManager,
              AttackRange,
              false,
              out target,
              out distance,
              out _);
        }

        return false;
    }

    private void EnsureGridManager()
    {
        if (gridManager == null)
        {
            gridManager = FindFirstObjectByType<GridManager>();
        }
    }

    private void EnsureHateSystem()
    {
        if (hateSystem == null)
        {
            hateSystem = MonsterHateSystem.EnsureOn(selfIdentity);
        }
    }

    private void EnsureMonsterStats()
    {
        if (monsterStats == null)
        {
            monsterStats = GetComponent<MonsterStats>();
        }
    }

    private void PlayAttackAnimation()
    {
        if (monsterAnimator == null)
        {
            Debug.LogWarning($"[{name}] MonsterAnimator 未关联，跳过攻击动画。");
            return;
        }

        int attackTriggerHash = Animator.StringToHash(attackTriggerName);
        monsterAnimator.ResetTrigger(attackTriggerHash);
        monsterAnimator.SetTrigger(attackTriggerHash);
    }

    private void ResolveAttackDamage(MonsterTarget target)
    {
        (int monsterX, int monsterZ) = gridManager.GetGridPosition(transform.position);
        Vector2Int attackerGrid = new Vector2Int(monsterX, monsterZ);

        if (target.IsPlayer)
        {
            if (PlayerOrientationDamageController.Instance != null)
            {
                PlayerOrientationDamageController.Instance.ResolveMonsterAttack(
                  AttackDamage,
                  attackerGrid,
                  selfIdentity);
            }
            else if (CombatStatsManager.Instance != null)
            {
                CombatStatsManager.Instance.TakeDamage(AttackDamage);
            }
            return;
        }

        if (target.Monster == null)
        {
            return;
        }

        MonsterStats targetStats = target.Monster.GetComponent<MonsterStats>();
        if (targetStats == null)
        {
            targetStats = target.Monster.GetComponentInChildren<MonsterStats>();
        }

        if (targetStats == null)
        {
            Debug.LogWarning($"[{name}] 攻击目标 [{target.DisplayName}] 缺少 MonsterStats 组件。");
            return;
        }

        targetStats.TakeDamage(AttackDamage, selfIdentity);
    }

    public override void CancelAction()
    {
        base.CancelAction();

        if (monsterAnimator != null)
        {
            monsterAnimator.ResetTrigger(Animator.StringToHash(attackTriggerName));
        }
    }
}
