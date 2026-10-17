using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CardManager))]
public class CardManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        CardManager cardManager = target as CardManager;
        if (cardManager == null)
        {
            return;
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("本回合连击（运行时只读）", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.IntField("ComboCount", cardManager.ComboCount);
            EditorGUILayout.IntField("NextCombo", cardManager.NextCombo);
        }

        if (Application.isPlaying)
        {
            Repaint();
        }
    }
}
