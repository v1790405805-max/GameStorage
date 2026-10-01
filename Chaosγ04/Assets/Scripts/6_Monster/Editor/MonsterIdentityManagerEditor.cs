using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MonsterIdentityManager))]
public class MonsterIdentityManagerEditor : Editor
{
    private SerializedProperty factionProperty;
    private SerializedProperty monsterIdProperty;
    private SerializedProperty isDogLeaderProperty;
    private SerializedProperty canTraverseSpecialTerrainProperty;
    private SerializedProperty hostileFactionsProperty;
    private SerializedProperty hostileFactionsInitializedProperty;

    private void OnEnable()
    {
        factionProperty = serializedObject.FindProperty("faction");
        monsterIdProperty = serializedObject.FindProperty("monsterId");
        isDogLeaderProperty = serializedObject.FindProperty("isDogLeader");
        canTraverseSpecialTerrainProperty = serializedObject.FindProperty("canTraverseSpecialTerrain");
        hostileFactionsProperty = serializedObject.FindProperty("hostileFactions");
        hostileFactionsInitializedProperty = serializedObject.FindProperty("hostileFactionsInitialized");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("怪物基础信息", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(factionProperty, new GUIContent("阵营"));
        DrawDogLeaderField();
        EditorGUILayout.PropertyField(monsterIdProperty, new GUIContent("怪物 ID"));
        EditorGUILayout.PropertyField(canTraverseSpecialTerrainProperty, new GUIContent("可通过特殊地形"));

        EditorGUILayout.Space(8f);
        DrawHostileFactionFlags();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawDogLeaderField()
    {
        MonsterIdentityManager.MonsterFaction faction =
            (MonsterIdentityManager.MonsterFaction)factionProperty.enumValueIndex;
        bool canBeDogLeader = faction == MonsterIdentityManager.MonsterFaction.Dog;

        if (!canBeDogLeader)
        {
            isDogLeaderProperty.boolValue = false;
            return;
        }

        EditorGUILayout.PropertyField(
            isDogLeaderProperty,
            new GUIContent("Dog Leader", "仅 Dog 阵营有效"));
    }

    private void DrawHostileFactionFlags()
    {
        EditorGUILayout.LabelField("敌对阵营", EditorStyles.boldLabel);

        MonsterIdentityManager.MonsterFaction selfFaction =
            (MonsterIdentityManager.MonsterFaction)factionProperty.enumValueIndex;
        MonsterIdentityManager.HostileFactionFlags flags =
            (MonsterIdentityManager.HostileFactionFlags)hostileFactionsProperty.intValue;

        if (!hostileFactionsInitializedProperty.boolValue)
        {
            flags =
                MonsterIdentityManager.HostileFactionFlags.Dog |
                MonsterIdentityManager.HostileFactionFlags.Crocodile |
                MonsterIdentityManager.HostileFactionFlags.Player;
            hostileFactionsInitializedProperty.boolValue = true;
        }

        MonsterIdentityManager.HostileFactionFlags selfFlag =
            MonsterIdentityManager.GetFactionFlag(selfFaction);
        flags &= ~selfFlag;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Hostile Flags");

        flags = DrawFlagToggle(
            flags,
            MonsterIdentityManager.HostileFactionFlags.Dog,
            "Dog",
            selfFlag == MonsterIdentityManager.HostileFactionFlags.Dog);

        flags = DrawFlagToggle(
            flags,
            MonsterIdentityManager.HostileFactionFlags.Crocodile,
            "Crocodile",
            selfFlag == MonsterIdentityManager.HostileFactionFlags.Crocodile);

        flags = DrawFlagToggle(
            flags,
            MonsterIdentityManager.HostileFactionFlags.Player,
            "Player",
            false);

        EditorGUILayout.EndHorizontal();

        hostileFactionsProperty.intValue = (int)flags;
        EditorGUILayout.HelpBox("自己所在的阵营会被锁定，不能勾选。", MessageType.Info);
    }

    private MonsterIdentityManager.HostileFactionFlags DrawFlagToggle(
        MonsterIdentityManager.HostileFactionFlags currentFlags,
        MonsterIdentityManager.HostileFactionFlags flag,
        string label,
        bool locked)
    {
        bool currentValue = (currentFlags & flag) != 0;

        using (new EditorGUI.DisabledScope(locked))
        {
            bool nextValue = EditorGUILayout.ToggleLeft(label, currentValue, GUILayout.Width(82f));
            if (!locked && nextValue != currentValue)
            {
                currentFlags = nextValue
                    ? currentFlags | flag
                    : currentFlags & ~flag;
            }
        }

        return currentFlags;
    }
}
