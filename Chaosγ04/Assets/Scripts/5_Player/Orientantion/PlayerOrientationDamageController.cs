using UnityEngine;

/// <summary>
/// 玩家朝向受击判定与伤害减免结算：攻击者所在格与玩家所在格的 XZ 网格方向 == 玩家朝向的网格方向时，视为正面接敌，
/// 受到的伤害减免 damageReduction。
/// 判定完全基于地图网格坐标系统（GridManager 的 XZ 坐标），不依赖世界坐标 Position：
/// - 攻击方向：攻击者所在格 XZ − 玩家所在格 XZ（怪物攻击必为正交相邻单格，方向为标准化 ±1/0）
/// - 玩家朝向：CombatStatsManager 记录的 Horizontal/Vertical（Animator 参数，标准化 ±1）
/// 换算为网格方向：Horizontal/Vertical → ( (H+V)/2, (H−V)/2 )，即
/// (1,1)→+x，(-1,-1)→-x，(1,-1)→+z，(-1,1)→-z
/// 所有参数均为标准化整数，正面判定采用精确相等比较，无需角度/点积计算。
///
/// 【职责边界】本类是"伤害减免运算"的唯一入口：任何攻击要对玩家结算伤害，
/// 都应调用本类的 ResolveMonsterAttack（而不是直接调用
/// CombatStatsManager.TakeDamage），由本类完成方位判定、减免倍率计算、
/// 最终伤害取整，再转交 CombatStatsManager 做纯数值扣减。
///
/// 使用方式：把本组件挂到场景任意物体上（建议与 PlayerOrientationController 同物体）。
/// </summary>
public class PlayerOrientationDamageController : MonoBehaviour
{
    public static PlayerOrientationDamageController Instance { get; private set; }

    [Header("引用（留空则自动查找）")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private PlayerMoveController playerMoveController;

    [Header("伤害减免比例")]
    [Tooltip("正面接敌时伤害减免比例（0.2 = 减免 20%），最终伤害向上取整")]
    [Range(0f, 0.9f)]
    [SerializeField] private float damageReduction = 0.2f;

    private CombatStatsManager stats;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();
        if (playerMoveController == null)
            playerMoveController = FindFirstObjectByType<PlayerMoveController>();

        stats = CombatStatsManager.Instance;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// 【伤害减免运算的唯一入口】接收某次怪物攻击的原始（未减免）伤害数值，
    /// 以及攻击者所在的格坐标：
    /// 1. 判断攻击者相对玩家所在格的方向
    /// 2. 结合玩家当前朝向判断是否正面接敌
    /// 3. 计算减免后的最终伤害（向上取整）
    /// 4. 调用 CombatStatsManager.TakeDamage 完成纯数值结算
    ///
    /// attackerGrid 为 null 时（例如无法获取攻击者格坐标的旧调用/自伤场景），
    /// 不做任何减免，直接按原始伤害结算。
    /// </summary>
    public void ResolveMonsterAttack(int rawDamage, Vector2Int? attackerGrid)
    {
        int finalDamage = rawDamage;

        if (attackerGrid.HasValue)
        {
            finalDamage = Mathf.CeilToInt(rawDamage * GetDamageMultiplier(attackerGrid.Value));
        }

        if (stats == null)
            stats = CombatStatsManager.Instance;

        if (stats != null)
        {
            stats.TakeDamage(finalDamage);
        }
    }

    /// <summary>攻击者所在格是否位于玩家朝向的正前方（网格方向精确相等）。</summary>
    public bool IsAttackFromFront(Vector2Int attackerGrid)
    {
        Vector2Int attackDir = GetAttackDirectionGrid(attackerGrid);
        Vector2Int facing = GetPlayerFacingGrid();

        if (attackDir == Vector2Int.zero || facing == Vector2Int.zero) return false;

        return attackDir == facing;
    }

    /// <summary>根据受击网格方向返回伤害倍率：正面 = 1 - damageReduction，其余 = 1。</summary>
    public float GetDamageMultiplier(Vector2Int attackerGrid)
    {
        return IsAttackFromFront(attackerGrid) ? (1f - damageReduction) : 1f;
    }

    /// <summary>攻击者相对玩家的网格方向（标准化 ±1/0）；非正交相邻或数据缺失时返回 zero。</summary>
    private Vector2Int GetAttackDirectionGrid(Vector2Int attackerGrid)
    {
        if (playerMoveController == null)
            playerMoveController = FindFirstObjectByType<PlayerMoveController>();
        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();

        if (playerMoveController == null || gridManager == null) return Vector2Int.zero;

        Vector2Int playerGrid = playerMoveController.PlayerGridPos;
        if (!gridManager.IsValidGridPosition(playerGrid.x, playerGrid.y)) return Vector2Int.zero;

        Vector2Int delta = attackerGrid - playerGrid;

        // 仅接受正交相邻单格（曼哈顿距离 = 1），同格或非相邻均视为非正面
        if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) != 1) return Vector2Int.zero;

        return delta;
    }

    /// <summary>
    /// 将 CombatStatsManager 记录的 (Horizontal, Vertical) 朝向换算为网格 XZ 方向（标准化整数）。
    /// 映射：(1,1)→+x，(-1,-1)→-x，(1,-1)→+z，(-1,1)→-z。
    ///
    /// CombatStatsManager 每帧都会自动从 Animator 同步 Horizontal/Vertical，
    /// 因此这里读到的始终是玩家当前的实时朝向，无需在此处额外触发同步。
    /// </summary>
    private Vector2Int GetPlayerFacingGrid()
    {
        if (stats == null)
            stats = CombatStatsManager.Instance;
        if (stats == null) return Vector2Int.zero;

        int h = Mathf.RoundToInt(stats.Horizontal);
        int v = Mathf.RoundToInt(stats.Vertical);

        if (h == 0 && v == 0) return Vector2Int.zero;

        return new Vector2Int((h + v) / 2, (h - v) / 2);
    }
}