using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PixelRender
{
#if UNITY_EDITOR
    [ExecuteInEditMode]
#endif
    public class BoxSDF : SDFLight
    {
        public Vector3 b = new Vector3(1, 1, 1);
        public float r = 0;


        public override SDFGraph GetGraphType()
        {
            return SDFGraph.Box;
        }

        public BoxData GetData()
        {
            var data = new BoxData();
            data.LightData = GetLightData();
            data.B = b;
            data.R = r;
            return data;
        }

        public override Vector3 GetBound()
        {
            return b;
        }

        private void OnDrawGizmos()
        {
            Gizmos.DrawIcon(transform.position, "BoxGizmos", true, this.Color);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = this.Color;
            Matrix4x4 defaultMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, b * 2);
            Gizmos.matrix = defaultMatrix;
        }
    }
}
