using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 怪物攻击范围的数学形状。
/// </summary>
public enum MonsterAttackRangeType
{
    Straight = 1,
    Diamond = 2,
    Square = 3
}

/// <summary>
/// 攻击范围内部的可选区域模式。
/// 具体攻击目标始终由仇恨系统选出。
/// </summary>
public enum MonsterTargetSelectMode
{
    AnyCell,
    EdgeOnly,
    Aoe,
    AQuarterCircle
}

/// <summary>
/// Monster attack action. The attack target is selected by MonsterHateSystem and may be
/// the player or any other hostile-faction monster. Leave decisions suppress this action.
/// 已接入动画 Hit 关键帧事件同步结算与后摇控制。
/// </summary>
public class MonsterAttackAction : MonsterAttackActionBase
{
    [Header("攻击数值")]
    [Tooltip("本次攻击的基础伤害，最终伤害会叠加 MonsterStats 中的 AttackBuff。")]
    [SerializeField, Min(0)] private int attackDamage = 1;

    [Header("攻击范围")]
    [Tooltip("攻击范围使用的数学形状。")]
    [SerializeField] private MonsterAttackRangeType rangeType =
        MonsterAttackRangeType.Diamond;
    [Tooltip("攻击范围的最大格子距离。")]
    [SerializeField, Min(0)] private int attackRange = 1;
    [Tooltip("攻击范围内可选区域的模式。具体目标仍由仇恨系统选出。")]
    [SerializeField] private MonsterTargetSelectMode targetSelectMode =
        MonsterTargetSelectMode.AnyCell;

    [Header("网格引用")]
    [Tooltip("怪物攻击使用的网格管理器。")]
    public GridManager gridManager;

    [Tooltip("负责显示攻击范围与目标格的网格视觉管理器。")]
    [SerializeField] private GridVisualManager visualManager;

    [Tooltip("攻击范围与目标格使用的网格样式。")]
    [SerializeField] private GridStyleData attackGridStyle;

    [Header("攻击动画")]
    [Tooltip("怪物攻击使用的 Animator，留空时自动从子对象查找。")]
    [SerializeField] private Animator monsterAnimator;
    [Tooltip("Animator 中用于播放攻击动画的 Trigger 参数名。")]
    [SerializeField] private string attackTriggerName = "Attack";

    [Header("攻击范围预警显示时序")]
    [Tooltip("显示攻击范围后到显示目标确认格的等待时长。")]
    [SerializeField, Min(0f)] private float targetWarningDuration = 0.05f;
    [Tooltip("目标确认格的显示时长。")]
    [SerializeField, Min(0f)] private float targetConfirmedDuration = 0.3f;

    [Header("攻击伤害结算时序")]
    [Tooltip("攻击动画播放后等待多久结算伤害。")]
    [SerializeField, Min(0f)] private float damageApplyDelay = 0.35f;
    [Tooltip("伤害结算后等待攻击收招表现完成的时间。")]
    [SerializeField, Min(0f)] private float postDamageDelay = 0.25f;

    [Header("若进入反击阶段等待再进行攻击的时间间隔")]
    [Tooltip("反击卡打出后，等待其效果和动画表现再继续攻击的时长（秒）")]
    [SerializeField, Min(0f)] private float counterCardPresentationDelay = 0.4f;

    private MonsterIdentityManager selfIdentity;
    private MonsterHateSystem hateSystem;
    private MonsterStats monsterStats;
    private Animator resolvedMonsterAnimator;
    private bool suppressedForTurn;

    // 运行时状态
    private MonsterTarget currentAttackTarget;
    private Coroutine attackRoutine;
    private CellManager attackTargetCell;
    private readonly HashSet<CellManager> highlightedAttackRangeCells =
        new HashSet<CellManager>();

    public Animator MonsterAnimator
    {
        get
        {
            if (monsterAnimator != null)
            {
                return monsterAnimator;
            }

            if (resolvedMonsterAnimator == null)
            {
                resolvedMonsterAnimator =
                    GetComponentInChildren<Animator>(true);
            }

            return resolvedMonsterAnimator;
        }
    }

    public int AttackRange => attackRange;
    public MonsterTargetSelectMode TargetSelectMode => targetSelectMode;
    public override int PreferredMaxRange => attackRange;

    private int BaseAttackDamage => attackDamage;

    public int AttackDamage =>
        monsterStats != null
            ? monsterStats.ResolveAttackDamage(BaseAttackDamage)
            : BaseAttackDamage;

    private MonsterAttackRangeType AttackRangeType => rangeType;

    private GridStyleData AttackGridStyle => attackGridStyle;

    private float TargetWarningDuration => targetWarningDuration;

