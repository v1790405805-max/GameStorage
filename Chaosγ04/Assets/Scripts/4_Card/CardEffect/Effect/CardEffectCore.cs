using UnityEngine;

/// <summary>
/// 卡牌效果基类：非数值类效果（如传送）继承本类。
/// 在 Inspector 中通过 ExtraCardEffect 槽位直接拖拽效果脚本（.cs）挂载，
/// 打出卡牌时由 CardManager 运行时实例化并执行，无需创建效果资产文件。
/// </summary>
public abstract class CardEffectCore : ScriptableObject
{
    /// <summary>
    /// 卡牌打出前的费用修正值。返回负数表示降低费用，默认不修改。
    /// </summary>
    public virtual int GetCostModifier(CardData card)
    {
        return 0;
    }

    /// <summary>
    /// 卡牌打出前的位移距离修正值。默认不修改。
    /// </summary>
    public virtual int GetMoveDistanceModifier(CardData card)
    {
        return 0;
    }

    /// <summary>
    /// 卡牌打出前校验效果是否允许使用。默认允许。
    /// 返回 false 时，调用方应取消出牌，不消耗费用，也不播放卡牌表现。
    /// </summary>
    public virtual bool CanPlay(CardData card, Vector2Int targetGrid)
    {
        return true;
    }

    /// <summary>
    /// 校验卡牌挂载的全部额外效果是否允许在指定落点出牌。
    /// </summary>
    public static bool CanPlayAll(CardData card, Vector2Int targetGrid)
    {
        if (card == null || card.extraEffects == null)
        {
            return true;
        }

        foreach (ExtraCardEffect extra in card.extraEffects)
        {
            if (string.IsNullOrEmpty(extra.effectTypeName))
            {
                continue;
            }

            System.Type effectType = System.Type.GetType(extra.effectTypeName);
            if (effectType == null ||
                !typeof(CardEffectCore).IsAssignableFrom(effectType) ||
                effectType.IsAbstract)
            {
                continue;
            }

            CardEffectCore instance = CreateInstance(effectType) as CardEffectCore;
            if (instance == null)
            {
                continue;
            }

            bool canPlay = instance.CanPlay(card, targetGrid);
            Destroy(instance);

            if (!canPlay)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 执行效果。
    /// </summary>
    /// <param name="card">触发本次结算的卡牌数据（运行时克隆实例）</param>
    /// <param name="targetGrid">落点格坐标</param>
    /// <returns>是否执行成功（失败仅告警，不取消整卡）</returns>
    public abstract bool Execute(CardData card, Vector2Int targetGrid);
}
