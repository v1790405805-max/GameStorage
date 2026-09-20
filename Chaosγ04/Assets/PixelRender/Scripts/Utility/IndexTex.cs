using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PixelRender
{
    [CreateAssetMenu(fileName = "NewIndexTex", menuName = "PixelTool/IndexTex")]

    public class IndexTex : ScriptableObject
    {
        [HideInInspector] public Color m00;
        [HideInInspector] public Color m01;
        [HideInInspector] public Color m02;
        [HideInInspector] public Color m03;
        [HideInInspector] public Color m10;
        [HideInInspector] public Color m11;
        [HideInInspector] public Color m12;
        [HideInInspector] public Color m13;
        [HideInInspector] public Color m20;
        [HideInInspector] public Color m21;
        [HideInInspector] public Color m22;
        [HideInInspector] public Color m23;
        [HideInInspector] public Color m30;
        [HideInInspector] public Color m31;
        [HideInInspector] public Color m32;
        [HideInInspector] public Color m33;

        public Color GetColor(int index)
        {
            switch (index)
            {
                case 0: return m00;
                case 1: return m01;
                case 2: return m02;
                case 3: return m03;
                case 4: return m10;
                case 5: return m11;
                case 6: return m12;
                case 7: return m13;
                case 8: return m20;
                case 9: return m21;
                case 10: return m22;
                case 11: return m23;
                case 12: return m30;
                case 13: return m31;
                case 14: return m32;
                case 15: return m33;
                default: return Color.black;
            }


        }
    }

    #if UNITY_EDITOR
    [CustomEditor(typeof(IndexTex))]
        public class IndexTexEditor : Editor
        {
            public override void OnInspectorGUI()
            {
                base.OnInspectorGUI();
                var p = (IndexTex)target;
                serializedObject.Update();
                EditorGUILayout.LabelField("索引贴图", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                var m30 = EditorGUILayout.ColorField(p.m30);
                var m31 = EditorGUILayout.ColorField(p.m31);
                var m32 = EditorGUILayout.ColorField(p.m32);
                var m33 = EditorGUILayout.ColorField(p.m33);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                var m20 = EditorGUILayout.ColorField(p.m20);
                var m21 = EditorGUILayout.ColorField(p.m21);
                var m22 = EditorGUILayout.ColorField(p.m22);
                var m23 = EditorGUILayout.ColorField(p.m23);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                var m10 = EditorGUILayout.ColorField(p.m10);
                var m11 = EditorGUILayout.ColorField(p.m11);
                var m12 = EditorGUILayout.ColorField(p.m12);
                var m13 = EditorGUILayout.ColorField(p.m13);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                var m00 = EditorGUILayout.ColorField(p.m00);
                var m01 = EditorGUILayout.ColorField(p.m01);
                var m02 = EditorGUILayout.ColorField(p.m02);
                var m03 = EditorGUILayout.ColorField(p.m03);
                EditorGUILayout.EndHorizontal();

                serializedObject.FindProperty("m00").colorValue = m00;
                serializedObject.FindProperty("m01").colorValue = m01;
                serializedObject.FindProperty("m02").colorValue = m02;
                serializedObject.FindProperty("m03").colorValue = m03;
                serializedObject.FindProperty("m10").colorValue = m10;
                serializedObject.FindProperty("m11").colorValue = m11;
                serializedObject.FindProperty("m12").colorValue = m12;
                serializedObject.FindProperty("m13").colorValue = m13;
                serializedObject.FindProperty("m20").colorValue = m20;
                serializedObject.FindProperty("m21").colorValue = m21;
                serializedObject.FindProperty("m22").colorValue = m22;
                serializedObject.FindProperty("m23").colorValue = m23;
                serializedObject.FindProperty("m30").colorValue = m30;
                serializedObject.FindProperty("m31").colorValue = m31;
                serializedObject.FindProperty("m32").colorValue = m32;
                serializedObject.FindProperty("m33").colorValue = m33;

                serializedObject.ApplyModifiedProperties();

                if (GUILayout.Button("保存图片"))
                {
                    SaveTextureToFile(p);
                }
            }

            private void SaveTextureToFile(IndexTex indexTex)
            {
                int size = 64;
                Texture2D preview = new Texture2D(size, size, TextureFormat.ARGB32, false, true);

                Color[] colors = new Color[size * size];
                for (int i = 0; i < colors.Length; i++)
                {
                    int x = i % size / 16;
                    int y = i / size / 16;
                    colors[i] = indexTex.GetColor(x + y * 4);
                }

                preview.SetPixels(colors);
                preview.Apply();

                string assetPath = AssetDatabase.GetAssetPath(indexTex);

                // 构建文件名
                string fileName = Path.GetFileNameWithoutExtension(assetPath) + ".png";
                string savePath = Path.GetDirectoryName(assetPath) + "/" + fileName;

                // 保存纹理
                byte[] bytes = preview.EncodeToPNG();
                File.WriteAllBytes(savePath, bytes);

                // 刷新 AssetDatabase 以便 Unity 能够识别新保存的文件
                AssetDatabase.Refresh();
            }
        }
    #endif
}
   
    
