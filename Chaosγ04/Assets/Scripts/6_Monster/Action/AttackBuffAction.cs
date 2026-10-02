using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 提升自身所属阵营中所有活跃怪物的攻击力。
/// 触发后会终止其他 Action，并进入两回合冷却。
/// </summary>
[DisallowMultipleComponent]
public sealed class AttackBuffAction : MonsterActionBase
{
    [Header("增益配置")]
    [Tooltip("每次触发时为阵营内所有活跃怪物增加的攻击力。")]
    [SerializeField, Min(1)] private int attackBonus = 2;

    [Header("冷却")]
    [Tooltip("触发后需要跳过的完整回合数；填写 2 表示触发回合后的两回合内不可再次触发。")]
    [SerializeField, Min(0)] private int cooldownRounds = 2;

    private MonsterIdentityManager selfIdentity;
    private int lastTriggeredRound = int.MinValue;

    public bool IsOnCooldown
    {
        get
        {
            if (cooldownRounds <= 0 || lastTriggeredRound == int.MinValue)
            {
                return false;
            }

            int currentRound = GetCurrentRound();
            return currentRound >= lastTriggeredRound &&
                   currentRound - lastTriggeredRound <= cooldownRounds;
        }
    }

    public override bool CanExecute(MonsterActionContext context)
    {
        ResolveReferences();

        if (selfIdentity == null || MonsterIdentitySystem.Instance == null)
        {
            return false;
        }

        return !IsOnCooldown;
    }

    protected override void OnStart()
    {
        ResolveReferences();

        if (selfIdentity == null)
        {
            Debug.LogError($"[{name}] AttackBuffAction 缺少 MonsterIdentityManager。");
            CompleteAction();
            return;
        }

        MonsterIdentitySystem monsterSystem = MonsterIdentitySystem.Instance;
        if (monsterSystem == null)
        {
            Debug.LogError($"[{name}] AttackBuffAction 找不到 MonsterIdentitySystem。");
            CompleteAction();
            return;
        }

        // 触发本 Action 时，立即停止其他 Action 及当前序列的后续动作。
        MonsterActionManager.RequestTerminationOfOtherActions(this);

        int affectedCount = ApplyFactionAttackBonus(monsterSystem);
        lastTriggeredRound = GetCurrentRound();

        Debug.Log(
            $"[{name}] 阵营 {selfIdentity.faction} 的攻击力提升 {attackBonus} 点，" +
            $"共影响 {affectedCount} 只怪物。");

        CompleteAction();
    }

    private int ApplyFactionAttackBonus(MonsterIdentitySystem monsterSystem)
    {
        List<MonsterIdentityManager> factionMonsters =
            monsterSystem.GetMonstersByFaction(selfIdentity.faction);

        int affectedCount = 0;
        foreach (MonsterIdentityManager monster in factionMonsters)
        {
            if (monster == null || !monster.gameObject.activeInHierarchy)
            {
                continue;
            }

            MonsterStats monsterStats = monster.GetComponent<MonsterStats>();
            if (monsterStats == null)
            {
                monsterStats = monster.GetComponentInChildren<MonsterStats>(true);
            }

            if (monsterStats == null)
            {
                continue;
            }

            monsterStats.attackDamage += attackBonus;
            affectedCount++;
        }

        return affectedCount;
    }

    private void ResolveReferences()
    {
        if (selfIdentity == null)
        {
            selfIdentity = GetComponent<MonsterIdentityManager>();
        }
    }

    private static int GetCurrentRound()
    {
        return TurnManager.Instance != null
            ? TurnManager.Instance.currentRoundCount
            : 0;
    }
}