    private float TargetConfirmedDuration => targetConfirmedDuration;

    private float DamageApplyDelay => damageApplyDelay;

    private float PostDamageDelay => postDamageDelay;

    private void Awake()
    {
        selfIdentity = GetComponent<MonsterIdentityManager>();
        monsterStats = GetComponent<MonsterStats>();
        hateSystem = MonsterHateSystem.EnsureOn(selfIdentity);
        EnsureGridManager();
        EnsureVisualManager();
    }

    private void OnDisable()
    {
        StopAttackRoutine();
        RestoreAttackRangeVisuals();
        CounterTimeManager.Instance?.CancelPending(this);
    }

    public override bool CanExecute(MonsterActionContext context)
    {
        if (suppressedForTurn || context == null)
        {
            return false;
        }

        if (context.IsPatrolling ||
            (context.WarnAction != null &&
             context.WarnAction.IsRestricting))
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
        StopAttackRoutine();
        RestoreAttackRangeVisuals();
        ResetAttackAnimation();
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

        if (Context != null &&
            (Context.IsPatrolling ||
             (Context.WarnAction != null &&
              Context.WarnAction.IsRestricting)))
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
        RestoreAttackRangeVisuals();

        MonsterTarget target = null;
        MonsterCombatPlan plan = Context != null ? Context.CombatPlan : null;
        CellManager selfCell = MonsterGridNavigation.FindMonsterCell(
            gridManager,
            selfIdentity);
        bool plannedPlayerVisible =
            plan != null &&
            plan.Target != null &&
            selfCell != null &&
            (!plan.Target.IsPlayer ||
             ConcealmentCell.CanMonsterSeePlayer(selfCell));
        bool usingPlannedTarget =
            plan != null &&
            plan.HasSelectedAttack &&
            plan.SelectedAttack == this &&
            TryEvaluateAttackFrom(
                selfCell,
                plan.Target,
                plannedPlayerVisible,
                out _);

        if (usingPlannedTarget)
        {
            target = plan.Target;
        }
        else if (!TrySelectAttackTarget(out target, out _))
        {
            CompleteAction();
            return;
        }

        // 记录目标并启动打击时序协程
        currentAttackTarget = target;

        StopAttackRoutine();
        attackRoutine = StartCoroutine(AttackSequenceRoutine());
    }

    private IEnumerator AttackSequenceRoutine()
    {
        ShowAttackRangeWarning();
        yield return new WaitForSeconds(TargetWarningDuration);
        ShowAttackTargetConfirmed();
        yield return new WaitForSeconds(TargetConfirmedDuration);

        CounterTimeManager counterTimeManager = CounterTimeManager.Instance;
        if (currentAttackTarget != null &&
            currentAttackTarget.IsPlayer &&
            counterTimeManager != null &&
            counterTimeManager.TryOpen(this))
        {
            RestoreAttackRangeVisuals();
            counterTimeManager.FocusOnPlayerCell();
            yield return counterTimeManager.WaitForResolution(counterCardPresentationDelay);
        }

        PlayAttackAnimation();
        RestoreAttackRangeVisuals();

        // 等待攻击动画播放到伤害结算时间点
        yield return new WaitForSeconds(DamageApplyDelay);
        if (currentAttackTarget != null)
        {
            ResolveAttackDamage(currentAttackTarget.Cell);
        }

        // 等待伤害结算后的收招表现完成，再让行动结束
        yield return new WaitForSeconds(PostDamageDelay);

        CompleteAction();
        attackRoutine = null;
    }

