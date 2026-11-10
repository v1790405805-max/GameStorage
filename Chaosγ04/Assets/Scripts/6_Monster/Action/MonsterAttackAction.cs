using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster attack action. The attack target is selected by MonsterHateSystem and may be
/// the player or any other hostile-faction monster. Leave decisions suppress this action.
/// 已接入动画 Hit 关键帧事件同步结算与后摇控制。
/// </summary>
public class MonsterAttackAction : MonsterActionBase
{
    [Header("网格管理器引用")]
    public GridManager gridManager;

    [Header("网格视觉引用")]
    [SerializeField] private GridVisualManager visualManager;

    [Header("Animator引用")]
    public Animator monsterAnimator;

    [Header("动画参数配置")]
    public string attackTriggerName = "Attack";

    [Header("攻击目标格提示")]
    [Tooltip("引用 MonsterAttack 的 GridStyleData，用于攻击目标格的预警色与确认色。")]
    [SerializeField] private GridStyleData attackGridStyle;

    [Tooltip("目标格显示 CellClickedColor / LineClickedColor 的预警时长（秒）。")]
    [SerializeField, Min(0f)] private float targetWarningDuration = 0.05f;

    [Tooltip("目标格切换为 TargetCellColor / TargetLineColor 后，等待多久再发起攻击（秒）。")]
    [SerializeField, Min(0f)] private float targetConfirmedDuration = 0.3f;

    [Header("打击节奏与保底配置")]
    [Tooltip("若动画未配置 Hit 事件，超时多少秒后自动保底扣血（防卡死）")]
    [SerializeField] private float hitDelayFallback = 0.35f;

    [Tooltip("击中判定后，等待收招动作播放完毕的时长（秒），之后结束怪物行动")]
    [SerializeField] private float postAttackDuration = 0.25f;

    [Header("反击时间")]
    [Tooltip("反击卡打出后，等待其效果和动画表现再继续攻击的时长（秒）")]
    [SerializeField, Min(0f)] private float counterCardPresentationDelay = 0.4f;

    private MonsterIdentityManager selfIdentity;
    private MonsterHateSystem hateSystem;
    private MonsterStats monsterStats;
    private bool suppressedForTurn;

    // 运行时状态
    private MonsterTarget currentAttackTarget;
    private bool hasDealtDamage;
    private Coroutine attackRoutine;
    private CellManager attackTargetCell;
    private readonly HashSet<CellManager> highlightedAttackRangeCells = new HashSet<CellManager>();

    public int AttackRange => monsterStats != null ? monsterStats.attackRange : 0;
    public int AttackDamage => monsterStats != null ? monsterStats.attackDamage : 0;

    private void Awake()
    {
        selfIdentity = GetComponent<MonsterIdentityManager>();
        monsterStats = GetComponent<MonsterStats>();
        hateSystem = MonsterHateSystem.EnsureOn(selfIdentity);
        EnsureGridManager();
        EnsureVisualManager();
        EnsureEventForwarder();
    }

    private void OnDisable()
    {
        StopAttackRoutine();
        RestoreAttackRangeVisuals();
        CounterTimeManager.Instance?.CancelPending(this);
    }

    /// <summary>
    /// 确保带有 Animator 的子物体上挂有事件转发器，捕获动画帧抛出的 Hit
    /// </summary>
    private void EnsureEventForwarder()
    {
        if (monsterAnimator != null)
        {
            MonsterAnimationEventForwarder forwarder = monsterAnimator.GetComponent<MonsterAnimationEventForwarder>();
            if (forwarder == null)
            {
                forwarder = monsterAnimator.gameObject.AddComponent<MonsterAnimationEventForwarder>();
            }
            forwarder.attackAction = this;
        }
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
        StopAttackRoutine();
        RestoreAttackRangeVisuals();
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
        EnsureEventForwarder();

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
        RestoreAttackRangeVisuals();

        if (!TrySelectAttackTarget(out MonsterTarget target, out _))
        {
            CompleteAction();
            return;
        }

        // 记录目标并启动打击时序协程
        currentAttackTarget = target;
        hasDealtDamage = false;

        StopAttackRoutine();
        attackRoutine = StartCoroutine(AttackSequenceRoutine());
    }

