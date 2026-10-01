using System;
using UnityEngine;

/// <summary>
/// 所有怪物动作脚本的基类
/// </summary>
public abstract class MonsterActionBase : MonoBehaviour
{
    [Header("Action Buffer")]
    [Tooltip("动作正确执行完成后，等待该时间再触发下一个动作；序列中的最后一个动作不会等待。")]
    [SerializeField, Min(0f)] private float postActionDelay = 0.5f;

    public event Action OnActionCompleted;

    public bool IsRunning { get; private set; }
    public float PostActionDelay => postActionDelay;

    public virtual bool CanExecute(MonsterActionContext context)
    {
        return true;
    }

    public virtual void OnSkipped()
    {
    }

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
