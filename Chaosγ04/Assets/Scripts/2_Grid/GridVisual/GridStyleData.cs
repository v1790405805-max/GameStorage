using UnityEngine;

/// <summary>
/// Grid 渲染样式资产（ScriptableObject）。
/// 每个资产文件对应一套配色方案，资产名称即为该样式的标识（如 PlayerMove、CardAttack 等）。
/// </summary>
[CreateAssetMenu(fileName = "NewGridStyleData", menuName = "GridStyleBasic/GridStyleData")]
public class GridStyleData : ScriptableObject
{
    [Header("影响范围内格子颜色配置")]
    public Color cellClickedColor      = new Color(0f,           0f,           0f,           0.6f);
    public Color lineClickedColor      = new Color(179f / 255f,  1f,           231f / 255f,  26f / 255f);

    [Header("范围区域外边线颜色配置")]
    public Color outerLineClickedColor = new Color(0f,           1f,           170f / 255f,  1f);

    [Header("玩家所在格颜色配置")]
    public Color playerCellColor       = new Color(125f / 255f,  178f / 255f,  125f / 255f,  102f / 255f);
    public Color playerLineColor       = new Color(128f / 255f,  1f,           128f / 255f,  1f);

    [Header("目标格（范围内鼠标悬停）颜色配置")]
    public Color targetCellColor       = new Color(54f / 255f,   76f / 255f,   54f / 255f,   102f / 255f);
    public Color targetLineColor       = new Color(128f / 255f,  1f,           128f / 255f,  1f);
}
