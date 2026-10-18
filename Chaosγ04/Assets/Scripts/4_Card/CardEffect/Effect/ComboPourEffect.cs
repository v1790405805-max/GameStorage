using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 1010 倾泻：
/// 本回合打出的第 6 张或之后的卡牌才能使用；
/// 对目标造成 10 × 本回合已打出卡牌数（包含本卡）的伤害。
/// </summary>
public sealed class ComboPourEffect : CardEffectCore
{
    private const int DamagePerCombo = 10;
    private const int DefaultComboRequirement = 6;

    public override bool CanPlay(CardData card, Vector2Int targetGrid)
    {
        CardManager cardManager = CardManager.Instance;
        return cardManager != null &&
               cardManager.MeetsCombo(GetRequiredCombo(card));
    }

    public override bool Execute(CardData card, Vector2Int targetGrid)
    {
        CardManager cardManager = CardManager.Instance;
        if (cardManager == null)
        {
            return false;
        }

        int requiredCombo = GetRequiredCombo(card);
        if (!cardManager.MeetsCurrentCombo(requiredCombo))
        {
            Debug.LogWarning(
                $"[ComboPourEffect] 当前连击数为 {cardManager.ComboCount}，未达到连击 {requiredCombo}。");
            return false;
        }

        int damage = DamagePerCombo * cardManager.ComboCount;
        if (damage <= 0)
        {
            return false;
        }

        GridManager gridManager = Object.FindFirstObjectByType<GridManager>();
        if (gridManager == null || MonsterIdentitySystem.Instance == null)
        {
            Debug.LogWarning("[ComboPourEffect] 无法找到 GridManager 或 MonsterIdentitySystem。");
            return false;
        }

        List<MonsterIdentityManager> monsters =
            new List<MonsterIdentityManager>(MonsterIdentitySystem.Instance.GetAllMonsters());

        PlayerCombatReceiver.ExecuteOnHit(() =>
        {
            foreach (MonsterIdentityManager monster in monsters)
            {
                if (monster == null || !monster.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var (mx, mz) = gridManager.GetGridPosition(monster.transform.position);
                if (new Vector2Int(mx, mz) != targetGrid)
                {
                    continue;
                }

                MonsterStats stats = monster.GetComponent<MonsterStats>();
                stats?.TakeDamage(damage);
            }
        });

        Debug.Log(
            $"[ComboPourEffect] 连击 {cardManager.ComboCount}，倾泻造成 {damage} 点伤害。");
        return true;
    }

    private static int GetRequiredCombo(CardData card)
    {
        return card != null && card.combo > 0 ? card.combo : DefaultComboRequirement;
    }
}
