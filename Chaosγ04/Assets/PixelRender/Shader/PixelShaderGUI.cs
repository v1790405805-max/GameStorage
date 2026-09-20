using UnityEditor;
using UnityEngine;

namespace PixelRender
{
    public class PixelShaderGUI : ShaderGUI
    {
        private bool _showColor, _showLighting, _showAlpha,_showOutline;
        
        private bool _useLightDir, _useLocalLight, _useNormalMap, _useShadowAttenuation, _useSpecular, 
            _useRim, _useOutline, _useAlphaClip, _useDither, _useDitherOutline, _useEmission, _useDecal;

        private Material _material;
        
        //自定义灯光方向
        private const string LIGHT_DIR_ON = "_LIGHT_DIR_ON"; //自定义灯光方向
        private const string LOCAL_LIGHT_ON = "_LOCAL_LIGHT_ON"; //自定义灯光是否使用局部坐标
        private const string NORMAL_MAP_ON = "_NORMAL_MAP_ON"; //使用法线贴图
        private const string SHADOW_ATTENUATION_ON = "_SHADOW_ATTENUATION_ON"; //开启阴影
        private const string SPECULAR_ON = "_SPECULAR_ON"; //开启高光
        private const string RIM_ON = "_RIM_ON"; //开启边缘光
        private const string OUTLINE_ON = "_OUTLINE_ON"; //开启描边
        private const string ALPHA_CLIP_ON = "_ALPHA_CLIP_ON"; //开启AlphaClip
        private const string DITHER_ON = "_DITHER_ON"; //开启透明抖动
        private const string DITHER_OUTLINE_ON = "_DITHER_OUTLINE_ON"; //开启透明抖动描边
        private const string EMISSION_ON = "_EMISSION_ON";//开启自发光
        private const string DECAL_ON = "_DECAL_ON";//开始贴花

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            _material = materialEditor.target as Material;
            _useLightDir = _material.IsKeywordEnabled(LIGHT_DIR_ON);
            _useLocalLight = _material.IsKeywordEnabled(LOCAL_LIGHT_ON);
            _useNormalMap = _material.IsKeywordEnabled(NORMAL_MAP_ON);
            _useShadowAttenuation = _material.IsKeywordEnabled(SHADOW_ATTENUATION_ON);
            _useSpecular = _material.IsKeywordEnabled(SPECULAR_ON);
            _useRim = _material.IsKeywordEnabled(RIM_ON);
            _useOutline = _material.IsKeywordEnabled(OUTLINE_ON);
            _useAlphaClip = _material.IsKeywordEnabled(ALPHA_CLIP_ON);
            _useDither = _material.IsKeywordEnabled(DITHER_ON);
            _useDitherOutline = _material.IsKeywordEnabled(DITHER_OUTLINE_ON);
            _useEmission = _material.IsKeywordEnabled(EMISSION_ON);
            _useDecal = _material.IsKeywordEnabled(DECAL_ON);
            
            EditorGUILayout.LabelField("============PixelRender============", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            DrawColorGroup(materialEditor,properties);
            EditorGUILayout.Space();
            DrawLightingGroup(materialEditor,properties);
            EditorGUILayout.Space();
            DrawAlpha(materialEditor,properties);
            EditorGUILayout.Space();
            DrawOutline(materialEditor, properties);
            
            EditorGUILayout.Space();
            _useDecal = EditorGUILayout.ToggleLeft("贴花", _useDecal);
            if (_useDecal)
            {
                _material.EnableKeyword(DECAL_ON);
            }
            else
            {
                _material.DisableKeyword(DECAL_ON);
            }
            EditorGUILayout.Space();
            
            var cull = FindProperty("_Cull", properties);
            materialEditor.ShaderProperty(cull, "剔除模式");
            var zWrite = FindProperty("_ZWrite", properties);
            materialEditor.ShaderProperty(zWrite, "深度写入");
            var zTest = FindProperty("_ZTest", properties);
            materialEditor.ShaderProperty(zTest, "深度测试");
            
            var renderQueue = EditorGUILayout.IntField("Render Queue", _material.renderQueue);
            _material.renderQueue = renderQueue;
            var dsgi = EditorGUILayout.ToggleLeft("Double Sided Global Illumination", _material.doubleSidedGI);
            _material.doubleSidedGI = dsgi;
            EditorUtility.SetDirty(_material);
        }

