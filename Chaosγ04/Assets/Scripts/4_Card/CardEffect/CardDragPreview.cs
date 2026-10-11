using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 拖拽期间的卡牌预览数据。由实现了 <see cref="ICardDragPreviewProvider"/> 的额外效果提供，
/// CardDragController 只负责把它渲染出来（范围高亮 / Player_WhenMove / 假设性位移），
/// 自身不含任何与具体卡牌相关的逻辑。
/// </summary>
public struct CardDragPreview
{
    /// <summary>需要用卡牌 gridStyle 的目标格配色高亮的格子。</summary>
    public List<CellManager> highlightCells;

    /// <summary>玩家将要到达的落点（用于 Player_WhenMove 预览）；为空则不显示。</summary>
    public CellManager playerPreviewCell;

    /// <summary>拖拽期间会被假设性移动的怪物（如推击目标）；为空则不移动。</summary>
    public MonsterIdentityManager monsterToMove;

    /// <summary>假设性移动的目标格。</summary>
    public CellManager monsterDestinationCell;
}

/// <summary>
/// 额外效果实现本接口后，CardDragController 会在拖拽悬停时调用它来生成预览。
/// 与具体卡牌无关：任何效果都能提供自己的预览内容。
/// </summary>
public interface ICardDragPreviewProvider
{
    bool TryGetDragPreview(
        GridManager gridManager,
        CardData card,
        Vector2Int targetGrid,
        out CardDragPreview preview);
}
