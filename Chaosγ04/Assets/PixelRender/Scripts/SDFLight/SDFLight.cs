using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PixelRender
{
    public class SDFLight : MonoBehaviour
    {
        public Color Color = Color.white;
        public float Intensity = 1;
        public bool UseNormal = false;
        
        //[LabelText("分段灯光")] 
        public bool SegmentLight = false;

        //[ShowIf("SegmentLight")] [LabelText("分段范围"), MinMaxSlider(0, 10)]
        public Vector2 SegmentRange = new Vector2(0, 1);

        //[ShowIf("SegmentLight")] [LabelText("灯光段数"), Range(1, 16)]
        public uint SegmentNum = 4;
        
        //[ShowIf("SegmentLight")] [LabelText("段数曲线"),Range(-1,1)]
        public float LightPow = 0;
        
        //[ShowIf("SegmentLight")] [LabelText("强度曲线"),Range(-1,1)]
        public float SegmentPow = 0;

        protected SDFGraph _graph;

        public virtual SDFGraph GetGraphType()
        {
            return _graph;
        }

        public virtual T GetData<T>() where T : struct
        {
            return new T();
        }

        public virtual Vector3 GetBound()
        {
            return Vector3.one;
        }

        protected LightSDFData GetLightData()
        {
            var lightPow = 1f;
            if (LightPow < 0)
            {
                lightPow = Mathf.Lerp(1, 0.2f, -LightPow);
            }
            else if(LightPow > 0)
            {
                 lightPow = Mathf.Lerp(1f, 5f, LightPow);
            }

            var segmentPow = 1f;
            if (SegmentPow < 0)
            {
                segmentPow = Mathf.Lerp(1, 0.2f, -SegmentPow);
            }
            else if(SegmentPow > 0)
            {
                segmentPow = Mathf.Lerp(1f, 5f, SegmentPow);
            }

            return new LightSDFData()
            {
                Matrix4X4 = Matrix4x4.TRS(transform.position, transform.rotation,
                    transform.localScale),
                Color = Color,
                Intensity = Intensity,
                UseNormal = UseNormal?1:0,
                LightPow = lightPow,
                SegmentPow = segmentPow,
                SegmentRange = SegmentRange,
                SegmentNum = SegmentLight ? SegmentNum : 0,
            };
        }

        private void OnDrawGizmos()
        {

        }
    }

    public enum SDFGraph
    {
        Sphere = 0,
        Box = 1,
        Torus = 2,
        Capsule = 3,
    }
}
