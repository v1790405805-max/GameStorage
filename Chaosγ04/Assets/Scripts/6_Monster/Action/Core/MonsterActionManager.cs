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

    [Header("动作序列")]
    [Tooltip("本回合要依次执行的动作列表")]
    [SerializeField] private List<MonsterActionStep> actionSequence = new List<MonsterActionStep>();

    private int currentIndex = -1;
    private bool isExecuting = false;
    private Coroutine sequenceCoroutine;
    private MonsterIdentityManager selfIdentity;
    private MonsterAttackAction attackAction;
    private GridManager gridManager;

    public event Action OnSequenceStarted;
    public event Action<MonsterActionBase> OnActionStarted;
    public event Action<MonsterActionBase> OnActionFinished;
    public event Action<MonsterActionBase> OnActionSkipped;
    public event Action OnSequenceFinished;

    public bool IsExecuting => isExecuting;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.RegisterEnemyBehaviour(this);
        }
    }

    private void OnDisable()
    {
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

        MonsterActionContext context = BuildActionContext();
        sequenceCoroutine = StartCoroutine(ExecuteSequenceRoutine(context));
    }

    public void StopSequence()
    {
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
    }

    private IEnumerator ExecuteSequenceRoutine(MonsterActionContext context)
    {
        isExecuting = true;
        OnSequenceStarted?.Invoke();

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

            if (!action.CanExecute(context))
            {
                Debug.Log($"[{name}] {action.GetType().Name} 的前置条件不满足，已跳过。");
                action.OnSkipped();
                OnActionSkipped?.Invoke(action);
                continue;
            }

            bool isCompleted = false;
            Action completionHandler = null;

            completionHandler = () =>
            {
                isCompleted = true;
                action.OnActionCompleted -= completionHandler;
            };

            action.OnActionCompleted += completionHandler;
            OnActionStarted?.Invoke(action);

            // 执行当前动作 (内部调用 OnStart)
            action.StartAction();

            // 等待动作内部调用 CompleteAction()
            yield return new WaitUntil(() => isCompleted);

            OnActionFinished?.Invoke(action);

            if (action.PostActionDelay > 0f)
            {
                yield return new WaitForSeconds(action.PostActionDelay);
            }
        }

        isExecuting = false;
        currentIndex = -1;
        sequenceCoroutine = null;

        // 通知 TurnManager，本怪物全部动作已完成
        OnSequenceFinished?.Invoke();
    }

    public void SetActionSequence(List<MonsterActionStep> steps)
    {
        if (isExecuting) return;
        actionSequence = steps ?? new List<MonsterActionStep>();
    }

    public void AddAction(MonsterActionBase action)
    {
        if (action != null)
        {
            actionSequence.Add(new MonsterActionStep
            {
                action = action
            });
        }
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
            attackAction = GetComponent<MonsterAttackAction>();
            if (attackAction == null && actionSequence != null)
            {
                for (int i = 0; i < actionSequence.Count; i++)
                {
                    MonsterActionStep step = actionSequence[i];
                    if (step != null && step.action is MonsterAttackAction candidate)
                    {
                        attackAction = candidate;
                        break;
                    }
                }
            }
        }

        if (gridManager == null)
        {
            gridManager = attackAction != null ? attackAction.gridManager : null;
            if (gridManager == null)
            {
                gridManager = FindFirstObjectByType<GridManager>();
            }
        }
    }

    private MonsterActionContext BuildActionContext()
    {
        ResolveReferences();

        bool hostileInAttackRangeAtTurnStart = false;
        if (selfIdentity != null && gridManager != null && attackAction != null)
        {
            hostileInAttackRangeAtTurnStart =
                MonsterTargetManager.HasHostileInAttackRange(
                    selfIdentity,
                    gridManager,
                    attackAction.AttackRange);
        }

        return new MonsterActionContext(
            selfIdentity,
            gridManager,
            attackAction,
            hostileInAttackRangeAtTurnStart);
    }

}
