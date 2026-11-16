using UnityEngine;

/// <summary>
/// 被动范围效果：常规情况下使用方形范围，反击时间中改为直线范围。
/// </summary>
public class DuckRangeTypeEffect : CardEffectCore
{
    private const RangeType NormalRangeType = RangeType.Square;
    private const RangeType CounterRangeType = RangeType.Straight;

    public override bool TryGetRangeTypeOverride(CardData card, out RangeType rangeType)
    {
        rangeType = CounterTimeManager.Instance != null &&
                    CounterTimeManager.Instance.IsActive
            ? CounterRangeType
            : NormalRangeType;
        return true;
    }

    public override bool Execute(CardData card, Vector2Int targetGrid)
    {
        // 范围类型在拖拽和落点校验前已经解析，这里不需要额外结算。
        return true;
    }
}
