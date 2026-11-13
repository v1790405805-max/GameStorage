using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// 敌方攻击命中前触发的反击时间状态。
/// 该状态覆盖在敌方回合之上，不改变 TurnManager 的回合类型。
/// </summary>
[DisallowMultipleComponent]
public class CounterTimeManager : MonoBehaviour
{
    public const string PlayerMaskLayerName = "CounterTimePlayerMask";
    public const string AttackerMaskLayerName = "CounterTimeAttackerMask";
    private const float CounterTimeChromaticAberrationIntensity = 0.35f;

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
    private Volume resolvedGlobalVolume;
    private ChromaticAberration counterChromaticAberration;
    private readonly Dictionary<GameObject, int> maskedRendererLayers =
        new Dictionary<GameObject, int>();

    public bool IsActive => state != CounterTimeState.Inactive;
    public bool IsCounterStageActive => isCounterStageActive;
    public static bool IsCounterTimeMaskActive { get; private set; }
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

    private void OnDisable()
    {
        EndCounterTimeMask();
        ResetCounterTimeChromaticAberration();
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
        focusTween?.Kill();
        focusTween = null;
        FinishCounterStage();
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
        MapInspector.Instance?.SetMapMode(false);
        ApplyCounterTimeChromaticAberration();
        BeginCounterTimeMask(attackAction);
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
        FinishCounterStage();
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
        focusTween?.Kill();
        focusTween = null;
        FinishCounterStage();
        UpdateAbandonButton(false);
    }

    private void FinishCounterStage()
    {
        isCounterStageActive = false;
        focusTween?.Kill();
        focusTween = null;
        EndCounterTimeMask();
        ResetCounterTimeChromaticAberration();

        if (TurnManager.Instance != null &&
            TurnManager.Instance.CurrentTurn == TurnState.Enemy)
        {
            MapInspector.Instance?.SetMapMode(true);
        }
    }

    private void ApplyCounterTimeChromaticAberration()
    {
        EnsureCounterTimeChromaticAberration();
        if (counterChromaticAberration == null)
        {
            return;
        }

        counterChromaticAberration.active = true;
        counterChromaticAberration.intensity.overrideState = true;
        counterChromaticAberration.intensity.value =
            CounterTimeChromaticAberrationIntensity;
    }

    private void ResetCounterTimeChromaticAberration()
    {
        if (counterChromaticAberration != null)
        {
            counterChromaticAberration.intensity.value = 0f;
        }
    }

    private void EnsureCounterTimeChromaticAberration()
    {
        if (counterChromaticAberration != null)
        {
            return;
        }

        if (resolvedGlobalVolume == null)
        {
            Volume[] volumes = FindObjectsByType<Volume>(FindObjectsSortMode.None);
            for (int i = 0; i < volumes.Length; i++)
            {
                Volume volume = volumes[i];
                if (volume != null && volume.isGlobal)
                {
                    resolvedGlobalVolume = volume;
                    break;
                }
            }
        }

        if (resolvedGlobalVolume == null)
        {
            Debug.LogWarning(
                "[CounterTimeManager] 未找到 Global Volume，无法切换色差效果。");
            return;
        }

        VolumeProfile runtimeProfile = resolvedGlobalVolume.profile;
        if (!runtimeProfile.TryGet(out counterChromaticAberration))
        {
            counterChromaticAberration =
                runtimeProfile.Add<ChromaticAberration>(true);
        }
    }

    private void BeginCounterTimeMask(MonsterAttackAction attackAction)
    {
        EndCounterTimeMask();

        int playerLayer = LayerMask.NameToLayer(PlayerMaskLayerName);
        int attackerLayer = LayerMask.NameToLayer(AttackerMaskLayerName);
        Transform playerRoot = ResolvePlayerMaskRoot();
        Transform attackerRoot = attackAction != null
            ? attackAction.monsterAnimator != null
                ? attackAction.monsterAnimator.transform
                : attackAction.transform
            : null;

        if (playerLayer < 0 ||
            attackerLayer < 0 ||
            playerRoot == null ||
            attackerRoot == null)
        {
            Debug.LogWarning(
                "[CounterTimeManager] 反击遮罩缺少专用 Layer 或角色根节点，视觉遮罩将跳过。");
            return;
        }

        int playerRendererCount =
            AssignMaskLayer(playerRoot, playerLayer);
        int attackerRendererCount =
            AssignMaskLayer(attackerRoot, attackerLayer);
        if (playerRendererCount == 0 || attackerRendererCount == 0)
        {
            Debug.LogWarning(
                "[CounterTimeManager] 反击遮罩在玩家或攻击者下未找到角色网格，视觉遮罩将跳过。");
            EndCounterTimeMask();
            return;
        }

        IsCounterTimeMaskActive = true;
    }

    private Transform ResolvePlayerMaskRoot()
    {
        ResolveFocusReferences();
        if (playerMoveController == null)
        {
            return null;
        }

        Transform playerTransform = playerMoveController.PlayerTransform != null
            ? playerMoveController.PlayerTransform
            : playerMoveController.transform;
        return playerTransform;
    }

    private int AssignMaskLayer(Transform root, int layer)
    {
        int assignedCount = 0;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (!(renderer is MeshRenderer) &&
                !(renderer is SkinnedMeshRenderer) &&
                !(renderer is SpriteRenderer))
            {
                continue;
            }

            if (string.Equals(
                    renderer.gameObject.name,
                    "shadow",
                    System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            GameObject target = renderer.gameObject;
            if (!maskedRendererLayers.ContainsKey(target))
            {
                maskedRendererLayers.Add(target, target.layer);
            }

            target.layer = layer;
            assignedCount++;
        }

        return assignedCount;
    }

    private void EndCounterTimeMask()
    {
        IsCounterTimeMaskActive = false;
        foreach (KeyValuePair<GameObject, int> pair in maskedRendererLayers)
        {
            if (pair.Key != null)
            {
                pair.Key.layer = pair.Value;
            }
        }

        maskedRendererLayers.Clear();
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
