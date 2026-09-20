using System.Collections;
using UnityEngine;

/// <summary>
/// 撤离点 / 到达即胜利地块
/// 将此脚本挂载到地图上的特定撤离点格子（包含 CellManager）或与其重合的物体上
/// </summary>
public class EscapePoint : MonoBehaviour
{
    [Header("结算等待设置")]
    [Tooltip("玩家彻底停下脚步后，延迟触发胜利结算的时间（秒），留出停步动画或视觉缓冲期")]
    [Min(0f)] public float settlementDelay = 0.5f;

    private PlayerMoveController playerController;
    private CellManager escapeCell;

    private bool hasTriggered = false;
    private bool isWaitingForMoveComplete = false;
    private bool isCountingDown = false; // 是否处于延迟等待阶段

    private void Start()
    {
        // 1. 获取撤离点自身的 CellManager 引用（挂在同一个物体上，或者从父级获取）
        escapeCell = GetComponent<CellManager>();
        if (escapeCell == null)
        {
            escapeCell = GetComponentInParent<CellManager>();
        }

        // 2. 获取场景中的 PlayerMoveController
        playerController = FindFirstObjectByType<PlayerMoveController>();

        if (playerController == null)
        {
            Debug.LogWarning("[EscapePoint] 场景中未找到 PlayerMoveController，撤离点无法工作！");
        }

        if (escapeCell == null)
        {
            Debug.LogWarning($"[EscapePoint] {gameObject.name} 上未检测到 CellManager 组件，将回退至位置比对逻辑。");
        }
    }

    private void Update()
    {
        // 已经触发、正处于倒计时、或找不到玩家控制器时，直接跳过 Update 检测
        if (hasTriggered || isCountingDown || playerController == null) return;

        // 阶段 1：当检测到玩家正在移动时，判断玩家移动的目标点/当前位置是否踩到了撤离点
        if (!isWaitingForMoveComplete)
        {
            if (playerController.IsMoving && IsPlayerOnEscapePoint())
            {
                // 玩家踏入了撤离点，标记“准备结算”，等待移动和动画彻底结束
                isWaitingForMoveComplete = true;
            }
        }
        // 阶段 2：如果已经踏入撤离点，等待玩家真正停止移动（协程结束、 Run 动画结束）
        else
        {
            // 玩家移动尚未结束，继续等待
            if (playerController.IsMoving) return;

            // 玩家移动已彻底结束！二次确认玩家最终落点是否确实在撤离点上
            if (IsPlayerOnEscapePoint())
            {
                // 启动带有延迟等待的结算协程
                StartCoroutine(VictorySequenceRoutine());
            }
            else
            {
                // 如果玩家只是路过穿过撤离点，最终落点不在撤离点上，取消等待
                isWaitingForMoveComplete = false;
            }
        }
    }

    /// <summary>
    /// 判断玩家当前位置是否与撤离点重合
    /// </summary>
    private bool IsPlayerOnEscapePoint()
    {
        Vector3 playerPos = playerController.transform.position;
        Vector3 pointPos = transform.position;

        playerPos.y = 0;
        pointPos.y = 0;

        // 容差设定为 0.15f，确保移动完全停止在中心点附近才认定踩中
        return Vector3.Distance(playerPos, pointPos) <= 0.15f;
    }

    /// <summary>
    /// 胜利结算延迟协程
    /// </summary>
    private IEnumerator VictorySequenceRoutine()
    {
        isCountingDown = true;

        // 如果设置了等待时间，执行等待
        if (settlementDelay > 0f)
        {
            yield return new WaitForSeconds(settlementDelay);
        }

        // 再次确认玩家没有在等待期间被移走（增强稳健性）
        if (IsPlayerOnEscapePoint())
        {
            TriggerEscapeVictory();
        }
        else
        {
            isWaitingForMoveComplete = false;
            isCountingDown = false;
        }
    }

    private void TriggerEscapeVictory()
    {
        hasTriggered = true;
        isCountingDown = false;
        isWaitingForMoveComplete = false;

        Debug.Log($"[EscapePoint] 玩家彻底停步并等待了 {settlementDelay} 秒，成功于撤离点：{gameObject.name} 触发战斗胜利！");

        if (BattleConditionManager.Instance != null)
        {
            BattleConditionManager.Instance.TriggerVictory();
        }
    }
}