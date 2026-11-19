using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class MonsterActionStep
{
    [Tooltip("要执行的具体动作脚本组件")]
    public MonsterActionBase action;
}

public class MonsterActionManager : MonoBehaviour, ITurnStateListener
{
    [Header("回合初始缓冲时间")]
    [Tooltip("切到本怪物行动后，等待多久才开始执行第一个动作（秒）")]
    [SerializeField] private float startDelay = 0.5f;

    [Header("无有效动作时的视聚焦镜头暂留时间")]
    [Tooltip("本回合没有实际攻击或移动时，延迟多久再结束动作序列（秒）")]
    [SerializeField, Min(0f)] private float noActionHoldDuration = 2f;

    [Header("动作序列")]
    [Tooltip("本回合要依次执行的动作列表")]
    [SerializeField] private List<MonsterActionStep> actionSequence = new List<MonsterActionStep>();
    [Tooltip("序列缺少战斗规划动作时，运行时自动创建一个零时长 PlannerAction。")]
    [SerializeField] private bool autoCreateCombatPlanner = true;

    private int currentIndex = -1;
    private bool isExecuting = false;
    private Coroutine sequenceCoroutine;
    private MonsterIdentityManager selfIdentity;
    private MonsterAttackActionBase attackAction;
    private MonsterMoveAction moveAction;
    private MonsterLeaveAction leaveAction;
    private MonsterWarnAction warnAction;
    private GridManager gridManager;
    private bool terminateRemainingActions;
    private MonsterActionContext actionContext;
    private readonly HashSet<MonsterActionExclusionGroup> executedExclusionGroups =
        new HashSet<MonsterActionExclusionGroup>();

    public event Action OnSequenceStarted;
    public event Action<MonsterActionBase> OnActionStarted;
    public event Action<MonsterActionBase> OnActionFinished;
    public event Action<MonsterActionBase> OnActionSkipped;
    public event Action OnSequenceFinished;

    private static event Action<MonsterActionBase> TerminationRequested;

    public bool IsExecuting => isExecuting;
    public MonsterActionStage CurrentStage { get; private set; } =
        MonsterActionStage.Idle;
    public MonsterActionBase CurrentAction { get; private set; }
    public MonsterCombatPlan CombatPlan =>
        actionContext != null ? actionContext.CombatPlan : null;
    public bool IsMoving => CurrentStage == MonsterActionStage.Moving;
    public bool IsAttacking => CurrentStage == MonsterActionStage.Attacking;

    /// <summary>
    /// 请求终止其他动作：其他 ActionManager 的当前序列立即停止，
    /// 发出请求的 ActionManager 则在当前 Action 完成后停止后续动作。
    /// </summary>
    public static void RequestTerminationOfOtherActions(MonsterActionBase source)
    {
        if (source == null)
        {
            return;
        }

        TerminationRequested?.Invoke(source);
    }

    private void Awake()
    {
        ResolveReferences();
        EnsureCombatPlannerActionInSequence();
        EnsureLeaveActionInSequence();
        EnsureWarnActionOrderInSequence();
    }

