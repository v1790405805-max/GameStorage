using System;
using UnityEngine;

public class MonsterStats : MonoBehaviour, IDamageable
{
    [Header("生命数值")]
    public int maxHp = 20;
    public int currentHp;
    public int currentBlock = 0;

    [Header("战斗属性")]
    [Tooltip("怪物一回合最多可以移动的格子数。")]
    public int mobility = 3;

    [Tooltip("以分层格子图上的 A* 路径长度计算的攻击距离。")]
    public int attackRange = 1;

    [Tooltip("怪物攻击的基础伤害。")]
    public int attackDamage = 5;

    // 怪物死亡事件（供战斗结算器监听）
    public static event Action OnAnyMonsterDied;
    public static event Action<MonsterIdentityManager> PlayerAttackedMonster;

    private MonsterHateSystem hateSystem;

    private void Awake()
    {
        // 游戏一开始，立即给当前血量赋最大值
        currentHp = maxHp;
    }

    private void Start()
    {
        // 在 Start 里刷新一次头顶 UI 显示
        GetComponentInChildren<MonsterInfoUI>()?.UpdateHpDisplay(currentHp, maxHp);
    }

    public void TakeDamage(int damageAmount)
    {
        ApplyDamage(damageAmount, null, playerAttributed: true);
    }

    /// <summary>
    /// 怪物之间攻击时传入攻击者，便于仇恨系统记录“受到攻击”。
    /// 玩家攻击继续使用旧入口，攻击者按 Player 处理。
    /// </summary>
    public void TakeDamage(int damageAmount, MonsterIdentityManager attacker)
    {
        ApplyDamage(damageAmount, attacker, playerAttributed: attacker == null);
    }

    private void ApplyDamage(int damageAmount, MonsterIdentityManager attacker, bool playerAttributed)
    {
        int remainingDamage = damageAmount;

        // 1. 先扣护盾
        if (currentBlock > 0)
        {
            if (currentBlock >= remainingDamage)
            {
                currentBlock -= remainingDamage;
                remainingDamage = 0;
            }
            else
            {
                remainingDamage -= currentBlock;
                currentBlock = 0;
            }
        }

        // 2. 再扣血量
        if (remainingDamage > 0)
        {
            currentHp = Mathf.Max(0, currentHp - remainingDamage);
            Debug.Log($"[{gameObject.name}] 受到了 {remainingDamage} 点伤害，剩余HP: {currentHp}");

            // 只有玩家造成的伤害才计入玩家 RunData
            if (playerAttributed && RunDataManager.Instance != null)
            {
                RunDataManager.Instance.totalDamageDealt += remainingDamage;
            }

            // 受击后实时刷新血条 UI
            GetComponentInChildren<MonsterInfoUI>()?.UpdateHpDisplay(currentHp, maxHp);
        }

        RegisterHitHate(attacker);

        if (playerAttributed)
        {
            MonsterIdentityManager identity = GetComponent<MonsterIdentityManager>();
            if (identity != null)
            {
                PlayerAttackedMonster?.Invoke(identity);
            }
        }

        // 3. 死亡判定
        if (currentHp <= 0)
        {
            Die(playerAttributed);
        }
    }

    private void RegisterHitHate(MonsterIdentityManager attacker)
    {
        MonsterIdentityManager identity = GetComponent<MonsterIdentityManager>();
        if (identity == null)
        {
            return;
        }

        if (hateSystem == null)
        {
            hateSystem = MonsterHateSystem.EnsureOn(identity);
        }

        hateSystem?.RegisterHit(attacker);
    }

    private void Die(bool playerAttributed)
    {
        Debug.Log($"[{gameObject.name}] 死亡！");

        // 只有玩家造成的击杀才计入玩家 RunData
        if (playerAttributed && RunDataManager.Instance != null)
        {
            RunDataManager.Instance.totalKills += 1;
        }

        // 触发死亡事件
        OnAnyMonsterDied?.Invoke();

        // 不要直接 Destroy，改为隐藏物体，支持 SL 读档恢复
        gameObject.SetActive(false);
    }
}