        private void DrawColorGroup(MaterialEditor editor, MaterialProperty[] properties)
        {
            _showColor = EditorGUILayout.BeginFoldoutHeaderGroup(_showColor, "Color");
            if (_showColor)
            {
                var albedo = FindProperty("_MainTex", properties);
                editor.ShaderProperty(albedo, "MainTex");
                
                var color = FindProperty("_Color", properties);
                editor.ShaderProperty(color, "Color");
                
                _useEmission = EditorGUILayout.ToggleLeft("自发光", _useEmission);
                if (_useEmission)
                {
                    _material.EnableKeyword(EMISSION_ON);
                    var emissionMask = FindProperty("_EmissionMask", properties);
                    editor.ShaderProperty(emissionMask, "自发光遮罩");
                    
                    var emissionColor = FindProperty("_EmissionColor", properties);
                    editor.ShaderProperty(emissionColor, "自发光颜色");
                    
                    var emissionOutlineInt = FindProperty("_EmissionOutlineInt", properties);
                    editor.ShaderProperty(emissionOutlineInt, "自发光描边增强");
                }
                else
                {
                    _material.DisableKeyword(EMISSION_ON);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        private void DrawLightingGroup(MaterialEditor editor, MaterialProperty[] properties)
        {
            _showLighting = EditorGUILayout.BeginFoldoutHeaderGroup(_showLighting, "Lighting");
            if (_showLighting)
            {
                _useNormalMap = EditorGUILayout.ToggleLeft("法线贴图", _useNormalMap);
                if (_useNormalMap)
                {
                    _material.EnableKeyword(NORMAL_MAP_ON);
                    var noramlMapTex = FindProperty("_NormalTex", properties);
                    editor.ShaderProperty(noramlMapTex, "NormalMap");
                    
                    var normalScale = FindProperty("_NormalScale", properties);
                    editor.ShaderProperty(normalScale, "NormalScale");
                }
                else
                {
                    _material.DisableKeyword(NORMAL_MAP_ON);
                }
                
                var shadowRampTex = FindProperty("_ShadowRampTex", properties);
                editor.ShaderProperty(shadowRampTex, "漫反射Ramp");

                var shadingOffset = FindProperty("_ShadingOffset", properties);
                editor.ShaderProperty(shadingOffset, "阴影RampOffset");
                
                EditorGUILayout.Space();
                
                _useShadowAttenuation = EditorGUILayout.ToggleLeft("投影", _useShadowAttenuation);
                if (_useShadowAttenuation)
                {
                    _material.EnableKeyword(SHADOW_ATTENUATION_ON);
                    var lightAttenuation = FindProperty("_LightAttenuation", properties);
                    editor.ShaderProperty(lightAttenuation, "投影衰减");
                    
                    var shadowColor = FindProperty("_ShadowColor", properties);
                    editor.ShaderProperty(shadowColor, "投影颜色");
                }
                else
                {
                    _material.DisableKeyword(SHADOW_ATTENUATION_ON);
                }
                
                EditorGUILayout.Space();
                
                _useSpecular = EditorGUILayout.ToggleLeft("高光", _useSpecular);
                if (_useSpecular)
                {
                    _material.EnableKeyword(SPECULAR_ON);
                    var specularColor = FindProperty("_SpecularColor", properties);
                    editor.ShaderProperty(specularColor, "高光颜色");
                    
                    var specularSize = FindProperty("_SpecularSize", properties);
                    editor.ShaderProperty(specularSize, "高光大小");
                    
                    var specularEdgeSmoothness = FindProperty("_SpecularEdgeSmoothness", properties);
                    editor.ShaderProperty(specularEdgeSmoothness, "高光平滑");
                }
                else
                {
                    _material.DisableKeyword(SPECULAR_ON);
                }
                
                
                EditorGUILayout.Space();
                
                _useRim = EditorGUILayout.ToggleLeft("边缘光", _useRim);
                if (_useRim)
                {
                    _material.EnableKeyword(RIM_ON);
                    
                    var rimColor = FindProperty("_RimColor", properties);
                    editor.ShaderProperty(rimColor, "边缘光颜色");
                    
                    var rimGradient = FindProperty("_RimGradientTex", properties);
                    editor.ShaderProperty(rimGradient, "漫反射边缘Ramp");
                    
                    var rimSize = FindProperty("_RimSize", properties);
                    editor.ShaderProperty(rimSize, "边缘光尺寸");
                    
                    var rimEdgeSmoothness = FindProperty("_RimEdgeSmoothness", properties);
                    editor.ShaderProperty(rimEdgeSmoothness, "边缘光平滑"); 
                }
                else
                {
                    _material.DisableKeyword(RIM_ON);
                }
                
                _useLightDir = EditorGUILayout.ToggleLeft("自定平行光方向", _useLightDir);
                if (_useLightDir)
                {
                    _useLocalLight = EditorGUILayout.ToggleLeft("局部空间", _useLocalLight);
                    if (_useLocalLight)
                    {
                        _material.EnableKeyword(LOCAL_LIGHT_ON);
                    }
                    else
                    {
                        _material.DisableKeyword(LOCAL_LIGHT_ON);
                    }

                    _material.EnableKeyword(LIGHT_DIR_ON);
                    var lightRow = FindProperty("_LightRow", properties);
                    editor.ShaderProperty(lightRow, "LightRow");
                    
                    var lightYaw = FindProperty("_LightYaw", properties);
                    editor.ShaderProperty(lightYaw, "LightYaw");
                }
                else
                {
                    _material.DisableKeyword(LIGHT_DIR_ON);
                }

                EditorGUILayout.Space();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        private void DrawAlpha(MaterialEditor editor, MaterialProperty[] properties)
        {
            _showAlpha = EditorGUILayout.BeginFoldoutHeaderGroup(_showAlpha, "Alpha");
            if (_showAlpha)
            {
                _useDither = EditorGUILayout.ToggleLeft("抖动透明", _useDither);
                if (_useDither)
                {
                    _material.EnableKeyword(DITHER_ON);
                    var alpha = FindProperty("_Alpha", properties);
                    editor.ShaderProperty(alpha, "alpha");
                    _useDitherOutline = EditorGUILayout.ToggleLeft("保留描边", _useDitherOutline);
                    if (_useDitherOutline)
                    {
                        _material.EnableKeyword(DITHER_OUTLINE_ON);
                    }
                    else
                    {
                        _material.DisableKeyword(DITHER_OUTLINE_ON);
                    }
                }
                else
                {
                    _material.DisableKeyword(DITHER_ON);
                }
                _useAlphaClip = EditorGUILayout.ToggleLeft("AlpahClip", _useAlphaClip);
                if (_useAlphaClip)
                {
                    _material.EnableKeyword(ALPHA_CLIP_ON);
                    var clip = FindProperty("_AlphaClip", properties);
                    editor.ShaderProperty(clip, "Clip阈值");
                }
                else
                {
                    _material.DisableKeyword(ALPHA_CLIP_ON);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        private void DrawOutline(MaterialEditor editor, MaterialProperty[] properties)
        {
            _showOutline = EditorGUILayout.BeginFoldoutHeaderGroup(_showOutline, "Outline");
            if (_showOutline)
            {
                _useOutline = EditorGUILayout.ToggleLeft("启用描边", _useOutline);
                if (_useOutline)
                {
                    _material.EnableKeyword(OUTLINE_ON);

                    var addlightInt = FindProperty("_AddLightLineInt", properties);
                    editor.ShaderProperty(addlightInt, "额外线光增强");

                    EditorGUILayout.LabelField("============额外线============", EditorStyles.boldLabel);
                    var addLineColor = FindProperty("_AddLineColor", properties);
                    editor.ShaderProperty(addLineColor,"额外线颜色");
                    
                    var addLineGradientTex = FindProperty("_AddLineGradientTex", properties);
                    editor.ShaderProperty(addLineGradientTex,"额外线漫反射Ramp");
                    
                    var addLineEdge = FindProperty("_AddLineEdge", properties);
                    editor.ShaderProperty(addLineEdge, "额外线边缘");
                    
                    EditorGUILayout.LabelField("============边缘线============", EditorStyles.boldLabel);
                    var edgeColor = FindProperty("_EdgeColor", properties);
                    editor.ShaderProperty(edgeColor, "边缘颜色");
                    
                    var edgeGradient = FindProperty("_EdgeGradientTex", properties);
                    editor.ShaderProperty(edgeGradient,"边缘漫反射Ramp");
                    
                    var edgeAngle = FindProperty("_EdgeAngle", properties);
                    editor.ShaderProperty(edgeAngle, "边缘角度");
                
                    EditorGUILayout.LabelField("============描边线============", EditorStyles.boldLabel);
                    var outlineColor = FindProperty("_OutlineColor", properties);
                    editor.ShaderProperty(outlineColor, "描边颜色");
                    
                    var outlineGradient = FindProperty("_OutlineGradientTex", properties);
                    editor.ShaderProperty(outlineGradient,"描边漫反射Ramp");
                
                    var outlineSort = FindProperty("_OutlineSort", properties);
                    editor.ShaderProperty(outlineSort, "描边排序");
                }
                else
                {
                    _material.DisableKeyword(OUTLINE_ON);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
    }
}
