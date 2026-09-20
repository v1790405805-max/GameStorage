using System.Collections;
using System.Linq;
using UnityEngine;

/// <summary>
/// 怪物攻击动作：检测是否与 Player 水平/垂直相邻，若相邻则先播放攻击动画，播放完毕后再进行数值结算
///
/// 【职责边界】本类只负责：
/// 1) 判断是否满足发起攻击的条件（相邻、视野等）
/// 2) 播放攻击动画
/// 3) 把"攻击"这一动作及其初始伤害数值（attackDamage）连同攻击者所在格坐标
///    交给 PlayerOrientationDamageController 去处理
/// 本类完全不参与伤害减免的计算，也不直接决定玩家最终掉多少血。
/// </summary>
public class MonsterAttackAction : MonsterActionBase
{
    [Header("网格管理器引用")]
    public GridManager gridManager;

    [Header("Animator引用")]
    public Animator monsterAnimator;

    [Header("动画参数配置")]
    [Tooltip("触发攻击的 Animator Bool 变量名")]
    public string attackBoolName = "Attack";
    [Tooltip("动画保持开启及播放的总时长（秒）")]
    public float attackDuration = 2f / 3f; // 2/3 秒

    [Header("怪物数值配置")]
    [Tooltip("怪物攻击力（未经任何减免的初始数值）")]
    public int attackDamage = 5;

    private Coroutine attackCoroutine;
    private MonsterIdentityManager selfIdentity;

    private void Awake()
    {
        selfIdentity = GetComponent<MonsterIdentityManager>();
    }

    protected override void OnStart()
    {
        // 1. 确保 GridManager 存在
        if (gridManager == null)
        {
            gridManager = FindFirstObjectByType<GridManager>();
        }
        if (gridManager == null)
        {
            Debug.LogError($"[{name}] MonsterAttackAction 无法找到场景中的 GridManager！");
            CompleteAction();
            return;
        }
        gridManager.EnsureGridSystemInitialized();

        // 2. 获取自身 (Monster) 所在的网格坐标 (X, Z)
        (int monsterX, int monsterZ) = gridManager.GetGridPosition(transform.position);

        // 检查自身是否处于有效网格内
        if (!gridManager.IsValidGridPosition(monsterX, monsterZ))
        {
            Debug.LogWarning($"[{name}] 怪物当前位置不在有效的网格范围内！");
            CompleteAction();
            return;
        }

        // 3. 寻找场景中的 Player 及其坐标
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null)
        {
            Debug.LogWarning($"[{name}] 场景中未找到 Tag 为 'Player' 的物体！");
            CompleteAction();
            return;
        }
        (int playerX, int playerZ) = gridManager.GetGridPosition(playerObj.transform.position);

        // 3.5 可隐蔽片视野判定：怪物所在隐蔽片若不包含玩家所在格，则丢失视野，跳过攻击
        CellManager monsterCell = FindMonsterCellInColumn(monsterX, monsterZ);
        if (!ConcealmentCell.CanMonsterSeePlayer(monsterCell))
        {
            CompleteAction();
            return;
        }

        // 4. 判定是否为水平或垂直相邻（曼哈顿距离为 1）
        bool isAdjacent = IsOrthogonallyAdjacent(monsterX, monsterZ, playerX, playerZ);

        if (isAdjacent)
        {
            // 满足攻击条件，启动攻击协程
            attackCoroutine = StartCoroutine(PerformAttackRoutine());
        }
        else
        {
            // 不满足攻击条件，直接结束该动作
            CompleteAction();
        }
    }

    /// <summary>
    /// 在怪物所在列 (x, z) 的各层格子中定位怪物实际所在的格子。
    /// 依赖 CellManager 的 Trigger 登记（与 MonsterMoveAction.TryGetMonsterCell 同方式）。
    /// </summary>
    private CellManager FindMonsterCellInColumn(int x, int z)
    {
        foreach (CellManager cell in gridManager.GetCellManagersInColumn(x, z))
        {
            if (cell != null && cell.GetMonstersInside().Contains(selfIdentity))
            {
                return cell;
            }
        }
        return null;
    }

    /// <summary>
    /// 判断两个格子坐标是否在水平或垂直方向上紧邻（不包含斜向与自身重合）
    /// </summary>
    private bool IsOrthogonallyAdjacent(int x1, int z1, int x2, int z2)
    {
        int deltaX = Mathf.Abs(x1 - x2);
        int deltaZ = Mathf.Abs(z1 - z2);
        return (deltaX + deltaZ) == 1;
    }

    /// <summary>
    /// 执行攻击动作：先播放完整动画，再把攻击这一动作和初始伤害数值交出去结算
    /// </summary>
    private IEnumerator PerformAttackRoutine()
    {
        // 1. 开启攻击动画
        if (monsterAnimator != null)
        {
            monsterAnimator.SetBool(attackBoolName, true);
        }
        else
        {
            Debug.LogWarning($"[{name}] MonsterAnimator 未关联，仅等待时间后进行伤害计算。");
        }

        // 2. 等待动画播放完毕
        yield return new WaitForSeconds(attackDuration);

        // 3. 重置攻击动画 Bool 状态
        if (monsterAnimator != null)
        {
            monsterAnimator.SetBool(attackBoolName, false);
        }

        // 4.【发起攻击】只传出"攻击"这一动作的初始伤害数值（attackDamage）
        // 以及攻击者所在格坐标（用于朝向判定），具体减免多少、
        // 最终扣多少血完全不在本类关心范围内。
        if (gridManager != null)
        {
            gridManager.EnsureGridSystemInitialized();
            (int monsterX, int monsterZ) = gridManager.GetGridPosition(transform.position);

            if (PlayerOrientationDamageController.Instance != null)
            {
                PlayerOrientationDamageController.Instance.ResolveMonsterAttack(
                    attackDamage, new Vector2Int(monsterX, monsterZ));
            }
            else if (CombatStatsManager.Instance != null)
            {
                // 兜底：找不到朝向减免控制器时，按原始伤害直接结算
                CombatStatsManager.Instance.TakeDamage(attackDamage);
            }
        }
        else if (PlayerOrientationDamageController.Instance != null)
        {
            // 没有格坐标信息（理论上不会走到这里），交给减免控制器按无方位处理
            PlayerOrientationDamageController.Instance.ResolveMonsterAttack(attackDamage, null);
        }
        else if (CombatStatsManager.Instance != null)
        {
            CombatStatsManager.Instance.TakeDamage(attackDamage);
        }

        attackCoroutine = null;

        // 5. 标记当前 Action 执行完成
        CompleteAction();
    }

    public override void CancelAction()
    {
        base.CancelAction();
        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }
        // 被打断或取消时重置 Animator 变量
        if (monsterAnimator != null)
        {
            monsterAnimator.SetBool(attackBoolName, false);
        }
    }
}