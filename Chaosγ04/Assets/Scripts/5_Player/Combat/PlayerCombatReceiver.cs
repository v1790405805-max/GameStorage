using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 挂载在 Player 物体上：接收动画关键帧事件 OnHit，将扣血与受击反馈严格同步到命中时刻
/// </summary>
public class PlayerCombatReceiver : MonoBehaviour
{
    public static PlayerCombatReceiver Instance { get; private set; }

    private readonly List<Action> pendingHitActions = new List<Action>();
    private Coroutine fallbackCoroutine;

    [Header("防卡死保底")]
    [Tooltip("若动画未配置 OnHit 关键帧，超时后将自动强制执行伤害结算，防止游戏卡死")]
    [SerializeField] private float fallbackTimeout = 0.4f;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 静态调用入口：将伤害行为挂起，等待动画命中
    /// </summary>
    public static void ExecuteOnHit(Action action)
    {
        if (action == null) return;

        if (Instance != null)
        {
            Instance.RegisterPendingAction(action);
        }
        else
        {
            // 场景无 PlayerCombatReceiver 时直接执行保底
            action.Invoke();
        }
    }

    public void RegisterPendingAction(Action action)
    {
        pendingHitActions.Add(action);

        // 启动/刷新保底倒计时
        if (fallbackCoroutine != null)
        {
            StopCoroutine(fallbackCoroutine);
        }
        fallbackCoroutine = StartCoroutine(FallbackRoutine());
    }

    /// <summary>
    /// 【核心动画事件】在 Unity Animation 窗口中刀刃砍中帧添加 Event，函数名填 OnHit
    /// </summary>
    public void OnHit()
    {
        TriggerPendingActions();
    }

    private void TriggerPendingActions()
    {
        if (fallbackCoroutine != null)
        {
            StopCoroutine(fallbackCoroutine);
            fallbackCoroutine = null;
        }

        if (pendingHitActions.Count > 0)
        {
            List<Action> actionsToExecute = new List<Action>(pendingHitActions);
            pendingHitActions.Clear();

            foreach (var act in actionsToExecute)
            {
                act?.Invoke();
            }
        }
    }

    private IEnumerator FallbackRoutine()
    {
        yield return new WaitForSeconds(fallbackTimeout);

        if (pendingHitActions.Count > 0)
        {
            Debug.LogWarning("[PlayerCombatReceiver] 超时未触发 OnHit 动画事件，已启动保底扣血！请检查 Player 当前攻击动画片段是否添加了 OnHit 关键帧。");
            TriggerPendingActions();
        }
    }
}