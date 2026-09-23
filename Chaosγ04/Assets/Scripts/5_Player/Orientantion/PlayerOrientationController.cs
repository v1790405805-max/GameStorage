using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 长按玩家角色 → 显示 4 个操作 Button；常态隐藏；点击其他任意处（格子 / 其它 UI）→ 隐藏。
/// 优化：长按触发按钮时，会主动通知 PlayerMoveController 清除当前显示的移动范围。
/// </summary>
public class PlayerOrientationController : MonoBehaviour
{
    [Header("视觉管理器引用")]
    [SerializeField] private GridVisualManager visualManager;

    [Header("角色移动控制器引")]
    [SerializeField] private PlayerMoveController playerMoveController;

    [Header("角色动画控制器引用")]
    [SerializeField] private Animator characterAnimator;

    [Header("朝向按钮（运行时默认隐藏）")]
    [SerializeField] private Button buttonFront;
    [SerializeField] private Button buttonBack;
    [SerializeField] private Button buttonLeft;
    [SerializeField] private Button buttonRight;

    [Header("玩家朝向样式")]
    [Tooltip("长按显示朝向按钮时，角色周围格子使用的专属高亮样式资产（GridStyleData）")]
    [SerializeField] private GridStyleData playerOrientationStyle;

    [Header("长按用时参数")]
    [Tooltip("按住玩家角色达到该时长（秒）后显示操作按钮")]
    [Min(0.1f)]
    [SerializeField] private float longPressDuration = 0.5f;

    [Header("方向按钮隐藏")]
    [Tooltip("点击方向按钮后，按钮保持显示并停止响应的时间（秒）")]
    [Min(0f)]
    [SerializeField] private float hideButtonDelay = 1f;

    [Header("点击射线检测")]
    [Tooltip("点击射线 LayerMask，默认自动使用 GridManager.cellLayer（格子层）")]
    [SerializeField] private LayerMask clickLayerMask = 0;

    private bool buttonsVisible = false;
    private float pressStartTime = -1f;
    private bool longPressTriggered = false;
    private bool directionButtonInputLocked = false;

    private PointerEventData cachedPointerEventData;
    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();

    private static readonly int HorizontalHash = Animator.StringToHash("Horizontal");
    private static readonly int VerticalHash = Animator.StringToHash("Vertical");

    /// <summary>供 PlayerMoveController 查询长按阈值与按钮显示状态</summary>
    public static PlayerOrientationController Instance { get; private set; }

    public bool ButtonsVisible => buttonsVisible;

    /// <summary>区分“短按点击”与“长按”的时长（秒）</summary>
    public float LongPressDuration => longPressDuration;

    private void Awake()
    {
        Instance = this;
        if (visualManager == null)
            visualManager = FindFirstObjectByType<GridVisualManager>();

        // 自动查找 PlayerMoveController
        if (playerMoveController == null)
            playerMoveController = FindFirstObjectByType<PlayerMoveController>();

        // 自动查找 Animator（若未手选，优先从移动控制器获取或全局查找）
        if (characterAnimator == null && playerMoveController != null)
            characterAnimator = playerMoveController.GetComponentInChildren<Animator>();
        if (characterAnimator == null)
            characterAnimator = FindFirstObjectByType<Animator>();

        if (clickLayerMask.value == 0 && visualManager != null && visualManager.GridManager != null)
            clickLayerMask = 1 << visualManager.GridManager.cellLayer;

        RegisterButtonEvents();
    }

    private void RegisterButtonEvents()
    {
        // Front: Horizontal = 1, Vertical = 1
        if (buttonFront != null)
            buttonFront.onClick.AddListener(() => HandleDirectionButtonClicked(1f, 1f));

        // Right: Horizontal = 1, Vertical = -1
        if (buttonRight != null)
            buttonRight.onClick.AddListener(() => HandleDirectionButtonClicked(1f, -1f));

        // Back: Horizontal = -1, Vertical = -1
        if (buttonBack != null)
            buttonBack.onClick.AddListener(() => HandleDirectionButtonClicked(-1f, -1f));

        // Left: Horizontal = -1, Vertical = 1
        if (buttonLeft != null)
            buttonLeft.onClick.AddListener(() => HandleDirectionButtonClicked(-1f, 1f));
    }

