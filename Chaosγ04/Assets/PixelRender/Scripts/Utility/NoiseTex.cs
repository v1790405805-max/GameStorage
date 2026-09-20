using System;
using System.IO;
using Unity.Mathematics;
using UnityEngine;
using UnityEditor;

namespace PixelRender
{
    [CreateAssetMenu(fileName = "NewNoiseTex", menuName = "PixelTool/NoiseTex")]

    public class NoiseTex : ScriptableObject
    {
        public NoiseType Type;
        public Gradient GradientColor;
        [Range(1, 200)] public float Scale;
        [HideInInspector] public bool Previews;

        private static float Frac(float value)
        {
            return value - Mathf.Floor(value);
        }

        private static float Dot(Vector2 a, Vector2 b)
        {
            return a.x * b.x + a.y * b.y;
        }

        #region 梯度噪声

        private float GradientNoise(Vector2 p)
        {
            Vector2 ip = new Vector2(Mathf.Floor(p.x), Mathf.Floor(p.y));
            Vector2 fp = new Vector2(p.x % 1, p.y % 1);
            float d00 = Vector2.Dot(GradientNoise_dir(ip), fp);
            float d01 = Vector2.Dot(GradientNoise_dir(ip + new Vector2(0, 1)), fp - new Vector2(0, 1));
            float d10 = Vector2.Dot(GradientNoise_dir(ip + new Vector2(1, 0)), fp - new Vector2(1, 0));
            float d11 = Vector2.Dot(GradientNoise_dir(ip + new Vector2(1, 1)), fp - new Vector2(1, 1));
            fp = fp * fp * fp * (fp * (fp * 6 - Vector2.one * 15) + Vector2.one * 10);
            return Mathf.Lerp(Mathf.Lerp(d00, d01, fp.y), Mathf.Lerp(d10, d11, fp.y), fp.x);
        }

        private Vector2 GradientNoise_dir(Vector2 p)
        {
            p = new Vector2(p.x % 289, p.y % 289);
            float x = (34 * p.x + 1) * p.x % 289 + p.y;
            x = (34 * x + 1) * x % 289;
            x = ((x / 41) % 1) * 2 - 1;
            return new Vector2(x - Mathf.Floor(x + 0.5f), Mathf.Abs(x) - 0.5f).normalized;
        }

        private float GetGradientNoise(Vector2 uv, float scale)
        {
            return GradientNoise(uv * scale) + 0.5f;
        }

        #endregion

        #region 白噪声

        private float unity_noise_randomValue(Vector2 uv)
        {
            return Frac(Mathf.Sin(Dot(uv, new Vector2(12.9898f, 78.233f))) * 43758.5453f);
        }

        private float unity_noise_interpolate(float a, float b, float t)
        {
            return (1.0f - t) * a + (t * b);
        }

        private float unity_valueNoise(float2 uv)
        {
            Vector2 i = new Vector2(Mathf.Floor(uv.x), Mathf.Floor(uv.y));
            Vector2 f = new Vector2(Frac(uv.x), Frac(uv.y));
            Vector2 f1 = new Vector2(f.x * f.x, f.y * f.y);
            Vector2 f2 = new Vector2(3f - 2f * f.x, 3f - 2f * f.y);
            f = new Vector2(f1.x * f2.x, f1.y * f2.y);

            uv = new Vector2(Frac(uv.x), Frac(uv.y)) - Vector2.one * 0.5f;
            uv = new Vector2(Mathf.Abs(uv.x), Mathf.Abs(uv.y));
            Vector2 c0 = i + new Vector2(0.0f, 0.0f);
            Vector2 c1 = i + new Vector2(1.0f, 0.0f);
            Vector2 c2 = i + new Vector2(0.0f, 1.0f);
            Vector2 c3 = i + new Vector2(1.0f, 1.0f);
            float r0 = unity_noise_randomValue(c0);
            float r1 = unity_noise_randomValue(c1);
            float r2 = unity_noise_randomValue(c2);
            float r3 = unity_noise_randomValue(c3);

            float bottomOfGrid = unity_noise_interpolate(r0, r1, f.x);
            float topOfGrid = unity_noise_interpolate(r2, r3, f.x);
            float t = unity_noise_interpolate(bottomOfGrid, topOfGrid, f.y);
            return t;
        }

