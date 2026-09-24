using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 持续型卡牌效果的基类与本场运行时。
/// 负责创建能力实例、持有实例、分发战斗事件，并在战斗结束时统一销毁。
/// 具体能力继承本类，实现自身效果；卡牌牌堆仍由 CardManager 管理。
/// </summary>
public abstract class AbilityCore : CardEffectCore
{
    private static readonly List<AbilityCore> activeAbilities = new List<AbilityCore>();
    public static IReadOnlyList<AbilityCore> ActiveAbilities => activeAbilities;

    public CardData SourceCard { get; private set; }
    public bool IsActive { get; private set; }

    public sealed override bool Execute(CardData card, Vector2Int targetGrid)
    {
        return true;
    }

    public static void BeginCombat()
    {
        EndCombat();
    }

    public static void EndCombat()
    {
        foreach (AbilityCore ability in SnapshotActiveAbilities())
        {
            if (ability == null)
            {
                continue;
            }

            ability.Deactivate();
            Destroy(ability);
        }

        activeAbilities.Clear();
    }

    public static bool HasAbilityEffect(CardData card)
    {
        if (card == null || card.extraEffects == null)
        {
            return false;
        }

        foreach (ExtraCardEffect extra in card.extraEffects)
        {
            Type effectType = ResolveEffectType(extra);
            if (effectType != null && typeof(AbilityCore).IsAssignableFrom(effectType))
            {
                return true;
            }
        }

        return false;
    }

    public static bool TryActivate(CardData sourceCard, ExtraCardEffect extra)
    {
        if (sourceCard == null)
        {
            return false;
        }

        Type effectType = ResolveEffectType(extra);
        if (effectType == null || !typeof(AbilityCore).IsAssignableFrom(effectType))
        {
            return false;
        }

        AbilityCore ability = CreateInstance(effectType) as AbilityCore;
        if (ability == null)
        {
            return false;
        }

        ability.Activate(sourceCard);
        activeAbilities.Add(ability);
        return true;
    }

    public static void ActivateFromCard(CardData sourceCard)
    {
        if (sourceCard == null || sourceCard.extraEffects == null)
        {
            return;
        }

        foreach (ExtraCardEffect extra in sourceCard.extraEffects)
        {
            TryActivate(sourceCard, extra);
        }
    }

    public static void NotifyPlayerTurnStarted()
    {
        foreach (AbilityCore ability in SnapshotActiveAbilities())
        {
            if (ability != null)
            {
                ability.OnPlayerTurnStarted();
            }
        }
    }

    public static void NotifyEnemyTurnStarted()
    {
        foreach (AbilityCore ability in SnapshotActiveAbilities())
        {
            if (ability != null)
            {
                ability.OnEnemyTurnStarted();
            }
        }
    }

    public static void NotifyCardPlayed(CardData playedCard, Vector2Int targetGrid)
    {
        foreach (AbilityCore ability in SnapshotActiveAbilities())
        {
            if (ability != null)
            {
                ability.OnCardPlayed(playedCard, targetGrid);
            }
        }
    }

    /// <summary>
    /// 聚合当前所有能力，判断该格是否允许作为路径中间点。
    /// </summary>
    public static bool CanTraverseCell(CellManager cell)
    {
        if (cell == null)
        {
            return false;
        }

        foreach (AbilityCore ability in activeAbilities)
        {
            if (ability != null && ability.CanTraverse(cell))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 聚合当前所有能力，判断该格是否允许作为最终停留点。
    /// </summary>
    public static bool CanLandOnCell(CellManager cell)
    {
        if (cell == null)
        {
            return false;
        }

        foreach (AbilityCore ability in activeAbilities)
        {
            if (ability != null && !ability.CanLandOn(cell))
            {
                return false;
            }
        }

        return true;
    }

    internal void Activate(CardData sourceCard)
    {
        if (IsActive || sourceCard == null)
        {
            return;
        }

        SourceCard = sourceCard;
        IsActive = true;
        OnActivated();
    }

    internal void Deactivate()
    {
        if (!IsActive)
        {
            return;
        }

        try
        {
            OnDeactivated();
        }
        finally
        {
            IsActive = false;
            SourceCard = null;
        }
    }

    private static AbilityCore[] SnapshotActiveAbilities()
    {
        AbilityCore[] snapshot = new AbilityCore[activeAbilities.Count];
        activeAbilities.CopyTo(snapshot);
        return snapshot;
    }

    private static Type ResolveEffectType(ExtraCardEffect extra)
    {
        if (string.IsNullOrEmpty(extra.effectTypeName))
        {
            return null;
        }

        return Type.GetType(extra.effectTypeName);
    }

    /// <summary>
    /// 源卡牌打出时调用一次。
    /// </summary>
    protected virtual void OnActivated() { }

    /// <summary>
    /// 战斗结束、恢复至较早状态或能力实例被移除时调用。
    /// </summary>
    protected virtual void OnDeactivated() { }

    public virtual void OnPlayerTurnStarted() { }

    public virtual void OnEnemyTurnStarted() { }

    public virtual void OnCardPlayed(CardData playedCard, Vector2Int targetGrid) { }

    /// <summary>
    /// 默认不允许经过原本被阻挡的格子，由移动类能力按需覆盖。
    /// </summary>
    public virtual bool CanTraverse(CellManager cell) { return false; }

    /// <summary>
    /// 默认不限制落点，由移动类能力按需覆盖。
    /// </summary>
    public virtual bool CanLandOn(CellManager cell) { return true; }
}
