using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 敌方攻击命中前触发的反击时间状态。
/// 该状态覆盖在敌方回合之上，不改变 TurnManager 的回合类型。
/// </summary>
[DisallowMultipleComponent]
public class CounterTimeManager : MonoBehaviour
{
    private enum CounterTimeState
    {
        Inactive,
        WaitingForCard,
        ResolvingCard
    }

    public static CounterTimeManager Instance { get; private set; }

    [Header("放弃反击UI")]
    [Tooltip("反击时间中显示的放弃反击按钮；系统会自动显示和隐藏")]
    [SerializeField] private Button abandonCounterButton;

    private CounterTimeState state = CounterTimeState.Inactive;
    private MonsterAttackAction pendingAttack;
    private bool hasPlayedCard;

    public bool IsActive => state != CounterTimeState.Inactive;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        ResetState();
        BindAbandonButton(abandonCounterButton);
    }

    private void OnDestroy()
    {
        if (Instance != this)
        {
            return;
        }

        UnbindAbandonButton();
        ResetState();
        Instance = null;
    }

    public static CounterTimeManager EnsureInstance(GameObject fallbackHost = null)
    {
        if (Instance != null)
        {
            return Instance;
        }

        CounterTimeManager existing = FindFirstObjectByType<CounterTimeManager>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        return fallbackHost != null
            ? fallbackHost.AddComponent<CounterTimeManager>()
            : null;
    }

    private void ResetState()
    {
        state = CounterTimeState.Inactive;
        pendingAttack = null;
        hasPlayedCard = false;
        UpdateAbandonButton(false);
    }

    private void BindAbandonButton(Button button)
    {
        if (abandonCounterButton == button)
        {
            UpdateAbandonButton(state == CounterTimeState.WaitingForCard);
            return;
        }

        UnbindAbandonButton();
        abandonCounterButton = button;
        if (abandonCounterButton != null)
        {
            abandonCounterButton.onClick.AddListener(AbandonCounterWindow);
        }

        UpdateAbandonButton(state == CounterTimeState.WaitingForCard);
    }

    private void UnbindAbandonButton()
    {
        if (abandonCounterButton != null)
        {
            abandonCounterButton.onClick.RemoveListener(AbandonCounterWindow);
            abandonCounterButton = null;
        }
    }

    /// <summary>
    /// 在敌人已经选中玩家作为攻击目标、但尚未播放攻击动画时调用。
    /// </summary>
    public bool TryOpen(MonsterAttackAction attackAction)
    {
        if (state != CounterTimeState.Inactive ||
            attackAction == null ||
            !CanAnyCounterCardBePlayed())
        {
            return false;
        }

        pendingAttack = attackAction;
        hasPlayedCard = false;
        state = CounterTimeState.WaitingForCard;
        UpdateAbandonButton(true);
        return true;
    }

    /// <summary>
    /// 反击时间中仅允许使用费用足够的反击卡，并且整个窗口只能成功使用一张。
    /// </summary>
    public bool CanUseCard(CardData card)
    {
        if (state != CounterTimeState.WaitingForCard || hasPlayedCard)
        {
            return false;
        }

        CombatStatsManager combatStats = CombatStatsManager.Instance;
        if (combatStats == null || combatStats.currentEnergy <= 0)
        {
            return false;
        }

        return IsAffordableCounterCard(card, combatStats.currentEnergy);
    }

    /// <summary>
    /// 由 CardManager 在卡牌效果、动画和 UI 更新均已触发后调用。
    /// </summary>
    public void NotifyCardPlayed(CardData card)
    {
        if (state != CounterTimeState.WaitingForCard ||
            hasPlayedCard ||
            card == null ||
            !card.isCounterCard)
        {
            return;
        }

        hasPlayedCard = true;
        state = CounterTimeState.ResolvingCard;
        UpdateAbandonButton(false);
    }

    /// <summary>
    /// 让当前 AttackAction 等待玩家出牌或放弃反击，并在出牌后留出表现时间。
    /// </summary>
    public IEnumerator WaitForResolution(float cardPresentationDelay)
    {
        while (state == CounterTimeState.WaitingForCard)
        {
            yield return null;
        }

        bool cardWasPlayed = hasPlayedCard;

        state = CounterTimeState.Inactive;
        pendingAttack = null;
        hasPlayedCard = false;
        UpdateAbandonButton(false);

        if (cardWasPlayed && cardPresentationDelay > 0f)
        {
            yield return new WaitForSeconds(cardPresentationDelay);
        }
    }

    private void AbandonCounterWindow()
    {
        if (state != CounterTimeState.WaitingForCard)
        {
            return;
        }

        state = CounterTimeState.Inactive;
        UpdateAbandonButton(false);
    }

    public void CancelPending(MonsterAttackAction attackAction)
    {
        if (pendingAttack != attackAction)
        {
            return;
        }

        state = CounterTimeState.Inactive;
        pendingAttack = null;
        hasPlayedCard = false;
        UpdateAbandonButton(false);
    }

    private static bool CanAnyCounterCardBePlayed()
    {
        CombatStatsManager combatStats = CombatStatsManager.Instance;
        if (combatStats == null ||
            combatStats.currentEnergy <= 0 ||
            CardManager.Instance == null)
        {
            return false;
        }

        foreach (CardData card in CardManager.Instance.hand)
        {
            if (IsAffordableCounterCard(card, combatStats.currentEnergy))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAffordableCounterCard(CardData card, int availableEnergy)
    {
        return card != null &&
               card.isCounterCard &&
               availableEnergy >= card.GetEffectiveCost();
    }

    private void UpdateAbandonButton(bool visible)
    {
        if (abandonCounterButton == null)
        {
            return;
        }

        abandonCounterButton.gameObject.SetActive(visible);
    }
}
