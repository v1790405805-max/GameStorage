using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BaseHateManager))]
public sealed class BaseHateManagerEditor : Editor
{
    private SerializedProperty factionHateEntriesProperty;

    private void OnEnable()
    {
        factionHateEntriesProperty = serializedObject.FindProperty("factionHateEntries");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        BaseHateManager manager = (BaseHateManager)target;
        MonsterIdentityManager identity = manager.GetComponent<MonsterIdentityManager>();
        List<FactionOption> options = BuildOptions(identity);

        EditorGUILayout.LabelField("基础仇恨配置", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "可添加 Player 或敌对怪物阵营。",
            MessageType.Info);

        for (int i = 0; i < factionHateEntriesProperty.arraySize; i++)
        {
            SerializedProperty entryProperty =
                factionHateEntriesProperty.GetArrayElementAtIndex(i);
            SerializedProperty factionProperty =
                entryProperty.FindPropertyRelative("faction");
            SerializedProperty hateProperty =
                entryProperty.FindPropertyRelative("hate");

            const float removeButtonWidth = 48f;
            Rect factionRowRect = EditorGUILayout.GetControlRect(true);
            Rect factionPopupRect = EditorGUI.PrefixLabel(
                factionRowRect,
                new GUIContent("阵营"));
            factionPopupRect.width -= removeButtonWidth + 4f;
            DrawFactionPopup(factionProperty, options, factionPopupRect);

            Rect removeButtonRect = new Rect(
                factionRowRect.xMax - removeButtonWidth,
                factionRowRect.y,
                removeButtonWidth,
                factionRowRect.height);
            if (GUI.Button(removeButtonRect, "移除"))
            {
                factionHateEntriesProperty.DeleteArrayElementAtIndex(i);
                i--;
                continue;
            }

            Rect hateRect = EditorGUILayout.GetControlRect(true);
            hateProperty.intValue = Mathf.Max(
                0,
                EditorGUI.IntField(hateRect, new GUIContent("初始仇恨"), hateProperty.intValue));
        }

        if (GUILayout.Button("添加阵营仇恨"))
        {
            AddEntry(options.Count > 0
                ? options[0].Faction
                : BaseHateManager.HateFaction.Player);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawFactionPopup(
        SerializedProperty factionProperty,
        List<FactionOption> options,
        Rect position)
    {
        if (options.Count == 0)
        {
            EditorGUI.LabelField(position, "无可用阵营");
            return;
        }

        int currentValue = factionProperty.intValue;
        int currentIndex = options.FindIndex(option => (int)option.Faction == currentValue);
        if (currentIndex < 0)
        {
            currentIndex = 0;
            factionProperty.intValue = (int)options[currentIndex].Faction;
        }

        string[] labels = new string[options.Count];
        for (int i = 0; i < options.Count; i++)
        {
            labels[i] = options[i].Label;
        }

        int nextIndex = EditorGUI.Popup(position, currentIndex, labels);
        factionProperty.intValue = (int)options[nextIndex].Faction;
    }

    private void AddEntry(BaseHateManager.HateFaction faction)
    {
        int index = factionHateEntriesProperty.arraySize;
        factionHateEntriesProperty.InsertArrayElementAtIndex(index);

        SerializedProperty entryProperty =
            factionHateEntriesProperty.GetArrayElementAtIndex(index);
        entryProperty.FindPropertyRelative("faction").intValue = (int)faction;
        entryProperty.FindPropertyRelative("hate").intValue = 0;
    }

    private static List<FactionOption> BuildOptions(MonsterIdentityManager identity)
    {
        List<FactionOption> options = new List<FactionOption>
        {
            new FactionOption(BaseHateManager.HateFaction.Player, "Player")
        };

        if (identity == null ||
            identity.faction != MonsterIdentityManager.MonsterFaction.Dog)
        {
            options.Add(new FactionOption(BaseHateManager.HateFaction.Dog, "Dog"));
        }

        if (identity == null ||
            identity.faction != MonsterIdentityManager.MonsterFaction.Crocodile)
        {
            options.Add(new FactionOption(BaseHateManager.HateFaction.Crocodile, "Crocodile"));
        }

        return options;
    }

    private readonly struct FactionOption
    {
        public BaseHateManager.HateFaction Faction { get; }
        public string Label { get; }

        public FactionOption(BaseHateManager.HateFaction faction, string label)
        {
            Faction = faction;
            Label = label;
        }
    }
}
