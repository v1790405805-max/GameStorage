using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PixelRender
{
    #if UNITY_EDITOR
    public class LeaveShaderGUI : ShaderGUI
    {
        private bool _showColor, _showLighting, _showLeave,_showWind, _showOutline;

        private bool _useCircleAngle, _useRim,_useOutline,_useLeaveOUtline,_useOutlineDepth;
        
        private Material _material;

        private const string CIRLE_ANGLE_ON = "_CIRCLE_ANGLE_ON";
        private const string RIM_ON = "_RIM_ON";
        private const string OUTLINE_ON = "_OUTLINE_ON";
        private const string LEAVE_OUTLINE_ON = "_LEAVE_OUTLINE_ON";
        private const string OUTLINE_DEPTH_ON = "_OUTLINE_DEPTH_ON";

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            _material = materialEditor.target as Material;
            
            _useCircleAngle = _material.IsKeywordEnabled(CIRLE_ANGLE_ON);
            _useRim = _material.IsKeywordEnabled(RIM_ON);
            _useOutline = _material.IsKeywordEnabled(OUTLINE_ON);
            _useLeaveOUtline = _material.IsKeywordEnabled(LEAVE_OUTLINE_ON);
            _useOutlineDepth = _material.IsKeywordEnabled(OUTLINE_DEPTH_ON);
            
            EditorGUILayout.LabelField("============PixelLeave============", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            DrawMainColorGroup(materialEditor,properties);
            DrawLightingGroup(materialEditor, properties);
            DrawLeaveGroup(materialEditor, properties);
            DrawWindGroup(materialEditor, properties);
            DrawOutline(materialEditor, properties);
            
            var renderQueue = EditorGUILayout.IntField("Render Queue", _material.renderQueue);
            _material.renderQueue = renderQueue;
            EditorUtility.SetDirty(_material);
        }

        private void DrawMainColorGroup(MaterialEditor editor, MaterialProperty[] properties)
        {
            _showColor = EditorGUILayout.BeginFoldoutHeaderGroup(_showColor, "Color");
            if (_showColor)
            {
                var albedo = FindProperty("_MainTex", properties);
                editor.ShaderProperty(albedo, "MainTex");
                
                var color = FindProperty("_MainColor", properties);
                editor.ShaderProperty(color, "MainColor");
                
                var sheetX = FindProperty("_SheetX", properties);
                editor.ShaderProperty(sheetX, "SheetX");
                
                var sheetY = FindProperty("_SheetY", properties);
                editor.ShaderProperty(sheetY, "SheetY");
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        private void DrawLightingGroup(MaterialEditor editor, MaterialProperty[] properties)
        {
            _showLighting = EditorGUILayout.BeginFoldoutHeaderGroup(_showLighting, "光照");
            if (_showLighting)
            {
                var shadowRampTex = FindProperty("_ShadowRampTex", properties);
                editor.ShaderProperty(shadowRampTex, "漫反射Ramp");
                
                var noiseTex = FindProperty("_NoiseTex", properties);
                editor.ShaderProperty(noiseTex,"噪声");
                
                var noiseSize = FindProperty("_NoiseSize", properties);
                editor.ShaderProperty(noiseSize,"噪声尺寸");
                
                var noiseInt = FindProperty("_NoiseInt", properties);
                editor.ShaderProperty(noiseInt,"噪声强度");
                
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
                
                
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawLeaveGroup(MaterialEditor editor, MaterialProperty[] properties)
        {
            _showLeave = EditorGUILayout.BeginFoldoutHeaderGroup(_showLeave, "叶子");
            if (_showLeave)
            {
                var scale = FindProperty("_Scale",properties);
                editor.ShaderProperty(scale,"树叶尺寸");
                
                var offset = FindProperty("_Offset",properties);
                editor.ShaderProperty(offset,"位置偏移");
                
                _useCircleAngle = EditorGUILayout.ToggleLeft("环绕角度", _useCircleAngle);
                if (_useCircleAngle)
                {
                    _material.EnableKeyword(CIRLE_ANGLE_ON);
                }
                else
                {
                    _material.DisableKeyword(CIRLE_ANGLE_ON);
                }

                var angleInt = FindProperty("_AngleInt",properties);
                editor.ShaderProperty(angleInt,"角度偏移");
                
                var angleOffset = FindProperty("_AngleOffset",properties);
                editor.ShaderProperty(angleOffset,"角度扰乱");
                
                var centerScale = FindProperty("_CenterScale",properties);
                editor.ShaderProperty(centerScale,"边缘放大");
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawWindGroup(MaterialEditor editor, MaterialProperty[] properties)
        {
            _showWind = EditorGUILayout.BeginFoldoutHeaderGroup(_showWind, "风");
            if (_showWind)
            {
                var noiseTex = FindProperty("_NoiseTex", properties);
                editor.ShaderProperty(noiseTex,"噪声");
                
                var rotateNoiseSize = FindProperty("_RotateNoiseSize", properties);
                editor.ShaderProperty(rotateNoiseSize,"摆动噪声尺寸");
                
                var rotateNoiseInt = FindProperty("_RotateNoiseInt", properties);
                editor.ShaderProperty(rotateNoiseInt,"摆动噪声强度");
                
                var rotateSpeed = FindProperty("_RotateSpeed", properties);
                editor.ShaderProperty(rotateSpeed,"摆动速度");
                
                var sheetSpeed = FindProperty("_SheetSpeed", properties);
                editor.ShaderProperty(sheetSpeed,"序列帧速度");
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

                    _useLeaveOUtline = EditorGUILayout.ToggleLeft("单独树叶描边", _useLeaveOUtline);
                    if (_useLeaveOUtline)
                    {
                        _material.EnableKeyword(LEAVE_OUTLINE_ON);
                    }
                    else
                    {
                        _material.DisableKeyword(LEAVE_OUTLINE_ON);
                    }
                    
                    var addlightInt = FindProperty("_AddLightLineInt", properties);
                    editor.ShaderProperty(addlightInt, "额外线光增强");

                    var outlineColor = FindProperty("_OutlineColor", properties);
                    editor.ShaderProperty(outlineColor, "描边颜色");
                    
                    var outlineGradient = FindProperty("_OutlineGradientTex", properties);
                    editor.ShaderProperty(outlineGradient,"描边漫反射Ramp");
                
                    var outlineSort = FindProperty("_OutlineSort", properties);
                    editor.ShaderProperty(outlineSort, "描边排序");
                    
                    _useOutlineDepth = EditorGUILayout.ToggleLeft("自定义描边深度", _useOutlineDepth);
                    if (_useOutlineDepth)
                    {
                        _material.EnableKeyword(OUTLINE_DEPTH_ON);
                        var outlineDepth = FindProperty("_OutlineDepth", properties);
                        editor.ShaderProperty(outlineDepth, "深度");
                    }
                    else
                    {
                        _material.DisableKeyword(OUTLINE_DEPTH_ON);
                    }
                }
                else
                {
                    _material.DisableKeyword(OUTLINE_ON);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
    }
    #endif
    

}
