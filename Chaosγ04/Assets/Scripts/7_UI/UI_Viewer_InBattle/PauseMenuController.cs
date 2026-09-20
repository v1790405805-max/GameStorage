using UnityEngine;
using UnityEngine.UI; // 引入 UI 命名空间以操作 Button
using UnityEngine.SceneManagement;
using DG.Tweening;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PauseMenuController : MonoBehaviour
{
    [Header("--- UI 弹窗引用 ---")]
    [Tooltip("请确保挂载的这个 PausePanel 身上带有 Canvas Group 组件")]
    [SerializeField] private GameObject pausePanel;

    [Header("--- 按钮引用 ---")]
    [Tooltip("保存并退出按钮（用于在移动时禁用）")]
    [SerializeField] private Button saveAndExitButton;

    [Header("--- 场景配置 ---")]
    [SerializeField] private string mainMenuSceneName = "0_MainMenu_Scene";

    private CanvasGroup canvasGroup;
    private bool isPaused = false;

    private void Awake()
    {
        if (pausePanel != null)
        {
            canvasGroup = pausePanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = pausePanel.AddComponent<CanvasGroup>();
            }

            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            pausePanel.SetActive(false);
        }
    }

    private void Update()
    {
        bool isEscPressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            isEscPressed = true;
        }
#else
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            isEscPressed = true;
        }
#endif

        if (isEscPressed)
        {
            TogglePauseMenu();
        }
    }

    /// <summary>
    /// 检测场景中的角色或单位是否正在移动
    /// </summary>
    private bool IsUnitMoving()
    {
        // 检测玩家是否在移动
        PlayerMoveController moveManager = FindFirstObjectByType<PlayerMoveController>();
        if (moveManager != null && moveManager.IsMoving)
        {
            return true;
        }

        // 检测是否有怪物在移动
        MonsterMoveAction[] monsters = FindObjectsByType<MonsterMoveAction>(FindObjectsSortMode.None);
        foreach (var monster in monsters)
        {
            if (monster.MonsterIsMoving) return true;
        }

        return false;
    }

    /// <summary>
    /// 【手动绑定】开关弹窗（绑定给 TopBar 上的 HomeButton）
    /// </summary>
    public void TogglePauseMenu()
    {
        if (pausePanel == null || canvasGroup == null) return;

        isPaused = !isPaused;

        if (isPaused)
        {
            pausePanel.SetActive(true);
            Time.timeScale = 0f;

            // 【关键锁】在打开弹窗时，如果角色正在移动，置灰“保存并退出”按钮
            if (saveAndExitButton != null)
            {
                bool moving = IsUnitMoving();
                saveAndExitButton.interactable = !moving;
                if (moving)
                {
                    Debug.LogWarning("[PauseMenu] 监测到单位移动中，已禁用保存并退出按钮。");
                }
            }

            pausePanel.transform.localScale = Vector3.one * 0.8f;
            canvasGroup.alpha = 0f;

            canvasGroup.DOFade(1f, 0.25f).SetUpdate(true);
            pausePanel.transform.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutBack).SetUpdate(true);

            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
        else
        {
            Time.timeScale = 1f;

            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            canvasGroup.DOFade(0f, 0.2f).SetUpdate(true);
            pausePanel.transform.DOScale(Vector3.one * 0.8f, 0.2f).SetEase(Ease.InBack).SetUpdate(true)
                .OnComplete(() =>
                {
                    pausePanel.SetActive(false);
                });
        }
    }

    public void OnResumeButtonPressed()
    {
        if (isPaused)
        {
            TogglePauseMenu();
        }
    }

    public void OnAbandonButtonPressed()
    {
        Time.timeScale = 1f;

        // 1. 清理战斗管理器的残留数据
        if (CombatStatsManager.Instance != null)
        {
            CombatStatsManager.Instance.hasSavedGame = false;
            CombatStatsManager.Instance.savedMonsters.Clear();
            CombatStatsManager.Instance.savedHand.Clear();
            CombatStatsManager.Instance.savedDrawPile.Clear();
            CombatStatsManager.Instance.savedDiscardPile.Clear();
            CombatStatsManager.Instance.savedExhaustPile.Clear();
            CombatStatsManager.Instance.savedTurnStartSnapshot = null;
            CombatStatsManager.Instance.savedHorizontal = 0f;
            CombatStatsManager.Instance.savedVertical = 0f;
        }

        // =====================================================================
        // 【核心修复 1】清理全局数据管理器的大地图进度与历史数据！
        // 这样回到主菜单再开新局时，大地图就会重新生成并重置到起点。
        // =====================================================================
        if (RunDataManager.Instance != null)
        {
            RunDataManager.Instance.ResetRunData();
        }

        Debug.Log("[PauseMenu] 玩家选择放弃游戏，已清空战斗存档与大地图进度，并返回主菜单。");
        SceneManager.LoadScene(mainMenuSceneName);
    }

    /// <summary>
    /// 【手动绑定】保存并退出（绑定给 保存并退出 按钮）
    /// </summary>
    public void OnSaveAndExitButtonPressed()
    {
        // 【双重校验】防错拦截
        if (IsUnitMoving())
        {
            Debug.LogWarning("[PauseMenu] 角色或单位正在移动中，无法进行保存退出！");
            return;
        }

        Time.timeScale = 1f;

        if (CombatStatsManager.Instance != null)
        {
            CombatStatsManager.Instance.SaveCombatAndExit(mainMenuSceneName);
        }
        else
        {
            Debug.LogError("[PauseMenu] 未找到 CombatStatsManager 实例，强行返回主菜单。");
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
    }
}