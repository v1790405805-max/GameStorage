using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace PixelRender
{
#if UNITY_EDITOR
    [ExecuteInEditMode]
#endif
    public class SphereSDF : SDFLight
    {
        public float s = 1;
        
        public override SDFGraph GetGraphType()
        {
            return SDFGraph.Sphere;
        }

        public SphereData GetData()
        {
            var data = new SphereData();
            data.LightData = GetLightData();
            data.S = s;
            return data;
        }

        public override Vector3 GetBound()
        {
            return s * Vector3.one;
        }

        private void OnDrawGizmos()
        {
            Gizmos.DrawIcon(transform.position, "SphereGizmos", true, this.Color);
        }
        
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = this.Color;
            //Matrix4x4 defaultMatrix = Gizmos.matrix;
            //Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireSphere(transform.position, s);
            //Gizmos.matrix = defaultMatrix;
            

        }
    }
}
