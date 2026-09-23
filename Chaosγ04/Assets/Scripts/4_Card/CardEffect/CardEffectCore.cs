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
    /// 执行效果。
    /// </summary>
    /// <param name="card">触发本次结算的卡牌数据（运行时克隆实例）</param>
    /// <param name="targetGrid">落点格坐标</param>
    /// <returns>是否执行成功（失败仅告警，不取消整卡）</returns>
    public abstract bool Execute(CardData card, Vector2Int targetGrid);
}