        private float GetSimpleNoise(float2 UV, float Scale)
        {
            float t = 0.0f;

            float freq = Mathf.Pow(2.0f, 0);
            float amp = Mathf.Pow(0.5f, 3);
            t += unity_valueNoise(new Vector2(UV.x * Scale / freq, UV.y * Scale / freq)) * amp;

            freq = Mathf.Pow(2.0f, 1);
            amp = Mathf.Pow(0.5f, 2);
            t += unity_valueNoise(new Vector2(UV.x * Scale / freq, UV.y * Scale / freq)) * amp;

            freq = Mathf.Pow(2.0f, 2);
            amp = Mathf.Pow(0.5f, 1);
            t += unity_valueNoise(new Vector2(UV.x * Scale / freq, UV.y * Scale / freq)) * amp;

            return t;
        }

        #endregion




        public Color PickColor(Vector2 uv)
        {

            float noise = 0;
            switch (Type)
            {
                case NoiseType.GradientNoise:
                    noise = GetGradientNoise(uv, Scale);
                    break;
                case NoiseType.SimpleNoise:
                    noise = GetSimpleNoise(uv, Scale);
                    break;
            }

            //noise = Mathf.PerlinNoise(uv.x * (float)Scale,uv.y * (float)Scale);
            return GradientColor.Evaluate(noise);
        }

        public enum NoiseType
        {
            GradientNoise = 0,
            SimpleNoise = 1,
        }
    }

    #if UNITY_EDITOR
     [CustomEditor(typeof(NoiseTex))]
    public class NoiseTexEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var p = (NoiseTex)target;

            p.Previews = EditorGUILayout.BeginFoldoutHeaderGroup(p.Previews, "预览");

            if (p.Previews)
            {
                int previewSize = 256;
                Texture2D preview = new Texture2D(previewSize, previewSize, TextureFormat.ARGB32, false, true);

                Color[] colors = new Color[previewSize * previewSize];
                for (int i = 0; i < colors.Length; i++)
                {
                    int x = i / previewSize;
                    int y = i % previewSize;
                    var fx = (float)x / previewSize;
                    var fy = (float)y / previewSize;
                    colors[i] = SRGBToLinear(p.PickColor(new Vector2(fx, fy)));
                }

                preview.SetPixels(colors);
                preview.Apply();
                var previewRect = EditorGUILayout.GetControlRect(false, previewSize, GUILayout.Width(previewSize));
                EditorGUI.DrawPreviewTexture(previewRect, preview);
            }

            EditorGUILayout.EndFoldoutHeaderGroup();

            if (GUILayout.Button("保存图片"))
            {
                SaveTextureToFile(p);
            }

        }

        private void SaveTextureToFile(NoiseTex noiseTex)
        {
            int previewSize = 512;
            Texture2D preview = new Texture2D(previewSize, previewSize, TextureFormat.ARGB32, false, true);

            Color[] colors = new Color[previewSize * previewSize];
            for (int i = 0; i < colors.Length; i++)
            {
                int x = i / previewSize;
                int y = i % previewSize;
                var fx = (float)x / previewSize;
                var fy = (float)y / previewSize;
                colors[i] = noiseTex.PickColor(new Vector2(fx, fy));
            }

            preview.SetPixels(colors);
            preview.Apply();

            string assetPath = AssetDatabase.GetAssetPath(noiseTex);

            // 构建文件名
            string fileName = Path.GetFileNameWithoutExtension(assetPath) + ".png";
            string savePath = Path.GetDirectoryName(assetPath) + "/" + fileName;

            // 保存纹理
            byte[] bytes = preview.EncodeToPNG();
            File.WriteAllBytes(savePath, bytes);

            // 刷新 AssetDatabase 以便 Unity 能够识别新保存的文件
            AssetDatabase.Refresh();
        }

        private Color SRGBToLinear(Color srgb)
        {
            // 将sRGB色彩空间的颜色转换为线性色彩空间
            return new Color(
                SRGBToLinearChannel(srgb.r),
                SRGBToLinearChannel(srgb.g),
                SRGBToLinearChannel(srgb.b),
                srgb.a
            );
        }

        private float SRGBToLinearChannel(float srgb)
        {
            // sRGB转换到线性色彩空间
            if (srgb <= 0.04045f)
            {
                return srgb / 12.92f;
            }
            else
            {
                return Mathf.Pow((srgb + 0.055f) / 1.055f, 2.4f);
            }
        }
    }
    #endif
   
}

