using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MonsterHateSystem))]
[CanEditMultipleObjects]
public sealed class MonsterHateSystemEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("实时仇恨状态", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("进入 Play 模式后显示实时仇恨状态。", MessageType.Info);
            return;
        }

        GridManager gridManager = UnityEngine.Object.FindFirstObjectByType<GridManager>();

        for (int i = 0; i < targets.Length; i++)
        {
            MonsterHateSystem hateSystem = targets[i] as MonsterHateSystem;
            if (hateSystem == null)
            {
                continue;
            }

            DrawHateSystem(hateSystem, gridManager);
        }

        Repaint();
    }

    private static void DrawHateSystem(MonsterHateSystem hateSystem, GridManager gridManager)
    {
        MonsterIdentityManager identity = hateSystem.GetComponent<MonsterIdentityManager>();
        string displayName = identity != null && !string.IsNullOrEmpty(identity.monsterId)
            ? identity.monsterId
            : hateSystem.gameObject.name;

        EditorGUILayout.LabelField(displayName, EditorStyles.boldLabel);

        if (gridManager != null &&
            hateSystem.TrySelectTarget(
                gridManager,
                int.MaxValue,
                true,
                out MonsterTarget selectedTarget,
                out int selectedDistance,
                out int selectedHate))
        {
            string distanceText = selectedDistance == int.MaxValue
                ? "不可达"
                : selectedDistance.ToString();
            EditorGUILayout.LabelField(
                "最高仇恨目标",
                $"{selectedTarget.DisplayName}  距离 {distanceText}  仇恨 {selectedHate}");
        }
        else
        {
            EditorGUILayout.LabelField("最高仇恨目标", "无");
        }

        List<MonsterHateDebugEntry> entries = hateSystem.BuildDebugSnapshot(gridManager);
        if (entries.Count == 0)
        {
            EditorGUILayout.LabelField("仇恨列表", "空");
            EditorGUILayout.Space(4f);
            return;
        }

        EditorGUILayout.LabelField("仇恨列表", EditorStyles.miniBoldLabel);
        EditorGUI.indentLevel++;

        for (int i = 0; i < entries.Count; i++)
        {
            MonsterHateDebugEntry entry = entries[i];
            string distanceText = entry.HasDistance ? entry.Distance.ToString() : "不可达";

            EditorGUILayout.LabelField(entry.DisplayName, EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField("距离", distanceText);
            EditorGUILayout.LabelField("基础仇恨", entry.BaselineHate.ToString());
            EditorGUILayout.LabelField("受击仇恨", entry.HitHate.ToString());
            EditorGUILayout.LabelField("距离仇恨", entry.ProximityHate.ToString());
            EditorGUILayout.LabelField("总仇恨", entry.TotalHate.ToString());

            if (i < entries.Count - 1)
            {
                EditorGUILayout.Space(2f);
            }
        }

        EditorGUI.indentLevel--;
        EditorGUILayout.Space(4f);
    }
}
