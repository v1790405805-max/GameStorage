using System.Collections;
using DG.Tweening;
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

    [Header("反击视角聚焦")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private PlayerMoveController playerMoveController;
    [SerializeField, Min(0f)] private float focusDuration = 0.4f;

    private CounterTimeState state = CounterTimeState.Inactive;
    private MonsterAttackAction pendingAttack;
    private bool hasPlayedCard;
    private bool isCounterStageActive;
    private Tween focusTween;

    public bool IsActive => state != CounterTimeState.Inactive;
    public bool IsCounterStageActive => isCounterStageActive;
    private Camera MainCamera => MainCameraAppoint.Current;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        ResolveFocusReferences();
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
        isCounterStageActive = false;
        focusTween?.Kill();
        focusTween = null;
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
        isCounterStageActive = true;
        state = CounterTimeState.WaitingForCard;
        UpdateAbandonButton(true);
        return true;
    }

    /// <summary>
    /// 将视角聚焦到玩家当前所在单元格。
    /// 玩家格通过 GridManager / CellManager 解析，不使用攻击者 Transform 推导。
    /// </summary>
    public void FocusOnPlayerCell()
    {
        ResolveFocusReferences();

        Camera mainCamera = MainCamera;
        if (mainCamera == null || gridManager == null || playerMoveController == null)
        {
            return;
        }

        CellManager playerCell = ResolvePlayerCell();
        if (playerCell == null)
        {
            return;
        }

        Vector3 focusPoint = GetCellFocusPoint(playerCell);
        Ray centerRay = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Plane focusPlane = new Plane(Vector3.up, new Vector3(0f, focusPoint.y, 0f));
        if (!focusPlane.Raycast(centerRay, out float enter))
        {
            return;
        }

        Vector3 centerWorldPoint = centerRay.GetPoint(enter);
        Vector3 worldDelta = focusPoint - centerWorldPoint;
        worldDelta.y = 0f;

        Transform cameraTransform = mainCamera.transform;
        Vector3 planeDelta =
            worldDelta - cameraTransform.forward * Vector3.Dot(worldDelta, cameraTransform.forward);
        Vector3 localDelta = cameraTransform.parent != null
            ? cameraTransform.parent.InverseTransformVector(planeDelta)
            : planeDelta;
        Vector3 targetLocalPosition = cameraTransform.localPosition + localDelta;

        focusTween?.Kill();
        cameraTransform.DOKill();
        focusTween = cameraTransform
            .DOLocalMove(targetLocalPosition, focusDuration)
            .SetEase(Ease.OutCubic);
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

        FinishCounterStage();
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

    private void ResolveFocusReferences()
    {
        if (gridManager == null)
        {
            gridManager = FindFirstObjectByType<GridManager>();
        }

        if (playerMoveController == null)
        {
            playerMoveController = FindFirstObjectByType<PlayerMoveController>();
        }
    }

    private CellManager ResolvePlayerCell()
    {
        Vector2Int playerGrid = playerMoveController.PlayerGridPos;
        CellManager trackedCell = playerMoveController.PlayerOccupiedCell;

        if (trackedCell != null)
        {
            var (trackedX, trackedZ) = gridManager.GetCellGridPosition(trackedCell);
            if (trackedX == playerGrid.x && trackedZ == playerGrid.y)
            {
                return trackedCell;
            }
        }

        int playerLayer = trackedCell != null ? gridManager.GetCellLayer(trackedCell) : 1;
        return gridManager.GetCellManagerAt(playerGrid.x, playerGrid.y, playerLayer)
            ?? gridManager.GetCellManagerAt(playerGrid.x, playerGrid.y);
    }

    private static Vector3 GetCellFocusPoint(CellManager cell)
    {
        Collider cellCollider = cell.GetComponentInChildren<Collider>();
        if (cellCollider != null)
        {
            Bounds bounds = cellCollider.bounds;
            return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        }

        return cell.transform.position;
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
        isCounterStageActive = false;
        focusTween?.Kill();
        focusTween = null;
        UpdateAbandonButton(false);
    }

    private void FinishCounterStage()
    {
        isCounterStageActive = false;
        focusTween?.Kill();
        focusTween = null;
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
