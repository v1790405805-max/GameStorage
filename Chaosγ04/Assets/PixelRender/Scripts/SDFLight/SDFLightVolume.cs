using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PixelRender
{
    [ExecuteInEditMode]

    public class SDFLightVolume : MonoBehaviour
    {
        public static SDFLightVolume Instance;

        private List<SDFLight> _sdfItems = new List<SDFLight>();
        

        private void Start()
        {
            Instance = this;
        }

        private void Update()
        {
            Instance = this;
            var objs = GameObject.FindGameObjectsWithTag("SDFLight");
            _sdfItems.Clear();
            foreach (var obj in objs)
            {
                if (obj.TryGetComponent<SDFLight>(out var sdfLight))
                {
                    _sdfItems.Add(sdfLight);
                }
            }
        }

        public SphereData[] GetSphereDatas()
        {
            var datas = new List<SphereData>();
            foreach (var sdf in _sdfItems)
            {
                if (sdf.GetGraphType() == SDFGraph.Sphere)
                {
                    datas.Add((sdf as SphereSDF).GetData());
                }
            }

            return datas.ToArray();
        }

        public BoxData[] GetBoxDatas()
        {
            var datas = new List<BoxData>();
            foreach (var sdf in _sdfItems)
            {
                if (sdf.GetGraphType() == SDFGraph.Box)
                {
                    datas.Add((sdf as BoxSDF).GetData());
                }
            }

            return datas.ToArray();
        }

        public TorusData[] GetTorusDatas()
        {
            var datas = new List<TorusData>();
            foreach (var sdf in _sdfItems)
            {
                if (sdf.GetGraphType() == SDFGraph.Torus)
                {
                    datas.Add((sdf as TorusSDF).GetData());
                }
            }

            return datas.ToArray();
        }

        public CapsuleData[] GetCapsuleDatas()
        {
            var datas = new List<CapsuleData>();
            foreach (var sdf in _sdfItems)
            {
                if (sdf.GetGraphType() == SDFGraph.Capsule)
                {
                    datas.Add((sdf as CapsuleSDF).GetData());
                }
            }

            return datas.ToArray();
        }
    }

    public struct LightSDFData
    {
        public Matrix4x4 Matrix4X4;
        public float Intensity;
        public Color Color;
        public Vector2 SegmentRange;
        public uint SegmentNum;
        public int UseNormal;
        public float LightPow;
        public float SegmentPow;
    }

//考虑到每个sdf图形参数都不同，所以就单独写data，而不是写通用data
//考虑到sdf图形过多，就做几个常用的吧
    public struct SphereData
    {
        public LightSDFData LightData;
        public float S;
    }

    public struct BoxData
    {
        public LightSDFData LightData;
        public Vector3 B;
        public float R;
    }

    public struct TorusData
    {
        public LightSDFData LightData;
        public Vector3 T;
    }

    public struct CapsuleData
    {
        public LightSDFData LightData;
        public float H;
        public float R;
    }

}