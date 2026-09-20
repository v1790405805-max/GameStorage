using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PixelRender
{
    #if UNITY_EDITOR
    public class GrassShaderGUI : ShaderGUI
    {
        private bool _showColor, _showLighting, _showGrass, _showWind;

        private Material _material;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            _material = materialEditor.target as Material;
            
            EditorGUILayout.LabelField("============PixelGrass============", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            DrawMainColorGroup(materialEditor, properties);
            DrawLightingGroup(materialEditor, properties);
            DrawGrassGroup(materialEditor, properties);
            DrawWindGroup(materialEditor, properties);
            
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

                var mapTex = FindProperty("_MapTex", properties);
                editor.ShaderProperty(mapTex,"地图颜色");
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
                
                var shadowColor = FindProperty("_ShadowColor", properties);
                editor.ShaderProperty(shadowColor, "阴影颜色");

                var lightAttenuation = FindProperty("_LightAttenuation", properties);
                editor.ShaderProperty(lightAttenuation, "阴影范围");
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawGrassGroup(MaterialEditor editor, MaterialProperty[] properties)
        {
            _showGrass = EditorGUILayout.BeginFoldoutHeaderGroup(_showGrass, "草");
            if (_showGrass)
            {
                var scalex = FindProperty("_ScaleX",properties);
                editor.ShaderProperty(scalex,"x大小");
                
                var scaley = FindProperty("_ScaleY",properties);
                editor.ShaderProperty(scaley,"y大小");
                
                var angleOffset = FindProperty("_AngleOffset",properties);
                editor.ShaderProperty(angleOffset,"角度扰乱");
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        private void DrawWindGroup(MaterialEditor editor, MaterialProperty[] properties)
        {
            _showWind = EditorGUILayout.BeginFoldoutHeaderGroup(_showWind, "风");
            if (_showWind)
            {
                var windColor = FindProperty("_WindColor", properties);
                editor.ShaderProperty(windColor,"风颜色");
                
                var windAttenuation = FindProperty("_WindAttenuation", properties);
                editor.ShaderProperty(windAttenuation,"风范围");
                
                var rotateNoiseSize = FindProperty("_RotateNoiseSize", properties);
                editor.ShaderProperty(rotateNoiseSize,"摆动噪声尺寸");
                
                var rotateNoiseAngle = FindProperty("_RotateNoiseAngle", properties);
                editor.ShaderProperty(rotateNoiseAngle,"摆动噪声旋转");
                
                var rotateNoiseRatio = FindProperty("_RotateNoiseRatio", properties);
                editor.ShaderProperty(rotateNoiseRatio,"摆动噪声比例");
                
                var rotateNoiseSpeed = FindProperty("_RotateNoiseSpeed", properties);
                editor.ShaderProperty(rotateNoiseSpeed,"摆动噪声速度");
                
                var rotateNoiseInt = FindProperty("_RotateNoiseInt", properties);
                editor.ShaderProperty(rotateNoiseInt,"摆动噪声强度");
                
                
                var rotateSpeed = FindProperty("_RotateSpeed", properties);
                editor.ShaderProperty(rotateSpeed,"摆动速度");
                
                var sheetSpeed = FindProperty("_SheetSpeed", properties);
                editor.ShaderProperty(sheetSpeed,"序列帧速度");
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
    } 
    #endif
    
}