    private void HandleDirectionButtonClicked(float horizontal, float vertical)
    {
        if (directionButtonInputLocked) return;

        directionButtonInputLocked = true;
        SetDirectionParameters(horizontal, vertical);
        StartCoroutine(HideButtonsAfterDelay());
    }

    private IEnumerator HideButtonsAfterDelay()
    {
        yield return new WaitForSeconds(hideButtonDelay);
        HideButtons();
    }

    private void SetDirectionParameters(float horizontal, float vertical)
    {
        if (characterAnimator != null)
        {
            characterAnimator.SetFloat(HorizontalHash, horizontal);
            characterAnimator.SetFloat(VerticalHash, vertical);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (buttonFront != null) buttonFront.onClick.RemoveAllListeners();
        if (buttonBack != null) buttonBack.onClick.RemoveAllListeners();
        if (buttonLeft != null) buttonLeft.onClick.RemoveAllListeners();
        if (buttonRight != null) buttonRight.onClick.RemoveAllListeners();
    }

    private void Start()
    {
        // 常态隐藏：运行时统一收起 4 个按钮，保证进入战斗时不会残留显示
        HideButtons();
    }

    private void Update()
    {
        // 非玩家回合：与 PlayerMoveController 行为一致，直接收起按钮
        if (TurnManager.Instance != null && TurnManager.Instance.CurrentTurn != TurnState.Player)
        {
            HideButtons();
            pressStartTime = -1f;
            return;
        }

        if (Camera.main == null || Mouse.current == null || visualManager == null || visualManager.GridManager == null)
            return;

        // 仅在鼠标按键相关的帧做处理，避免每帧多余开销
        bool leftDown = Mouse.current.leftButton.isPressed;
        if (!leftDown && !Mouse.current.leftButton.wasPressedThisFrame && !Mouse.current.leftButton.wasReleasedThisFrame)
            return;

        bool overAnyUI, overOurButtons;
        RaycastUI(out overAnyUI, out overOurButtons);

        if (overAnyUI)
        {
            // 点在自己的按钮上：不做任何处理（交给 Button 的 onClick）
            // 点在其它 UI（手牌、结束回合等）：视为“点击其他地方”，收起按钮
            if (!overOurButtons)
                HideButtons();
            pressStartTime = -1f;
            return;
        }

        Vector2 mousePosition = Mouse.current.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(mousePosition);
        bool hasHit = Physics.Raycast(ray, out RaycastHit hit, 1000f, clickLayerMask.value);
        CellManager cellUnderMouse = hasHit && hit.collider != null
            ? hit.collider.GetComponentInParent<CellManager>()
            : null;

        bool onPlayerCell = cellUnderMouse != null && visualManager.IsPlayerOnCell(cellUnderMouse);

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (onPlayerCell)
            {
                // 在角色上按下：开始长按计时
                pressStartTime = Time.time;
                longPressTriggered = false;
            }
            else
            {
                // 点击其他地方：收起按钮（移动范围仍由 PlayerMoveController 清除）
                HideButtons();
                pressStartTime = -1f;
            }
        }

        // 按住过程中：持续停留在角色格上达到时长 → 显示按钮（每次按下只触发一次）
        if (leftDown && pressStartTime >= 0f && !longPressTriggered)
        {
            if (!onPlayerCell)
            {
                // 按住期间滑出了角色所在格，取消本次长按
                pressStartTime = -1f;
            }
            else if (Time.time - pressStartTime >= longPressDuration)
            {
                ShowButtons();
                longPressTriggered = true;
            }
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
            pressStartTime = -1f;
    }

    /// <summary>
    /// 用 EventSystem 做 UI 命中检测，区分“自己的按钮”与“其它 UI”
    /// </summary>
    private void RaycastUI(out bool overAnyUI, out bool overOurButtons)
    {
        overAnyUI = false;
        overOurButtons = false;
        if (EventSystem.current == null) return;

        if (cachedPointerEventData == null)
            cachedPointerEventData = new PointerEventData(EventSystem.current);
        cachedPointerEventData.position = Mouse.current.position.ReadValue();

        uiRaycastResults.Clear();
        EventSystem.current.RaycastAll(cachedPointerEventData, uiRaycastResults);

        for (int i = 0; i < uiRaycastResults.Count; i++)
        {
            GameObject go = uiRaycastResults[i].gameObject;
            if (go == null) continue;
            overAnyUI = true;

            // 是否命中我们管理的按钮（含按钮的子物体，如 Image / Text）
            Transform t = go.transform;
            while (t != null)
            {
                if (IsOurButtonTransform(t))
                {
                    overOurButtons = true;
                    return;
                }
                t = t.parent;
            }
        }
    }

    /// <summary>
    /// 检查指定 Transform 是否为 4 个方向按钮之一
    /// </summary>
    private bool IsOurButtonTransform(Transform t)
    {
        if (buttonFront != null && t == buttonFront.transform) return true;
        if (buttonBack != null && t == buttonBack.transform) return true;
        if (buttonLeft != null && t == buttonLeft.transform) return true;
        if (buttonRight != null && t == buttonRight.transform) return true;
        return false;
    }

    public void ShowButtons()
    {
        // 长按唤出朝向按钮时，主动清除 PlayerMoveController 的移动范围与路径指示，
        // 确保渲染范围、光标 hover 上下文以及路径 LineRenderer 均彻底清空
        if (playerMoveController != null)
            playerMoveController.ClearClickedGrid();

        // 同步 Inspector 中 GridVisualManager 的“当前激活样式”只读显示
        if (visualManager != null && playerOrientationStyle != null)
            visualManager.ApplyStyle(playerOrientationStyle);

        // 朝向状态下渲染专属格子样式（玩家朝向样式，点状只涂玩家格）
        ApplyOrientationStyle();

        SetButtonsActive(true);
        buttonsVisible = true;
    }

    public void HideButtons()
    {
        bool wasVisible = buttonsVisible;
        SetButtonsActive(false);
        buttonsVisible = false;
        directionButtonInputLocked = false;

        // 确实从显示状态退出时才还原格子样式（避免非玩家回合每帧调用导致无谓重置）
        if (wasVisible && visualManager != null)
            visualManager.ResetAllCellsVisuals();
    }

    private void SetButtonsActive(bool active)
    {
        if (buttonFront != null) buttonFront.gameObject.SetActive(active);
        if (buttonBack != null) buttonBack.gameObject.SetActive(active);
        if (buttonLeft != null) buttonLeft.gameObject.SetActive(active);
        if (buttonRight != null) buttonRight.gameObject.SetActive(active);
    }

    private void LateUpdate()
    {
        // 朝向状态激活期间，每帧末尾补涂玩家格样式：
        // GridHoverController 无交互上下文时，会用通用悬停色覆盖悬停格，并在鼠标移开后还原为普通色；
        // 这里在每帧 LateUpdate 重新涂色，保证朝向样式不被悬停逻辑覆盖消失（全部 Update 先于 LateUpdate 执行）。
        if (buttonsVisible)
            ApplyOrientationStyle();
    }

    /// <summary>
    /// 用“玩家朝向样式”点状高亮玩家自身所在格（参考卡牌 RangeType=Point 的逻辑）：
    /// 仅玩家格使用样式资产中的玩家格颜色/边线色，不填充范围、不画外边框。
    /// </summary>
    private void ApplyOrientationStyle()
    {
        if (playerOrientationStyle == null || visualManager == null || visualManager.GridManager == null)
            return;

        CellManager playerCell = GetPlayerCell();
        if (playerCell == null) return;

        // 点状渲染：只涂玩家格本身（Point 类型下非玩家格不参与高亮）
        playerCell.SetCellColor(playerOrientationStyle.playerCellColor, Application.isPlaying);
        playerCell.SetLineColor(playerOrientationStyle.playerLineColor);
    }

    private CellManager GetPlayerCell()
    {
        if (playerMoveController == null || visualManager == null || visualManager.GridManager == null)
            return null;

        Vector2Int pos = playerMoveController.PlayerGridPos;
        if (!visualManager.GridManager.IsValidGridPosition(pos.x, pos.y))
            return null;

        return visualManager.GridManager.GetCellManagerAt(pos.x, pos.y);
    }
}