    private void OnEnable()
    {
        TerminationRequested += HandleTerminationRequested;

        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.RegisterEnemyBehaviour(this);
        }
    }

    private void OnDisable()
    {
        // 怪物可能在动作序列执行中被击杀并 SetActive(false)。
        // 此时必须主动结束序列并通知等待方，否则 TurnManager 会一直等待 OnSequenceFinished。
        StopSequence();

        TerminationRequested -= HandleTerminationRequested;

        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.UnregisterEnemyBehaviour(this);
        }
    }

    // ==================================================================
    // 实现 ITurnStateListener 接口
    // ==================================================================
    public void OnTurnActivated()
    {
        StartSequence();
    }

    public void OnTurnDeactivated()
    {
        StopSequence();
    }

    // ==================================================================
    // 核心序列逻辑
    // ==================================================================
    public void StartSequence()
    {
        if (isExecuting)
        {
            Debug.LogWarning($"[{name}] 已有序列在执行中。");
            return;
        }

        if (actionSequence == null || actionSequence.Count == 0)
        {
            Debug.LogWarning($"[{name}] 动作序列为空，直接判定序列结束。");
            OnSequenceFinished?.Invoke();
            return;
        }

        ResetTurnDecisionState();
        terminateRemainingActions = false;
        executedExclusionGroups.Clear();
        CurrentAction = null;
        CurrentStage = MonsterActionStage.Idle;

        actionContext = BuildActionContext();
        sequenceCoroutine = StartCoroutine(
            ExecuteSequenceRoutine(actionContext));
    }

    public void StopSequence()
    {
        bool wasExecuting = isExecuting;

        if (sequenceCoroutine != null)
        {
            StopCoroutine(sequenceCoroutine);
            sequenceCoroutine = null;
        }

        if (currentIndex >= 0 && currentIndex < actionSequence.Count)
        {
            var currentStep = actionSequence[currentIndex];
            if (currentStep != null && currentStep.action != null)
            {
                currentStep.action.CancelAction();
            }
        }

        isExecuting = false;
        currentIndex = -1;
        terminateRemainingActions = false;
        CurrentAction = null;
        CurrentStage = MonsterActionStage.Idle;
        executedExclusionGroups.Clear();
        actionContext = null;

        if (wasExecuting)
        {
            OnSequenceFinished?.Invoke();
        }
    }

    private IEnumerator ExecuteSequenceRoutine(MonsterActionContext context)
    {
        isExecuting = true;
        OnSequenceStarted?.Invoke();
        bool didAttack = false;
        bool didMove = false;

        if (startDelay > 0f)
        {
            yield return new WaitForSeconds(startDelay);
        }

        for (int i = 0; i < actionSequence.Count; i++)
        {
            currentIndex = i;
            var step = actionSequence[i];

            if (step == null || step.action == null) continue;

            var action = step.action;
            action.BindContext(context);

            if (ShouldSkipForCombatSelection(action, context) ||
                !action.CanExecute(context))
            {
                Debug.Log($"[{name}] {action.GetType().Name} 的前置条件不满足，已跳过。");
                CurrentAction = action;
                CurrentStage = MonsterActionStage.Idle;
                action.OnSkipped();
                OnActionSkipped?.Invoke(action);
                CurrentAction = null;
                continue;
            }

            bool isCompleted = false;
            Action completionHandler = null;
            CellManager moveStartCell = null;

            if (action is MonsterMoveAction &&
                context != null &&
                context.GridManager != null &&
                context.Self != null)
            {
                context.GridManager.EnsureGridSystemInitialized();
                moveStartCell = MonsterGridNavigation.FindMonsterCell(
                    context.GridManager,
                    context.Self);
            }

            completionHandler = () =>
            {
                isCompleted = true;
                action.OnActionCompleted -= completionHandler;
            };

            action.OnActionCompleted += completionHandler;
            CurrentAction = action;
            CurrentStage = GetStageForAction(action);
            if (action.ExclusionGroup != MonsterActionExclusionGroup.None)
            {
                executedExclusionGroups.Add(action.ExclusionGroup);
            }

            OnActionStarted?.Invoke(action);

            // 执行当前动作 (内部调用 OnStart)
            action.StartAction();

            if (action is MonsterAttackActionBase)
            {
                didAttack = true;
            }

            // 等待动作内部调用 CompleteAction()
            yield return new WaitUntil(() => isCompleted);

            if (moveStartCell != null)
            {
                CellManager moveEndCell =
                    MonsterGridNavigation.FindMonsterCell(
                        context.GridManager,
                        context.Self);

                if (moveEndCell != null && moveEndCell != moveStartCell)
                {
                    didMove = true;
                }
            }

            OnActionFinished?.Invoke(action);
            CurrentAction = null;
            CurrentStage = MonsterActionStage.Idle;

            if (terminateRemainingActions)
            {
                break;
            }

            if (action.PostActionDelay > 0f)
            {
                yield return new WaitForSeconds(action.PostActionDelay);
            }
        }

        if (!didAttack &&
            !didMove &&
            noActionHoldDuration > 0f)
        {
            yield return new WaitForSeconds(noActionHoldDuration);
        }

        isExecuting = false;
        currentIndex = -1;
        sequenceCoroutine = null;
        terminateRemainingActions = false;
        CurrentAction = null;
        CurrentStage = MonsterActionStage.Finished;

        // 通知 TurnManager，本怪物全部动作已完成
        OnSequenceFinished?.Invoke();
    }

    private bool ShouldSkipForCombatSelection(
        MonsterActionBase action,
        MonsterActionContext context)
    {
        if (action == null)
        {
            return true;
        }

        if (action.ExclusionGroup != MonsterActionExclusionGroup.None &&
            executedExclusionGroups.Contains(action.ExclusionGroup))
        {
            return true;
        }

        if (action is MonsterAttackActionBase attackAction &&
            context != null &&
            context.CombatPlan != null &&
            context.CombatPlan.SelectedAttack != null &&
            context.CombatPlan.SelectedAttack != attackAction)
        {
            return true;
        }

        return false;
    }

    private static MonsterActionStage GetStageForAction(
        MonsterActionBase action)
    {
        if (action == null)
        {
            return MonsterActionStage.Idle;
        }

        switch (action.Category)
        {
            case MonsterActionCategory.Planning:
                return MonsterActionStage.PlanningCombat;
            case MonsterActionCategory.Movement:
                return MonsterActionStage.Moving;
            case MonsterActionCategory.Attack:
                return MonsterActionStage.Attacking;
            default:
                return MonsterActionStage.OtherAction;
        }
    }

    public void SetActionSequence(List<MonsterActionStep> steps)
    {
        if (isExecuting) return;
        actionSequence = steps ?? new List<MonsterActionStep>();
        EnsureCombatPlannerActionInSequence();
        EnsureLeaveActionInSequence();
        EnsureWarnActionOrderInSequence();
    }

    public void AddAction(MonsterActionBase action)
    {
        if (actionSequence == null)
        {
            actionSequence = new List<MonsterActionStep>();
        }

        if (action != null)
        {
            actionSequence.Add(new MonsterActionStep
            {
                action = action
            });
        }

        EnsureCombatPlannerActionInSequence();
        EnsureLeaveActionInSequence();
        EnsureWarnActionOrderInSequence();
    }

    public void ClearActions()
    {
        if (isExecuting) return;
        actionSequence.Clear();
    }

    private void ResolveReferences()
    {
        if (selfIdentity == null)
        {
            selfIdentity = GetComponent<MonsterIdentityManager>();
        }

        if (attackAction == null)
        {
            attackAction = GetComponent<MonsterAttackActionBase>();
            if (attackAction == null && actionSequence != null)
            {
                for (int i = 0; i < actionSequence.Count; i++)
                {
                    MonsterActionStep step = actionSequence[i];
                    if (step != null &&
                        step.action is MonsterAttackActionBase candidate)
                    {
                        attackAction = candidate;
                        break;
                    }
                }
            }
        }

        if (moveAction == null)
        {
            moveAction = GetComponent<MonsterMoveAction>();
            if (moveAction == null && actionSequence != null)
            {
                for (int i = 0; i < actionSequence.Count; i++)
                {
                    MonsterActionStep step = actionSequence[i];
                    if (step != null && step.action is MonsterMoveAction candidate)
                    {
                        moveAction = candidate;
                        break;
                    }
                }
            }
        }

        if (leaveAction == null)
        {
            leaveAction = GetComponent<MonsterLeaveAction>();
            if (leaveAction == null && actionSequence != null)
            {
                for (int i = 0; i < actionSequence.Count; i++)
                {
                    MonsterActionStep step = actionSequence[i];
                    if (step != null && step.action is MonsterLeaveAction candidate)
                    {
                        leaveAction = candidate;
                        break;
                    }
                }
            }
        }

        if (warnAction == null && actionSequence != null)
        {
            for (int i = 0; i < actionSequence.Count; i++)
            {
                MonsterActionStep step = actionSequence[i];
                if (step != null &&
                    step.action is MonsterWarnAction candidate)
                {
                    warnAction = candidate;
                    break;
                }
            }
        }

        if (gridManager == null)
        {
            gridManager = moveAction != null ? moveAction.gridManager : null;
            if (gridManager == null)
            {
                gridManager = FindFirstObjectByType<GridManager>();
            }
        }
    }

    public int GetActionIndex(MonsterActionBase action)
    {
        if (action == null || actionSequence == null)
        {
            return -1;
        }

        for (int i = 0; i < actionSequence.Count; i++)
        {
            if (actionSequence[i] != null && actionSequence[i].action == action)
            {
                return i;
            }
        }

        return -1;
    }

    public T GetActionOfType<T>() where T : MonsterActionBase
    {
        if (actionSequence != null)
        {
            for (int i = 0; i < actionSequence.Count; i++)
            {
                if (actionSequence[i] != null && actionSequence[i].action is T candidate)
                {
                    return candidate;
                }
            }
        }

        return GetComponent<T>();
    }

    public void GetActionsOfType<T>(List<T> results)
        where T : MonsterActionBase
    {
        if (results == null)
        {
            return;
        }

        results.Clear();
        if (actionSequence == null)
        {
            return;
        }

        HashSet<T> added = new HashSet<T>();
        for (int i = 0; i < actionSequence.Count; i++)
        {
            MonsterActionStep step = actionSequence[i];
            if (step != null &&
                step.action is T candidate &&
                added.Add(candidate))
            {
                results.Add(candidate);
            }
        }
    }

    private void ResetTurnDecisionState()
    {
        if (actionSequence == null)
        {
            return;
        }

        HashSet<MonsterActionBase> resetActions =
            new HashSet<MonsterActionBase>();
        for (int i = 0; i < actionSequence.Count; i++)
        {
            MonsterActionStep step = actionSequence[i];
            if (step != null &&
                step.action != null &&
                resetActions.Add(step.action))
            {
                step.action.ResetTurnState();
            }
        }
    }

    private void HandleTerminationRequested(MonsterActionBase source)
    {
        MonsterActionManager sourceManager =
            source != null ? source.GetComponent<MonsterActionManager>() : null;

        if (sourceManager == this)
        {
            TerminateRemainingActionsAfter(source);
            return;
        }

        StopSequence();
    }

    private void TerminateRemainingActionsAfter(MonsterActionBase source)
    {
        if (!isExecuting ||
            source == null ||
            currentIndex < 0 ||
            actionSequence == null ||
            currentIndex >= actionSequence.Count)
        {
            return;
        }

        MonsterActionStep currentStep = actionSequence[currentIndex];
        if (currentStep == null || currentStep.action != source)
        {
            return;
        }

        terminateRemainingActions = true;
    }

    private void EnsureLeaveActionInSequence()
    {
        // 只在已经引用 MonsterLeaveAction 时把它移动到序列最前面；不自动创建组件或插入动作。
        if (actionSequence == null)
        {
            return;
        }

        if (leaveAction == null)
        {
            for (int i = 0; i < actionSequence.Count; i++)
            {
                MonsterActionStep step = actionSequence[i];
                if (step != null && step.action is MonsterLeaveAction candidate)
                {
                    leaveAction = candidate;
                    break;
                }
            }
        }

        if (leaveAction == null)
        {
            return;
        }

        int leaveIndex = GetActionIndex(leaveAction);
        if (leaveIndex < 0)
        {
            return;
        }

        if (leaveIndex == 0)
        {
            return;
        }

        for (int i = actionSequence.Count - 1; i >= 0; i--)
        {
            if (actionSequence[i] != null && actionSequence[i].action == leaveAction)
            {
                actionSequence.RemoveAt(i);
            }
        }

        actionSequence.Insert(0, new MonsterActionStep
        {
            action = leaveAction
        });
    }

    private void EnsureWarnActionOrderInSequence()
    {
        if (actionSequence == null)
        {
            return;
        }

        warnAction = null;
        for (int i = 0; i < actionSequence.Count; i++)
        {
            MonsterActionStep step = actionSequence[i];
            if (step != null &&
                step.action is MonsterWarnAction candidate)
            {
                warnAction = candidate;
                break;
            }
        }

        if (warnAction == null)
        {
            return;
        }

        for (int i = actionSequence.Count - 1; i >= 0; i--)
        {
            if (actionSequence[i] != null &&
                actionSequence[i].action is MonsterWarnAction)
            {
                actionSequence.RemoveAt(i);
            }
        }

        int insertIndex = 0;
        int leaveIndex = GetActionIndex(leaveAction);
        if (leaveIndex >= 0)
        {
            insertIndex = leaveIndex + 1;
        }

        actionSequence.Insert(insertIndex, new MonsterActionStep
        {
            action = warnAction
        });
    }

    private void EnsureCombatPlannerActionInSequence()
    {
        if (!autoCreateCombatPlanner ||
            actionSequence == null ||
            actionSequence.Count == 0)
        {
            return;
        }

        bool hasAttackAction = false;
        MonsterCombatPlannerAction plannerAction = null;
        for (int i = 0; i < actionSequence.Count; i++)
        {
            MonsterActionStep step = actionSequence[i];
            if (step == null || step.action == null)
            {
                continue;
            }

            if (step.action is MonsterAttackActionBase)
            {
                hasAttackAction = true;
            }

            if (plannerAction == null &&
                step.action is MonsterCombatPlannerAction planner)
            {
                plannerAction = planner;
            }
        }

        if (!hasAttackAction)
        {
            return;
        }

        if (plannerAction == null)
        {
            plannerAction = GetComponent<MonsterCombatPlannerAction>();
            if (plannerAction == null)
            {
                plannerAction = gameObject.AddComponent<MonsterCombatPlannerAction>();
            }
        }

        for (int i = actionSequence.Count - 1; i >= 0; i--)
        {
            if (actionSequence[i] != null &&
                actionSequence[i].action == plannerAction)
            {
                actionSequence.RemoveAt(i);
            }
        }

        actionSequence.Insert(0, new MonsterActionStep
        {
            action = plannerAction
        });
    }

    private MonsterActionContext BuildActionContext()
    {
        ResolveReferences();

        List<MonsterAttackActionBase> attackActions =
            new List<MonsterAttackActionBase>();
        GetActionsOfType(attackActions);

        return new MonsterActionContext(
            selfIdentity,
            gridManager,
            moveAction,
            attackActions,
            warnAction);
    }

}