    private IEnumerator AttackSequenceRoutine()
    {
        ShowAttackRangeWarning();
        yield return new WaitForSeconds(targetWarningDuration);
        ShowAttackTargetConfirmed();
        yield return new WaitForSeconds(targetConfirmedDuration);

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

        // 等待动画中的 Hit 事件触发，若超时则通过 fallback 自动保底执行
        yield return new WaitForSeconds(hitDelayFallback);

        if (!hasDealtDamage)
        {
            Debug.LogWarning($"[{name}] 未在预定时长内收到 Hit 动画事件，触发保底伤害结算！");
            ExecuteHitDamage();
        }

        // 等待怪物把挥刀收招后摇播放完毕，再让行动结束
        yield return new WaitForSeconds(postAttackDuration);

        CompleteAction();
        attackRoutine = null;
    }

    /// <summary>
    /// 【核心事件响应】动画播放到击中帧时调用
    /// </summary>
    public void OnAnimationHit()
    {
        if (!hasDealtDamage)
        {
            ExecuteHitDamage();
        }
    }

    // 兼容可能直接在根节点触发的同名动画事件
    public void Hit() => OnAnimationHit();

    private void ExecuteHitDamage()
    {
        hasDealtDamage = true;
        if (currentAttackTarget != null)
        {
            ResolveAttackDamage(currentAttackTarget);
        }
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
        if (attackGridStyle == null)
        {
            Debug.LogWarning($"[{name}] MonsterAttackAction 未配置 attackGridStyle，跳过攻击目标格提示。");
            return;
        }

        CellManager attackerCell = MonsterPathfinding.FindMonsterCell(gridManager, selfIdentity);
        if (attackerCell == null)
        {
            Debug.LogWarning($"[{name}] 无法通过 GridManager / CellManager 获取怪物所在格，跳过攻击范围提示。");
            return;
        }

        highlightedAttackRangeCells.Clear();
        HashSet<CellManager> attackRange = MonsterPathfinding.CalculateReachableCells(
            gridManager,
            selfIdentity,
            attackerCell,
            AttackRange,
            blockByMonsters: false,
            includePlayerCell: true);

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

        visualManager.HighlightCardRange(attackRange, attackGridStyle, playerCell: null, RangeType.Diamond);
        highlightedAttackRangeCells.UnionWith(attackRange);

        if (currentAttackTarget == null || currentAttackTarget.Cell == null)
        {
            return;
        }

        attackTargetCell = currentAttackTarget.Cell;
    }

    private void ShowAttackTargetConfirmed()
    {
        if (attackGridStyle == null || attackTargetCell == null)
        {
            return;
        }

        attackTargetCell.SetCellColor(
            attackGridStyle.targetCellColor,
            Application.isPlaying,
            ignoreAccessState: true);
        attackTargetCell.SetLineColor(
            attackGridStyle.targetLineColor,
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

                cell.SetCellColor(gridManager.cellNormalColor, Application.isPlaying);
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

    public void ResetTurnState()
    {
        suppressedForTurn = false;
        StopAttackRoutine();
        RestoreAttackRangeVisuals();
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
        CellManager attackerCell = MonsterPathfinding.FindMonsterCell(gridManager, selfIdentity);
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
        StopAttackRoutine();
        RestoreAttackRangeVisuals();
        CounterTimeManager.Instance?.CancelPending(this);

        if (monsterAnimator != null)
        {
            monsterAnimator.ResetTrigger(Animator.StringToHash(attackTriggerName));
        }
    }
}

/// <summary>
/// 辅助事件转发器：运行时自动附加在子物体 Animator 上，接收 Unity Animation Event 并通知主动作脚本
/// </summary>
public class MonsterAnimationEventForwarder : MonoBehaviour
{
    [HideInInspector] public MonsterAttackAction attackAction;

    // 匹配动画切片中的 "Hit" 帧事件
    public void Hit()
    {
        attackAction?.OnAnimationHit();
    }

    // 兼容可能使用的 "OnHit"
    public void OnHit()
    {
        attackAction?.OnAnimationHit();
    }
}
