using UnityEngine;

/// <summary>
/// ScienceFiction 5101 反击：反击阶段使用时伤害 +2，
/// 并使目标怪物的 AttackBuff -2。
/// </summary>
public sealed class IncreaseDamegeAndWeakEffect : CardEffectCore
{
    private const int CounterDamageBonus = 2;
    private const int TargetAttackBuffChange = -2;

    public override bool Execute(CardData card, Vector2Int targetGrid)
    {
        if (!IsCounterStageActive())
        {
            return true;
        }

        GridManager gridManager = Object.FindFirstObjectByType<GridManager>();
        if (gridManager == null)
        {
            Debug.LogWarning("[IncreaseDamegeAndWeakEffect] 找不到 GridManager，无法降低 AttackBuff。");
            return false;
        }

        MonsterIdentityManager target =
            PushEffect.FindMonsterAtGrid(gridManager, targetGrid);
        if (target == null)
        {
            Debug.LogWarning(
                $"[IncreaseDamegeAndWeakEffect] 目标格 {targetGrid} 没有可作用的怪物。");
            return false;
        }

        MonsterStats stats = target.GetComponentInChildren<MonsterStats>();
        if (stats == null)
        {
            Debug.LogWarning(
                $"[IncreaseDamegeAndWeakEffect] 目标 [{target.name}] 缺少 MonsterStats。");
            return false;
        }

        bool attackResolvesOnHit = PlayerCombatReceiver.Instance != null;
        if (attackResolvesOnHit)
        {
            // CardManager reads card.damage on OnHit. Restore it after that callback
            // so the bonus applies to this attack only.
            int originalDamage = card.damage;
            card.damage = originalDamage + CounterDamageBonus;
            PlayerCombatReceiver.ExecuteOnHit(
                () => card.damage = originalDamage);
        }

        stats.AddAttackBuff(TargetAttackBuffChange);

        if (!attackResolvesOnHit)
        {
            stats.TakeDamage(CounterDamageBonus);
        }

        Debug.Log(
            $"[IncreaseDamegeAndWeakEffect] 反击伤害 +{CounterDamageBonus}，" +
            $"目标 [{target.name}] 的 AttackBuff 变为 {stats.AttackBuff}。");
        return true;
    }

    private static bool IsCounterStageActive()
    {
        return CounterTimeManager.Instance != null &&
               CounterTimeManager.Instance.IsCounterStageActive;
    }
}
