using UnityEngine;

/// <summary>
/// Runtime card target. The concrete cell is authoritative; GridCoord is its stable logical key.
/// </summary>
public readonly struct CardTarget
{
    public readonly CellManager Cell;
    public readonly GridCoord Coord;

    private CardTarget(CellManager cell, GridCoord coord)
    {
        Cell = cell;
        Coord = coord;
    }

    public bool IsExact => Cell != null;
    public bool IsValid => Coord.IsValid;
    public Vector2Int Grid => Coord.GridPosition;
    public int Layer => Coord.Layer;

    public static CardTarget FromCell(CellManager cell)
    {
        if (cell == null)
        {
            return default;
        }

        GridCoord coord = cell.Coord;
        if (!coord.IsValid)
        {
            GridManager gridManager = Object.FindFirstObjectByType<GridManager>();
            if (gridManager != null)
            {
                var (x, z) = gridManager.GetCellGridPosition(cell);
                coord = new GridCoord(x, z, gridManager.GetCellLayer(cell));
            }
        }

        return new CardTarget(cell, coord);
    }

    public static CardTarget FromCoord(GridManager gridManager, GridCoord coord)
    {
        CellManager cell = gridManager != null
            ? gridManager.GetCellManagerAt(coord)
            : null;
        return cell != null
            ? FromCell(cell)
            : new CardTarget(null, coord);
    }

    public static CardTarget FromGrid(GridManager gridManager, Vector2Int grid)
    {
        CellManager cell = gridManager != null
            ? gridManager.GetCellManagerAt(grid.x, grid.y)
            : null;
        return cell != null
            ? FromCell(cell)
            : new CardTarget(null, new GridCoord(grid.x, grid.y, 0));
    }

    public override string ToString()
    {
        return IsExact ? $"{Cell.name} {Coord}" : Coord.ToString();
    }
}
