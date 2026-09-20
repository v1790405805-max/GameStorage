using System;
using UnityEngine;

/// <summary>
/// 所有怪物动作脚本的基类
/// </summary>
public abstract class MonsterActionBase : MonoBehaviour
{
    public event Action OnActionCompleted;

    public bool IsRunning { get; private set; }

    public void StartAction()
    {
        if (IsRunning)
        {
            Debug.LogWarning($"[{name}] {GetType().Name} 已在执行中，忽略重复启动。");
            return;
        }

        IsRunning = true;
        OnStart();
    }

    protected abstract void OnStart();

    protected void CompleteAction()
    {
        if (!IsRunning) return;
        IsRunning = false;
        OnActionCompleted?.Invoke();
    }

    public virtual void CancelAction()
    {
        IsRunning = false;
    }
}