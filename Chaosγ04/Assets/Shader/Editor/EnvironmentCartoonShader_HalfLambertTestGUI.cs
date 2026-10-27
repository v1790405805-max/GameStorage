using UnityEditor;
using UnityEngine;

public class EnvironmentCartoonShader_HalfLambertTestGUI : ShaderGUI
{
    private bool _showBase = true;
    private bool _showLighting = true;
    private bool _showTones;
    private bool _showSpecular;
    private bool _showRim;
    private bool _showEnvironment;

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        EditorGUILayout.LabelField("环境卡通着色器（半伦勃朗测试）", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        DrawBaseGroup(materialEditor, properties);
        DrawLightingGroup(materialEditor, properties);
        DrawToneGroup(materialEditor, properties);
        DrawSpecularGroup(materialEditor, properties);
        DrawRimGroup(materialEditor, properties);
        DrawEnvironmentGroup(materialEditor, properties);
    }

    private void DrawBaseGroup(MaterialEditor editor, MaterialProperty[] properties)
    {
        _showBase = EditorGUILayout.BeginFoldoutHeaderGroup(_showBase, "基础外观");
        if (_showBase)
        {
            EditorGUI.indentLevel++;

            MaterialProperty mainColor = FindMaterialProperty("_MainColor", properties);
            MaterialProperty baseMap = FindMaterialProperty("_BaseMap", properties);
            DrawProperty(editor, mainColor, "基础颜色");
            if (baseMap != null)
            {
                editor.TexturePropertySingleLine(new GUIContent("基础贴图"), baseMap);
            }

            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawLightingGroup(MaterialEditor editor, MaterialProperty[] properties)
    {
        _showLighting = EditorGUILayout.BeginFoldoutHeaderGroup(_showLighting, "半兰伯特渐变");
        if (_showLighting)
        {
            EditorGUI.indentLevel++;
            DrawProperty(editor, FindMaterialProperty("_ShadowThreshold", properties), "阈值");
            DrawProperty(editor, FindMaterialProperty("_Smoothness", properties), "阴影过渡平滑度");
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawToneGroup(MaterialEditor editor, MaterialProperty[] properties)
    {
        _showTones = EditorGUILayout.BeginFoldoutHeaderGroup(_showTones, "Gooch 冷暖色调");
        if (_showTones)
        {
            EditorGUI.indentLevel++;
            DrawProperty(editor, FindMaterialProperty("_CoolColor", properties), "冷色");
            DrawProperty(editor, FindMaterialProperty("_WarmColor", properties), "暖色");
            DrawProperty(editor, FindMaterialProperty("_CoolFactor", properties), "冷色系数");
            DrawProperty(editor, FindMaterialProperty("_WarmFactor", properties), "暖色系数");
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawSpecularGroup(MaterialEditor editor, MaterialProperty[] properties)
    {
        _showSpecular = EditorGUILayout.BeginFoldoutHeaderGroup(_showSpecular, "高光");
        if (_showSpecular)
        {
            EditorGUI.indentLevel++;
            DrawProperty(editor, FindMaterialProperty("_SpecColor", properties), "高光颜色");
            DrawProperty(editor, FindMaterialProperty("_SpecGloss", properties), "高光光泽度 / 强度");
            DrawProperty(editor, FindMaterialProperty("_SpecSmoothness", properties), "高光平滑度（卡通）");
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawRimGroup(MaterialEditor editor, MaterialProperty[] properties)
    {
        _showRim = EditorGUILayout.BeginFoldoutHeaderGroup(_showRim, "菲涅尔与边缘光");
        if (_showRim)
        {
            EditorGUI.indentLevel++;
            DrawProperty(editor, FindMaterialProperty("_RimColor", properties), "边缘 / 菲涅尔颜色");
            DrawProperty(editor, FindMaterialProperty("_RimPower", properties), "边缘 / 菲涅尔强度");
            DrawProperty(editor, FindMaterialProperty("_RimThreshold", properties), "边缘阈值");
            DrawProperty(editor, FindMaterialProperty("_RimSmoothness", properties), "边缘平滑度");
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawEnvironmentGroup(MaterialEditor editor, MaterialProperty[] properties)
    {
        _showEnvironment = EditorGUILayout.BeginFoldoutHeaderGroup(_showEnvironment, "环境");
        if (_showEnvironment)
        {
            EditorGUI.indentLevel++;
            DrawProperty(editor, FindMaterialProperty("_EnvironmentIntensity", properties), "环境强度");
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private static void DrawProperty(MaterialEditor editor, MaterialProperty property, string label)
    {
        if (property != null)
        {
            editor.ShaderProperty(property, label);
        }
    }

    private static MaterialProperty FindMaterialProperty(string propertyName, MaterialProperty[] properties)
    {
        foreach (MaterialProperty property in properties)
        {
            if (property.name == propertyName)
            {
                return property;
            }
        }

        return null;
    }
}
