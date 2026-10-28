using System;
using UnityEditor;
using UnityEngine;

public class EnvironmentCartoonShaderCustomGUI : ShaderGUI
{
    private bool _showSurface = true;
    private bool _showLighting = true;
    private bool _showSpecular;
    private bool _showRim;
    private bool _showOutline = true;
    private bool _showEmission;
    private bool _showEnvironment = true;

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        EditorGUILayout.LabelField("环境卡通着色器", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        DrawSurfaceGroup(materialEditor, properties);
        DrawLightingGroup(materialEditor, properties);
        DrawSpecularGroup(materialEditor, properties);
        DrawRimGroup(materialEditor, properties);
        DrawOutlineGroup(materialEditor, properties);
        DrawEmissionGroup(materialEditor, properties);
        DrawEnvironmentGroup(materialEditor, properties);
    }

    private void DrawSurfaceGroup(MaterialEditor editor, MaterialProperty[] properties)
    {
        DrawFoldoutGroup(ref _showSurface, "基础表面", () =>
        {
            MaterialProperty baseMap = FindMaterialProperty("_BaseMap", properties);
            MaterialProperty baseColor = FindMaterialProperty("_BaseColor", properties);
            DrawProperty(editor, baseMap, "基础贴图");
            DrawProperty(editor, baseColor, "基础颜色");
            EditorGUILayout.Space(2);

            MaterialProperty normalMap = FindMaterialProperty("_NormalMap", properties);
            MaterialProperty normalScale = FindMaterialProperty("_NormalScale", properties);
            if (normalMap != null && normalScale != null)
            {
                editor.TexturePropertySingleLine(new GUIContent("法线贴图"), normalMap, normalScale);
            }
            else
            {
                DrawProperty(editor, normalMap, "法线贴图");
                DrawProperty(editor, normalScale, "法线强度");
            }

            MaterialProperty occlusionMap = FindMaterialProperty("_OcclusionMap", properties);
            MaterialProperty occlusionStrength = FindMaterialProperty("_OcclusionStrength", properties);
            if (occlusionMap != null && occlusionStrength != null)
            {
                editor.TexturePropertySingleLine(new GUIContent("遮蔽贴图"), occlusionMap, occlusionStrength);
            }
            else
            {
                DrawProperty(editor, occlusionMap, "遮蔽贴图");
                DrawProperty(editor, occlusionStrength, "遮蔽强度");
            }

            DrawProperty(editor, FindMaterialProperty("_VertexColorStrength", properties), "顶点色强度");
            EditorGUILayout.Space(2);
            DrawProperty(editor, FindMaterialProperty("_AlphaClip", properties), "启用透明裁剪");
            DrawProperty(editor, FindMaterialProperty("_Cutoff", properties), "透明裁剪阈值");
        });
    }

    private void DrawLightingGroup(MaterialEditor editor, MaterialProperty[] properties)
    {
        DrawFoldoutGroup(ref _showLighting, "半伦勃朗光照", () =>
        {
            DrawProperty(editor, FindMaterialProperty("_ShadowColor", properties), "阴影颜色");
            DrawProperty(editor, FindMaterialProperty("_ShadowThreshold", properties), "半伦勃朗阈值");
            DrawProperty(editor, FindMaterialProperty("_ShadowSmoothness", properties), "半伦勃朗过渡");
            DrawProperty(editor, FindMaterialProperty("_ShadowStrength", properties), "阴影强度");
            DrawProperty(editor, FindMaterialProperty("_AdditionalLightStrength", properties), "附加光强度");
        });
    }

    private void DrawSpecularGroup(MaterialEditor editor, MaterialProperty[] properties)
    {
        DrawFoldoutGroup(ref _showSpecular, "卡通高光", () =>
        {
            DrawProperty(editor, FindMaterialProperty("_SpecColor", properties), "高光颜色");
            DrawProperty(editor, FindMaterialProperty("_SpecThreshold", properties), "高光阈值");
            DrawProperty(editor, FindMaterialProperty("_SpecSmoothness", properties), "高光平滑度");
            DrawProperty(editor, FindMaterialProperty("_SpecStrength", properties), "高光强度");
        });
    }

    private void DrawRimGroup(MaterialEditor editor, MaterialProperty[] properties)
    {
        DrawFoldoutGroup(ref _showRim, "边缘光", () =>
        {
            DrawProperty(editor, FindMaterialProperty("_RimColor", properties), "边缘光颜色");
            DrawProperty(editor, FindMaterialProperty("_RimPower", properties), "边缘光衰减");
            DrawProperty(editor, FindMaterialProperty("_RimThreshold", properties), "边缘光阈值");
            DrawProperty(editor, FindMaterialProperty("_RimSmoothness", properties), "边缘光平滑度");
            DrawProperty(editor, FindMaterialProperty("_RimStrength", properties), "边缘光强度");
        });
    }

    private void DrawEmissionGroup(MaterialEditor editor, MaterialProperty[] properties)
    {
        DrawFoldoutGroup(ref _showEmission, "自发光", () =>
        {
            MaterialProperty emissionMap = FindMaterialProperty("_EmissionMap", properties);
            MaterialProperty emissionColor = FindMaterialProperty("_EmissionColor", properties);
            if (emissionMap != null && emissionColor != null)
            {
                editor.TexturePropertySingleLine(new GUIContent("自发光贴图"), emissionMap, emissionColor);
            }
            else
            {
                DrawProperty(editor, emissionMap, "自发光贴图");
                DrawProperty(editor, emissionColor, "自发光颜色");
            }
        });
    }

    private void DrawOutlineGroup(MaterialEditor editor, MaterialProperty[] properties)
    {
        DrawFoldoutGroup(ref _showOutline, "描边", () =>
        {
            DrawProperty(editor, FindMaterialProperty("_OutlineEnabled", properties), "启用描边");
            DrawProperty(editor, FindMaterialProperty("_OutlineColor", properties), "描边颜色");
            DrawProperty(editor, FindMaterialProperty("_OutlineWidth", properties), "描边宽度（像素）");
        });
    }

    private void DrawEnvironmentGroup(MaterialEditor editor, MaterialProperty[] properties)
    {
        DrawFoldoutGroup(ref _showEnvironment, "环境", () =>
        {
            EditorGUILayout.HelpBox(
                "环境光支持 Lightmap 与 Skybox 环境探针，反射优先使用 Reflection Probe，并自动回退到 Skybox；雾效使用 URP 场景雾设置。",
                MessageType.None);
            DrawProperty(editor, FindMaterialProperty("_EnvironmentIntensity", properties), "环境光强度");
            DrawProperty(editor, FindMaterialProperty("_EnvironmentReflectionIntensity", properties), "环境反射强度");
            DrawProperty(editor, FindMaterialProperty("_EnvironmentReflectionRoughness", properties), "环境反射粗糙度");
            DrawProperty(editor, FindMaterialProperty("_EnvironmentReflectionFresnelPower", properties), "环境反射菲涅尔衰减");
            DrawProperty(editor, FindMaterialProperty("_FogInfluence", properties), "雾效影响");
        });
    }

    private static void DrawFoldoutGroup(ref bool expanded, string title, Action drawContent)
    {
        expanded = EditorGUILayout.Foldout(expanded, title, true, EditorStyles.foldoutHeader);
        if (!expanded)
        {
            return;
        }

        EditorGUI.indentLevel++;
        try
        {
            drawContent();
        }
        finally
        {
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.Space(2);
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