    private void StopAttackRoutine()
    {
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }
    }

    private void ShowAttackRangeWarning()
    {
        GridStyleData gridStyle = AttackGridStyle;
        if (gridStyle == null)
        {
            Debug.LogWarning($"[{name}] MonsterAttackAction 未配置攻击 GridStyle，跳过攻击目标格提示。");
            return;
        }

        CellManager attackerCell = MonsterGridNavigation.FindMonsterCell(gridManager, selfIdentity);
        if (attackerCell == null)
        {
            Debug.LogWarning($"[{name}] 无法通过 GridManager / CellManager 获取怪物所在格，跳过攻击范围提示。");
            return;
        }

        highlightedAttackRangeCells.Clear();
        HashSet<CellManager> attackRange = MonsterGridNavigation.CalculateReachableCells(
            gridManager,
            selfIdentity,
            attackerCell,
            AttackRange,
            blockByMonsters: false,
            includePlayerCell: true);

        attackRange.RemoveWhere(
            cell => !IsCellWithinAttackShape(attackerCell, cell));

        if (currentAttackTarget != null && currentAttackTarget.Cell != null)
        {
            attackRange.Add(currentAttackTarget.Cell);
        }

        EnsureVisualManager();
        if (visualManager == null)
        {
            Debug.LogWarning($"[{name}] 找不到 GridVisualManager，跳过攻击范围提示。");
            return;
        }

        visualManager.SetReachablePatternColors(
            attackRange,
            (IReadOnlyCollection<CellManager>)null,
            gridStyle,
            Application.isPlaying);
        highlightedAttackRangeCells.UnionWith(attackRange);

        if (currentAttackTarget == null || currentAttackTarget.Cell == null)
        {
            return;
        }

        attackTargetCell = currentAttackTarget.Cell;
    }

    private void ShowAttackTargetConfirmed()
    {
        GridStyleData gridStyle = AttackGridStyle;
        if (gridStyle == null || attackTargetCell == null)
        {
            return;
        }

        attackTargetCell.SetCellColor(
            gridStyle.targetCellColor,
            Application.isPlaying,
            ignoreAccessState: true);
        attackTargetCell.SetLineColor(
            gridStyle.targetLineColor,
            ignoreAccessState: true);
    }

    private void RestoreAttackRangeVisuals()
    {
        if (gridManager != null)
        {
            foreach (CellManager cell in highlightedAttackRangeCells)
            {
                if (cell == null)
                {
                    continue;
                }

                cell.SetCellColor(
                    gridManager.cellNormalColor,
                    Application.isPlaying);
                cell.SetLineColor(gridManager.lineNormalColor);
            }
        }

        highlightedAttackRangeCells.Clear();
        attackTargetCell = null;
    }

    public void SetSuppressedForTurn(bool suppressed)
    {
        suppressedForTurn = suppressed;
    }

    public override void ResetTurnState()
    {
        suppressedForTurn = false;
        StopAttackRoutine();
        RestoreAttackRangeVisuals();
        ResetAttackAnimation();
    }

    private bool TrySelectAttackTarget(out MonsterTarget target, out int distance)
    {
        target = null;
        distance = int.MaxValue;
        if (gridManager == null || selfIdentity == null || hateSystem == null || monsterStats == null)
        {
            return false;
        }

        CellManager selfCell = MonsterGridNavigation.FindMonsterCell(gridManager, selfIdentity);
        if (selfCell == null)
        {
            return false;
        }

        bool includePlayer = ConcealmentCell.CanMonsterSeePlayer(selfCell);
        List<MonsterTarget> candidates = hateSystem.GetHostileTargets(gridManager);
        if (candidates == null || candidates.Count == 0)
        {
            return false;
        }

        int bestHate = int.MinValue;
        int bestDistance = int.MaxValue;

        foreach (MonsterTarget candidate in candidates)
        {
            if (!TryEvaluateAttackFrom(
                    selfCell,
                    candidate,
                    includePlayer,
                    out int candidateDistance))
            {
                continue;
            }

            int candidateHate = hateSystem.GetTotalHate(candidate, candidateDistance);
            bool better =
                candidateHate > bestHate ||
                (candidateHate == bestHate &&
                 candidateDistance < bestDistance);

            if (!better)
            {
                continue;
            }

            target = candidate;
            distance = candidateDistance;
            bestHate = candidateHate;
            bestDistance = candidateDistance;
        }

        return target != null;
    }

    public override bool TryEvaluateAttackFrom(
        CellManager selfCell,
        MonsterTarget candidate,
        bool playerVisible,
        out int pathDistance)
    {
        pathDistance = int.MaxValue;
        if (candidate == null || candidate.Cell == null)
        {
            return false;
        }

        if (candidate.IsPlayer && !playerVisible)
        {
            return false;
        }

        pathDistance = MonsterGridNavigation.GetPathDistance(
            gridManager,
            selfIdentity,
            selfCell,
            candidate.Cell);

        if (pathDistance == int.MaxValue || pathDistance > AttackRange)
        {
            return false;
        }

        return IsCellWithinAttackShape(selfCell, candidate.Cell);
    }

    private bool IsCellWithinAttackShape(
        CellManager originCell,
        CellManager targetCell)
    {
        if (gridManager == null || originCell == null || targetCell == null)
        {
            return false;
        }

        if (originCell == targetCell)
        {
            return true;
        }

        var (originX, originZ) = gridManager.GetCellGridPosition(originCell);
        var (targetX, targetZ) = gridManager.GetCellGridPosition(targetCell);
        int deltaX = Mathf.Abs(targetX - originX);
        int deltaZ = Mathf.Abs(targetZ - originZ);
        int range = AttackRange;

        switch (AttackRangeType)
        {
            case MonsterAttackRangeType.Straight:
                return (deltaX == 0 || deltaZ == 0) &&
                       deltaX + deltaZ <= range;

            case MonsterAttackRangeType.Diamond:
                return deltaX + deltaZ <= range;

            case MonsterAttackRangeType.Square:
                return Mathf.Max(deltaX, deltaZ) <= range;

            default:
                return false;
        }
    }

    private void EnsureGridManager()
    {
        if (gridManager == null)
        {
            gridManager = FindFirstObjectByType<GridManager>();
        }
    }

    private void EnsureVisualManager()
    {
        if (visualManager == null)
        {
            visualManager = FindFirstObjectByType<GridVisualManager>();
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

    private void ResetAttackAnimation()
    {
        Animator animator = MonsterAnimator;
        if (animator == null || string.IsNullOrEmpty(attackTriggerName))
        {
            return;
        }

        animator.ResetTrigger(
            Animator.StringToHash(attackTriggerName));
    }

    private void PlayAttackAnimation()
    {
        Animator animator = MonsterAnimator;
        if (animator == null)
        {
            Debug.LogWarning($"[{name}] 未配置攻击 Animator，跳过攻击动画。");
            return;
        }

        if (string.IsNullOrEmpty(attackTriggerName))
        {
            Debug.LogWarning($"[{name}] 未配置攻击动画 Trigger。");
            return;
        }

        int triggerHash = Animator.StringToHash(attackTriggerName);
        animator.ResetTrigger(triggerHash);
        animator.SetTrigger(triggerHash);
    }

    private void ResolveAttackDamage(CellManager lockedCell)
    {
        if (!gameObject.activeInHierarchy ||
            monsterStats == null ||
            monsterStats.currentHp <= 0)
        {
            Debug.Log($"[{name}] 攻击取消：攻击者已死亡或失效。");
            return;
        }

        if (lockedCell == null)
        {
            Debug.LogWarning($"[{name}] 攻击落空：目标格无效。");
            return;
        }

        CellManager attackerCell = MonsterGridNavigation.FindMonsterCell(gridManager, selfIdentity);
        Vector2Int? attackerGrid = null;
        if (attackerCell != null)
        {
            (int monsterX, int monsterZ) = gridManager.GetCellGridPosition(attackerCell);
            attackerGrid = new Vector2Int(monsterX, monsterZ);
        }
        else
        {
            Debug.LogWarning($"[{name}] 无法通过 GridManager / CellManager 获取攻击者所在格，玩家朝向减伤将跳过。");
        }

        // 攻击结算的是开始攻击时锁定的格子，而不是当时站在格上的角色引用。
        bool playerOccupiesCell =
            lockedCell.IsPlayerInside &&
            selfIdentity != null &&
            selfIdentity.IsHostileToPlayer();
        MonsterIdentityManager monsterOccupant = FindHostileMonsterOccupant(lockedCell);
        bool wasTargetingPlayer = currentAttackTarget != null && currentAttackTarget.IsPlayer;
        bool shouldHitPlayer =
            playerOccupiesCell &&
            (wasTargetingPlayer || monsterOccupant == null);

        if (shouldHitPlayer)
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

        if (monsterOccupant != null)
        {
            MonsterStats targetStats = monsterOccupant.GetComponent<MonsterStats>();
            if (targetStats == null)
            {
                targetStats = monsterOccupant.GetComponentInChildren<MonsterStats>();
            }

            if (targetStats == null)
            {
                Debug.LogWarning($"[{name}] 攻击目标 [{monsterOccupant.monsterId}] 缺少 MonsterStats 组件。");
                return;
            }

            targetStats.TakeDamage(AttackDamage, selfIdentity);
            return;
        }

        Debug.Log($"[{name}] 攻击落空：目标格上当前没有敌对角色。");
    }

    private MonsterIdentityManager FindHostileMonsterOccupant(CellManager cell)
    {
        if (cell == null || selfIdentity == null)
        {
            return null;
        }

        MonsterIdentityManager fallback = null;
        foreach (MonsterIdentityManager monster in cell.GetMonstersInside())
        {
            if (monster == null ||
                !monster.gameObject.activeInHierarchy ||
                !selfIdentity.IsHostileTo(monster))
            {
                continue;
            }

            if (currentAttackTarget != null &&
                currentAttackTarget.Monster == monster)
            {
                return monster;
            }

            if (fallback == null)
            {
                fallback = monster;
            }
        }

        return fallback;
    }

    public override void CancelAction()
    {
        base.CancelAction();
        StopAttackRoutine();
        RestoreAttackRangeVisuals();
        CounterTimeManager.Instance?.CancelPending(this);

        ResetAttackAnimation();
    }
}

