using UnityEngine;

/// <summary>
/// 永久提升自身攻击力，并在触发后持续支付每回合生命代价。
/// 触发回合不会扣血；之后每次轮到自身行动时扣除生命值。
/// </summary>
[DisallowMultipleComponent]
public sealed class BionicMan_SelfBuffAction : MonsterActionBase
{
    [Header("自身增益")]
    [Tooltip("每次触发时永久增加的攻击力。")]
    [SerializeField, Min(1)] private int attackBonus = 2;

    [Header("持续生命代价")]
    [Tooltip("效果生效后，每次轮到自身行动时直接损失的生命值。该伤害无视护甲。")]
    [SerializeField, Min(1)] private int healthCostPerTurn = 2;

    [Header("冷却")]
    [Tooltip("触发后需要跳过的完整回合数；填写 2 表示触发后两回合内不可再次触发。")]
    [SerializeField, Min(0)] private int cooldownRounds = 2;

    private MonsterStats selfStats;
    private MonsterActionManager actionManager;
    private MonsterActionManager subscribedActionManager;
    private int lastTriggeredRound = int.MinValue;
    private bool isHealthCostActive;

    public bool IsOnCooldown
    {
        get
        {
            if (cooldownRounds <= 0 || lastTriggeredRound == int.MinValue)
            {
                return false;
            }

            int currentRound = GetCurrentRound();
            return currentRound >= lastTriggeredRound &&
                   currentRound - lastTriggeredRound <= cooldownRounds;
        }
    }

    public override bool CanExecute(MonsterActionContext context)
    {
        ResolveReferences();
        return selfStats != null &&
               !IsOnCooldown &&
               !IsWaitingForHostileDetection(context);
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        BindToActionManager();
    }

    private void OnDisable()
    {
        UnbindFromActionManager();
    }

    protected override void OnStart()
    {
        ResolveReferences();

        if (IsWaitingForHostileDetection(Context))
        {
            CompleteAction();
            return;
        }

        if (selfStats == null)
        {
            Debug.LogError($"[{name}] BionicMan_SelfBuffAction 缺少 MonsterStats。");
            CompleteAction();
            return;
        }

        selfStats.AddAttackBuff(attackBonus);
        isHealthCostActive = true;
        lastTriggeredRound = GetCurrentRound();
        BindToActionManager();

        Debug.Log(
            $"[{name}] 自身攻击力提升 {attackBonus} 点；此后每次自身行动开始时，" +
            $"扣除 {healthCostPerTurn} 点生命值。");

        CompleteAction();
    }

    private static bool IsWaitingForHostileDetection(
        MonsterActionContext context)
    {
        return context != null &&
               (context.IsPatrolling ||
                (context.WarnAction != null &&
                 context.WarnAction.IsRestricting));
    }

    private void HandleOwnActionStarted()
    {
        if (!isHealthCostActive || selfStats == null || selfStats.currentHp <= 0)
        {
            return;
        }

        selfStats.TakeSelfDamage(healthCostPerTurn);
    }

    private void BindToActionManager()
    {
        if (subscribedActionManager == actionManager)
        {
            return;
        }

        UnbindFromActionManager();
        subscribedActionManager = actionManager;

        if (subscribedActionManager != null)
        {
            subscribedActionManager.OnSequenceStarted += HandleOwnActionStarted;
        }
    }

    private void UnbindFromActionManager()
    {
        if (subscribedActionManager != null)
        {
            subscribedActionManager.OnSequenceStarted -= HandleOwnActionStarted;
            subscribedActionManager = null;
        }
    }

    private void ResolveReferences()
    {
        if (selfStats == null)
        {
            selfStats = GetComponent<MonsterStats>();
            if (selfStats == null)
            {
                selfStats = GetComponentInChildren<MonsterStats>(true);
            }
        }

        if (actionManager == null)
        {
            actionManager = GetComponent<MonsterActionManager>();
            if (actionManager == null)
            {
                actionManager = GetComponentInParent<MonsterActionManager>(true);
            }
        }
    }

    private static int GetCurrentRound()
    {
        return TurnManager.Instance != null
            ? TurnManager.Instance.currentRoundCount
            : 0;
    }
}
