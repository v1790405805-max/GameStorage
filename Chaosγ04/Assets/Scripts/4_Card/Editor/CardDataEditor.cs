using UnityEngine;
using UnityEditor;

/// <summary>
/// CardData 自定义 Inspector。
/// 根据 effectFlags 开关动态显示/隐藏对应数值字段，并显示 gridStyle 样式引用槽。
/// </summary>
[CustomEditor(typeof(CardData))]
public class CardDataEditor : Editor
{
    // 基础信息
    private SerializedProperty cardID;
    private SerializedProperty cardName;
    private SerializedProperty type;
    private SerializedProperty cost;
    private SerializedProperty effectFlags;
    private SerializedProperty extraEffects;

    // 效果数值
    private SerializedProperty moveDistance;
    private SerializedProperty damage;
    private SerializedProperty block;
    private SerializedProperty healthChange;
    private SerializedProperty energyChange;
    private SerializedProperty actionPointChange;
    private SerializedProperty drawAmount;
    private SerializedProperty discardAmount;

    // 范围
    private SerializedProperty rangeType;
    private SerializedProperty rangeDistance;
    private SerializedProperty targetSelectMode;

    // Grid 样式
    private SerializedProperty gridStyle;

    // 描述
    private SerializedProperty description;

    private void OnEnable()
    {
        cardID = serializedObject.FindProperty("cardID");
        cardName = serializedObject.FindProperty("cardName");
        type = serializedObject.FindProperty("type");
        cost = serializedObject.FindProperty("cost");
        effectFlags = serializedObject.FindProperty("effectFlags");
        extraEffects = serializedObject.FindProperty("extraEffects");

        moveDistance = serializedObject.FindProperty("moveDistance");
        damage = serializedObject.FindProperty("damage");
        block = serializedObject.FindProperty("block");
        healthChange = serializedObject.FindProperty("healthChange");
        energyChange = serializedObject.FindProperty("energyChange");
        actionPointChange = serializedObject.FindProperty("actionPointChange");
        drawAmount = serializedObject.FindProperty("drawAmount");
        discardAmount = serializedObject.FindProperty("discardAmount");

        rangeType = serializedObject.FindProperty("rangeType");
        rangeDistance = serializedObject.FindProperty("rangeDistance");
        targetSelectMode = serializedObject.FindProperty("targetSelectMode");

        gridStyle = serializedObject.FindProperty("gridStyle");

        description = serializedObject.FindProperty("description");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // ── 1. 基础信息 ────────────────────────────────────────────
        EditorGUILayout.LabelField("基础信息", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(cardID, new GUIContent("Card ID"));
        EditorGUILayout.PropertyField(cardName, new GUIContent("Card Name"));
        EditorGUILayout.PropertyField(type, new GUIContent("Type"));
        EditorGUILayout.PropertyField(cost, new GUIContent("Cost"));

        EditorGUILayout.Space(8f);

        // ── 2. 效果类型开关 ────────────────────────────────────────
        EditorGUILayout.LabelField("效果类型开关", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(effectFlags, new GUIContent("Effect Flags"));
        // 监控 Flag 修改，确保勾选/取消时 UI 能立即响应
        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
        }

        EditorGUILayout.Space(8f);


        // ── 3. 效果数值（按开关动态显示）─────────────────────────
        // 关键修复：使用 intValue 代替 enumValueFlag，确保值能正确读取和提交
        CardEffectType flags = (CardEffectType)effectFlags.intValue;

        if (flags != CardEffectType.None)
        {
            EditorGUILayout.LabelField("效果数值", EditorStyles.boldLabel);

            if ((flags & CardEffectType.Movement) != 0) EditorGUILayout.PropertyField(moveDistance, new GUIContent("Move Distance"));
            if ((flags & CardEffectType.Attack) != 0) EditorGUILayout.PropertyField(damage, new GUIContent("Damage"));
            if ((flags & CardEffectType.Defense) != 0) EditorGUILayout.PropertyField(block, new GUIContent("Block"));
            if ((flags & CardEffectType.Health) != 0) EditorGUILayout.PropertyField(healthChange, new GUIContent("Health Change"));
            if ((flags & CardEffectType.Energy) != 0) EditorGUILayout.PropertyField(energyChange, new GUIContent("Energy Change"));
            if ((flags & CardEffectType.ActionPoint) != 0) EditorGUILayout.PropertyField(actionPointChange, new GUIContent("Action Point Change"));
            if ((flags & CardEffectType.DrawCard) != 0) EditorGUILayout.PropertyField(drawAmount, new GUIContent("Draw Amount"));
            if ((flags & CardEffectType.DiscardCard) != 0) EditorGUILayout.PropertyField(discardAmount, new GUIContent("Discard Amount"));

            EditorGUILayout.Space(8f);
        }

        // ── 4. 效果范围 ────────────────────────────────────────────
        EditorGUILayout.LabelField("效果作用范围", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(rangeType, new GUIContent("Range Type"));

        RangeType currentRangeType = (RangeType)rangeType.enumValueIndex;
        // 仅非 Point 类型需要显示 rangeDistance
        if (currentRangeType != RangeType.Point)
            EditorGUILayout.PropertyField(rangeDistance, new GUIContent("Range Distance"));

        EditorGUILayout.PropertyField(targetSelectMode, new GUIContent("Target Select Mode",
            "AllCells = 整个范围生效，无需精确落点\n" +
            "AnyCell  = 范围内任选一格落点\n" +
            "EdgeOnly = 只能选边沿格作为落点"));

        EditorGUILayout.Space(8f);

        // ── 4.5 额外效果（效果脚本挂载）──────────────────────────
        EditorGUILayout.LabelField("额外效果（效果脚本）", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("从 Project 窗口直接拖拽效果脚本（.cs）到下方槽位，可挂多个，按顺序触发。", MessageType.Info);

        for (int i = 0; i < extraEffects.arraySize; i++)
        {
            SerializedProperty element = extraEffects.GetArrayElementAtIndex(i);
            SerializedProperty scriptProp = element.FindPropertyRelative("effectScript");
            SerializedProperty typeNameProp = element.FindPropertyRelative("effectTypeName");

            // 防御：字段缺失时给出明确提示，而不是让 Inspector 渲染崩溃
            if (scriptProp == null || typeNameProp == null)
            {
                EditorGUILayout.HelpBox("额外效果字段缺失（检查 CardData.cs 中 ExtraCardEffect 定义）。", MessageType.Error);
                break;
            }

            EditorGUILayout.BeginHorizontal();
            MonoScript current = scriptProp.objectReferenceValue as MonoScript;
            MonoScript next = (MonoScript)EditorGUILayout.ObjectField($"效果 {i + 1}", current, typeof(MonoScript), false);
            if (next != current)
            {
                // 只允许挂载 CardEffect 子类脚本，并同步类型全名（Unity 6 运行时无 MonoScript，运行时靠类型名实例化）
                if (next != null)
                {
                    System.Type cls = next.GetClass();
                    if (cls == null || !typeof(CardEffect).IsAssignableFrom(cls))
                    {
                        Debug.LogWarning($"[CardDataEditor] 脚本 [{next.name}] 不是 CardEffect 的子类，无法挂载。");
                        next = null;
                    }
                }
                scriptProp.objectReferenceValue = next;
                typeNameProp.stringValue = next != null ? next.GetClass().AssemblyQualifiedName : null;
            }
            if (GUILayout.Button("×", GUILayout.Width(24)))
            {
                extraEffects.DeleteArrayElementAtIndex(i);
            }
            EditorGUILayout.EndHorizontal();
        }

        if (GUILayout.Button("+ 添加效果"))
        {
            extraEffects.InsertArrayElementAtIndex(extraEffects.arraySize);
        }

        EditorGUILayout.Space(8f);

        // ── 5. Grid 渲染样式 ───────────────────────────────────────
        EditorGUILayout.LabelField("Grid 渲染样式", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(gridStyle, new GUIContent("Grid Style",
            "拖拽此卡牌时使用的 Grid 高亮样式资产（GridStyleData）。\n留空则不显示范围高亮。"));

        // 样式未赋值时给出提示
        if (gridStyle.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox("未指定 Grid Style，拖拽此卡牌时将不显示 Grid 范围高亮。", MessageType.Warning);
        }

        EditorGUILayout.Space(8f);

        // ── 6. 卡牌描述 ────────────────────────────────────────────
        EditorGUILayout.LabelField("卡牌描述 Description", EditorStyles.boldLabel);
        description.stringValue = EditorGUILayout.TextArea(description.stringValue, GUILayout.Height(60));

        serializedObject.ApplyModifiedProperties();
    }
}