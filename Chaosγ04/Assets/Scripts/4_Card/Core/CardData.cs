using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>卡牌类型</summary>
public enum CardType
{
    Special,    // 特殊卡
    Attack,     // 攻击
    Skill,      // 技能
    Ability,    // 能力
    Movement    // 位移
}

/// <summary>卡牌范围形状</summary>
public enum RangeType
{
    Point,      // 点状（以施法者自身为施法目标）
    Straight,   // 直线
    Diamond     // 菱形
}

/// <summary>
/// 目标选择模式：决定玩家在高亮范围内如何指定最终落点。
/// 与 RangeType 正交，可自由组合。
/// </summary>
public enum TargetSelectMode
{
    AnyCell,
    EdgeOnly,
    Aoe,
    AQuarterCircle,
}

/// <summary>
/// 卡牌效果类型（位标志），支持在 Inspector 中多选叠加。
/// </summary>
[Flags]
public enum CardEffectType
{
    None = 0,
    Movement = 1 << 0,   // 位移
    Attack = 1 << 1,   // 攻击
    Defense = 1 << 2,   // 防御（格挡）
    Health = 1 << 3,   // 血量（治疗/自损）
    Energy = 1 << 4,   // 能量（增减）
    ActionPoint = 1 << 5,   // 行动点（增减）
    DrawCard = 1 << 6,   // 摸牌
    DiscardCard = 1 << 7    // 弃牌
}

[Serializable]
public struct ExtraCardEffect
{
    [Tooltip("从 Project 窗口直接拖拽效果脚本（.cs）到此槽位，如 TeleportEffect")]
    public UnityEngine.Object effectScript;

    [HideInInspector]
    public string effectTypeName;
}

[Serializable]
public struct CardPresentationEffectReference
{
    [Tooltip("从 Project 窗口直接拖拽动画或特效脚本（.cs）到此槽位")]
    public UnityEngine.Object effectScript;

    [HideInInspector]
    public string effectTypeName;
}

/// <summary>
/// 卡牌数据资产（ScriptableObject）。
/// </summary>
[CreateAssetMenu(fileName = "New Card Data", menuName = "Card Basic/Card Data")]
public class CardData : ScriptableObject
{
    public const int VariableCostValue = -1;

    public string cardID;
    public string cardName;
    public CardType type;
    [Tooltip("固定费用输入整数；变量费用输入 X（序列化值为 -1）")]
    public int cost;

    public bool IsVariableCost => cost == VariableCostValue;

    public CardEffectType effectFlags;

    [Tooltip("位移距离/格数")]
    public int moveDistance;
    [Tooltip("攻击伤害值")]
    public int damage;
    [Tooltip("格挡/护盾值（正数添加，负数移除）")]
    public int block;
    [Tooltip("血量变化值（正数恢复，负数自损/扣血）")]
    public int healthChange;
    [Tooltip("能量变化值（正数增加，负数消耗）")]
    public int energyChange;
    [Tooltip("行动点变化值（正数增加，负数消耗）")]
    public int actionPointChange;
    [Tooltip("摸牌数量")]
    public int drawAmount;
    [Tooltip("弃牌数量")]
    public int discardAmount;

    public RangeType rangeType = RangeType.Point;
    public int rangeDistance;

    public TargetSelectMode targetSelectMode = TargetSelectMode.AnyCell;

    [Tooltip("额外效果（非数值类），按需拖拽效果脚本（.cs）挂载，可挂多个")]
    public List<ExtraCardEffect> extraEffects = new List<ExtraCardEffect>();

    [Header("卡牌动画效果")]
    [Tooltip("按顺序拖拽 CardAnimationCore 子类脚本（.cs）挂载此卡牌的动画表现")]
    public List<CardPresentationEffectReference> animationEffects = new List<CardPresentationEffectReference>();

    [Header("卡牌特效")]
    [Tooltip("按顺序拖拽 CardVFXCore 子类脚本（.cs）挂载此卡牌的特效表现")]
    public List<CardPresentationEffectReference> vfxEffects = new List<CardPresentationEffectReference>();

    [Tooltip("拖拽此卡牌时使用的 Grid 高亮样式资产（GridStyleData）。\n留空则不显示范围高亮。")]
    public GridStyleData gridStyle;

    [Header("卡牌效果描述")]
    [TextArea(2, 4)]
    public string description;

    public int GetEffectiveCost()
    {
        if (IsVariableCost) return 1;

        int effectiveCost = cost;
        if (extraEffects == null) return Mathf.Max(0, effectiveCost);

        foreach (ExtraCardEffect extra in extraEffects)
        {
            if (string.IsNullOrEmpty(extra.effectTypeName)) continue;
            Type effectType = Type.GetType(extra.effectTypeName);
            if (effectType == null || !typeof(CardEffectCore).IsAssignableFrom(effectType)) continue;

            CardEffectCore instance = CreateInstance(effectType) as CardEffectCore;
            if (instance == null) continue;
            effectiveCost += instance.GetCostModifier(this);
            Destroy(instance);
        }

        return Mathf.Max(0, effectiveCost);
    }

    public string GetCostDisplayText() => IsVariableCost ? "X" : GetEffectiveCost().ToString();

    public int GetEffectiveMoveDistance() => Mathf.Max(0, moveDistance + GetTotalMoveDistanceModifier());

    public int GetEffectiveRangeDistance()
    {
        int rangeModifier = GetTotalMoveDistanceModifier();
        if (effectFlags.HasFlag(CardEffectType.Movement))
        {
            rangeModifier += MovementRangeEnlargeEffect.GetRangeDistanceBonus(this);
        }
        return Mathf.Max(0, rangeDistance + rangeModifier);
    }

    private int GetTotalMoveDistanceModifier()
    {
        int modifier = 0;
        if (extraEffects == null) return modifier;

        foreach (ExtraCardEffect extra in extraEffects)
        {
            if (string.IsNullOrEmpty(extra.effectTypeName)) continue;
            Type effectType = Type.GetType(extra.effectTypeName);
            if (effectType == null || !typeof(CardEffectCore).IsAssignableFrom(effectType)) continue;

            CardEffectCore instance = CreateInstance(effectType) as CardEffectCore;
            if (instance == null) continue;
            modifier += instance.GetMoveDistanceModifier(this);
            Destroy(instance);
        }

        return modifier;
    }

    public CardData Clone()
    {
        CardData clone = CreateInstance<CardData>();
        clone.name = string.IsNullOrEmpty(cardName) ? name : cardName;
        clone.cardID = cardID;
        clone.cardName = cardName;
        clone.type = type;
        clone.cost = cost;
        clone.effectFlags = effectFlags;
        clone.extraEffects = extraEffects == null ? null : new List<ExtraCardEffect>(extraEffects);
        clone.animationEffects = animationEffects == null ? null : new List<CardPresentationEffectReference>(animationEffects);
        clone.vfxEffects = vfxEffects == null ? null : new List<CardPresentationEffectReference>(vfxEffects);
        clone.moveDistance = moveDistance;
        clone.damage = damage;
        clone.block = block;
        clone.healthChange = healthChange;
        clone.energyChange = energyChange;
        clone.actionPointChange = actionPointChange;
        clone.drawAmount = drawAmount;
        clone.discardAmount = discardAmount;
        clone.rangeType = rangeType;
        clone.rangeDistance = rangeDistance;
        clone.targetSelectMode = targetSelectMode;
        clone.gridStyle = gridStyle;
        clone.description = description;
        return clone;
    }
}