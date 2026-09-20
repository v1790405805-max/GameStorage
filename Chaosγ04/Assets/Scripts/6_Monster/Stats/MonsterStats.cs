using System;
using UnityEngine;

public class MonsterStats : MonoBehaviour, IDamageable
{
    [Header("怪物数值")]
    public int maxHp = 20;
    public int currentHp;
    public int currentBlock = 0;

    // 怪物死亡事件（供战斗结算器监听）
    public static event Action OnAnyMonsterDied;

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

            // 累加真实造成的伤害
            if (RunDataManager.Instance != null)
            {
                RunDataManager.Instance.totalDamageDealt += remainingDamage;
            }

            // 受击后实时刷新血条 UI
            GetComponentInChildren<MonsterInfoUI>()?.UpdateHpDisplay(currentHp, maxHp);
        }

        // 3. 死亡判定
        if (currentHp <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log($"[{gameObject.name}] 死亡！");

        // 累加真实击杀数
        if (RunDataManager.Instance != null)
        {
            RunDataManager.Instance.totalKills += 1;
        }

        // 触发死亡事件
        OnAnyMonsterDied?.Invoke();

        // 不要直接 Destroy，改为隐藏物体，支持 SL 读档恢复
        gameObject.SetActive(false);
    }
}