using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>卡牌类型</summary>
public enum CardType
{
    Special,    // 特殊卡
    Attack,     // 攻击
    Skill,      // 技能
    Power,      // 能力
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
    /// <summary>
    /// 玩家可在范围内任意选一格作为落点。
    /// 落点必须命中 CurrentHighlightedGrids 内的某个格子。
    /// 适用：指向性攻击、位移落点选择等。
    /// </summary>
    AnyCell,

    /// <summary>
    /// 只能选范围最外沿的格子作为落点（每个方向取最远有效格）。
    /// 高亮显示完整范围，但落点验证只接受边沿格。
    /// 适用：必须打到最远处的穿刺技能等。
    /// </summary>
    EdgeOnly,

    /// <summary>
    /// AOE（范围效果）：范围内任意格都可作为落点，释放后效果作用于整个范围。
    /// 悬停范围内任意格时，整个范围联动显示为目标高亮。
    /// 适用：范围轰炸、群体增益/治疗等覆盖整片区域的卡牌。
    /// </summary>
    Aoe,
}

/// <summary>
/// 卡牌效果类型（位标志），支持在 Inspector 中多选叠加。
/// </summary>
[Flags]
public enum CardEffectType
{
    None        = 0,
    Movement    = 1 << 0,   // 位移
    Attack      = 1 << 1,   // 攻击
    Defense     = 1 << 2,   // 防御（格挡）
    Health      = 1 << 3,   // 血量（治疗/自损）
    Energy      = 1 << 4,   // 能量（增减）
    ActionPoint = 1 << 5,   // 行动点（增减）
    DrawCard    = 1 << 6,   // 摸牌
    DiscardCard = 1 << 7    // 弃牌
}

/// <summary>
/// 额外效果挂载槽：直接拖拽效果脚本（CardEffectCore 子类的 .cs）挂载，非数值类效果经此触发。
/// 注意：Unity 6 中 MonoScript 仅编辑器可用，运行时通过 effectTypeName 解析类型实例化，
/// 该字段由 CardDataEditor 在拖入脚本时自动写入。
/// </summary>
[Serializable]
public struct ExtraCardEffect
{
    [Tooltip("从 Project 窗口直接拖拽效果脚本（.cs）到此槽位，如 TeleportEffect")]
    public UnityEngine.Object effectScript;

    [HideInInspector]
    public string effectTypeName;   // 拖入脚本时由编辑器自动写入类型全名（运行时实例化用）
}

/// <summary>
/// 卡牌表现效果挂载槽：动画与特效脚本共用相同的引用结构。
/// 实际类型由 CardDataEditor 校验，运行时通过 effectTypeName 实例化。
/// </summary>
[Serializable]
public struct CardPresentationEffectReference
{
    [Tooltip("从 Project 窗口直接拖拽动画或特效脚本（.cs）到此槽位")]
    public UnityEngine.Object effectScript;

    [HideInInspector]
    public string effectTypeName;   // 拖入脚本时由编辑器自动写入类型全名（运行时实例化用）
}

/// <summary>
/// 卡牌数据资产（ScriptableObject）。
/// </summary>
[CreateAssetMenu(fileName = "New Card Data", menuName = "Card Basic/Card Data")]
public class CardData : ScriptableObject
{
    public string   cardID;
    public string   cardName;
    public CardType type;
    public int      cost;           // 卡牌费用

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

    public RangeType rangeType     = RangeType.Point;
    public int       rangeDistance;                     // 范围距离

    [Tooltip("目标选择模式：决定玩家如何在范围内指定落点。\n" +
             "AllCells = 整个范围全部生效，无需落点\n" +
             "AnyCell  = 范围内任选一格\n" +
             "EdgeOnly = 只能选最外沿格子\n" +
             "AOE      = 范围内任意格释放，效果覆盖整个范围")]
    public TargetSelectMode targetSelectMode = TargetSelectMode.AnyCell;

    [Tooltip("额外效果（非数值类），按需拖拽效果脚本（.cs）挂载，可挂多个")]
    public List<ExtraCardEffect> extraEffects = new List<ExtraCardEffect>();

    [Header("卡牌动画效果")]
    [Tooltip("按顺序拖拽 CardAnimationCore 子类脚本（.cs）挂载此卡牌的动画表现")]
    public List<CardPresentationEffectReference> animationEffects = new List<CardPresentationEffectReference>();

    [Header("卡牌特效")]
    [Tooltip("按顺序拖拽 CardVFXCore 子类脚本（.cs）挂载此卡牌的特效表现")]
    public List<CardPresentationEffectReference> vfxEffects = new List<CardPresentationEffectReference>();


    [Tooltip("拖拽此卡牌时使用的 Grid 高亮样式资产（GridStyleData）。\n" +
             "留空则不显示范围高亮。")]
    public GridStyleData gridStyle;

    [Header("卡牌效果描述")]
    [TextArea(2, 4)]
    public string description;

    /// <summary>
    /// 克隆卡牌数据，防止运行时修改影响原始资产。
    /// </summary>
    public CardData Clone()
    {
        CardData clone = CreateInstance<CardData>();
        clone.name              = string.IsNullOrEmpty(cardName) ? name : cardName;
        clone.cardID            = cardID;
        clone.cardName          = cardName;
        clone.type              = type;
        clone.cost              = cost;
        clone.effectFlags       = effectFlags;
        clone.extraEffects      = extraEffects == null ? null : new List<ExtraCardEffect>(extraEffects);
        clone.animationEffects  = animationEffects == null ? null : new List<CardPresentationEffectReference>(animationEffects);
        clone.vfxEffects        = vfxEffects == null ? null : new List<CardPresentationEffectReference>(vfxEffects);
        clone.moveDistance      = moveDistance;
        clone.damage            = damage;
        clone.block             = block;
        clone.healthChange      = healthChange;
        clone.energyChange      = energyChange;
        clone.actionPointChange = actionPointChange;
        clone.drawAmount        = drawAmount;
        clone.discardAmount     = discardAmount;
        clone.rangeType         = rangeType;
        clone.rangeDistance     = rangeDistance;
        clone.targetSelectMode  = targetSelectMode;
        clone.gridStyle         = gridStyle;    // ScriptableObject 引用，浅拷贝即可
        clone.description       = description;
        return clone;
    }
}
