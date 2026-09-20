using UnityEngine;

public class GridSystem
{
    public int Width { get; private set; }
    public int Height { get; private set; }
    public float CellWidth { get; private set; }
    /// <summary>格子的纵深尺寸（原 CellHeight 更名）。</summary>
    public float CellLength { get; private set; }
    public float Gap { get; private set; }
    /// <summary>层高：铺设完成后按此高度丈量层号（0~1 格高 = L1，1~2 格高 = L2，以此类推）。</summary>
    public float CellHeight { get; private set; }

    private Vector3 centerPosition;
    private float totalWidth;
    private float totalLength;

    public GridSystem(int width, int height, float cellWidth, float cellLength, float gap, float cellHeight, Vector3 centerPosition = default)
    {
        this.Width = width;
        this.Height = height;
        this.CellWidth = cellWidth;
        this.CellLength = cellLength;
        this.Gap = gap;
        this.CellHeight = cellHeight;
        this.centerPosition = centerPosition;

        this.totalWidth = (width * cellWidth) + ((width - 1) * gap);
        this.totalLength = (height * cellLength) + ((height - 1) * gap);
    }

    /// <summary>
    /// 获取棋盘最左上角的起点位置（因为现在纵排从上往下，所以左上角作为起点）
    /// </summary>
    private Vector3 GetTopLeftOrigin()
    {
        float halfWidth = totalWidth * 0.5f;
        float halfLength = totalLength * 0.5f;
        // X 轴往左退一半，Z 轴往上进一半
        return centerPosition + new Vector3(-halfWidth, 0f, halfLength);
    }

    /// <summary>
    /// 根据网格坐标 (x, z)，计算该格子「中心点」在 3D 世界中的物理位置 (Vector3)。
    /// 使用逻辑平面高度 (Y = 0)；多层地图请使用带 height 参数的重载。
    /// </summary>
    public Vector3 GetWorldPosition(int x, int z)
    {
        return GetWorldPosition(x, z, 0f);
    }

    /// <summary>
    /// 根据网格坐标 (x, z) 与指定表面高度 height，计算该格子「中心点」的世界位置。
    /// 同一 (x, z) 在不同高度上可以有多个格子，height 用于区分层级。
    /// </summary>
    public Vector3 GetWorldPosition(int x, int z, float height)
    {
        if (!IsValidGridPosition(x, z))
        {
            Debug.LogWarning($"[GridSystem] 尝试获取越界坐标: ({x}, {z})");
        }

        Vector3 origin = GetTopLeftOrigin();

        // X 轴向右递增 (+)
        float posX = x * (CellWidth + Gap) + (CellWidth * 0.5f);
        // Z 轴向底/下递减 (-)，从而实现“从上往下”排列
        float posZ = -(z * (CellLength + Gap) + (CellLength * 0.5f));

        return origin + new Vector3(posX, height, posZ);
    }

    /// <summary>
    /// 输入一个 3D 世界物理坐标，反向推算出它属于哪一个网格坐标 (x, z)
    /// </summary>
    public (int x, int z) GetGridPosition(Vector3 worldPosition)
    {
        Vector3 origin = GetTopLeftOrigin();
        Vector3 relativePos = worldPosition - origin;

        int x = Mathf.FloorToInt(relativePos.x / (CellWidth + Gap));
        // Z轴是向下延伸的，所以取负数来计算
        int z = Mathf.FloorToInt(-relativePos.z / (CellLength + Gap));

        return (x, z);
    }

    /// <summary>
    /// 按照层高 (CellHeight) 丈量高度，返回对应的层号：
    /// 0 ~ 1 格高 → L1，1 ~ 2 格高 → L2，2 ~ 3 格高 → L3，以此类推。
    /// 低于 0 的高度统一归入 L1；层高未配置时返回 L1。
    /// </summary>
    /// <param name="height">相对网格逻辑平面 (Y = 0) 的高度。</param>
    public int GetLayerNumberFromHeight(float height)
    {
        if (CellHeight <= 0f) return 1;

        int layer = Mathf.FloorToInt(height / CellHeight) + 1;
        return Mathf.Max(1, layer);
    }

    public bool IsValidGridPosition(int x, int z)
    {
        return x >= 0 && x < Width && z >= 0 && z < Height;
    }
}
