using System;
using UnityEngine;

/// <summary>
/// Logical identity of one concrete cell in a grid column.
/// X and Z select the column; Layer is the height layer derived from that surface.
/// </summary>
[Serializable]
public readonly struct GridCoord : IEquatable<GridCoord>
{
    public static readonly GridCoord Invalid = new GridCoord(-1, -1, 0);

    public readonly int X;
    public readonly int Z;
    public readonly int Layer;

    public GridCoord(int x, int z, int layer)
    {
        X = x;
        Z = z;
        Layer = layer;
    }

    public bool IsValid => X >= 0 && Z >= 0;
    public Vector2Int GridPosition => new Vector2Int(X, Z);

    public bool Equals(GridCoord other)
    {
        return X == other.X && Z == other.Z && Layer == other.Layer;
    }

    public override bool Equals(object obj)
    {
        return obj is GridCoord other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hashCode = X;
            hashCode = (hashCode * 397) ^ Z;
            hashCode = (hashCode * 397) ^ Layer;
            return hashCode;
        }
    }

    public override string ToString()
    {
        return $"({X}, {Z}, L{Layer})";
    }

    public static bool operator ==(GridCoord left, GridCoord right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(GridCoord left, GridCoord right)
    {
        return !left.Equals(right);
    }
}
