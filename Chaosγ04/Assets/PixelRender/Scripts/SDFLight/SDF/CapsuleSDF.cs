using System;
using System.Collections;
using System.Collections.Generic;
using GizmoDraw;
using UnityEngine;

namespace PixelRender
{
#if UNITY_EDITOR
    [ExecuteInEditMode]
#endif
    public class CapsuleSDF : SDFLight
    {
        public float h = 1;
        public float r = 0.2f;

        // private void Start()
        // {
        //     SDFLightVolume.Instance.Register(this);
        // }
        //
        // private void OnDestroy()
        // {
        //     SDFLightVolume.Instance.Unregister(this);
        // }
        
        public override SDFGraph GetGraphType()
        {
            return SDFGraph.Capsule;
        }

        public CapsuleData GetData()
        {
            var data = new CapsuleData();
            data.LightData = GetLightData();
            data.H = h;
            data.R = r;
            return data;
        }
        
        private void OnDrawGizmos()
        {
            Gizmos.DrawIcon(transform.position, "CapsuleGizmos", true, this.Color);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = this.Color;
            Matrix4x4 defaultMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            GizmoDraw.GizmoDraw.DrawCircle(Vector3.zero, r, Axes.Y);
            GizmoDraw.GizmoDraw.DrawCircle(Vector3.up * h,r,Axes.Y);
            GizmoDraw.GizmoDraw.DrawCircle(Vector3.zero,r,Axes.X,startTheta: Mathf.PI);
            GizmoDraw.GizmoDraw.DrawCircle(Vector3.zero,r,Axes.Z,startTheta: Mathf.PI);
            GizmoDraw.GizmoDraw.DrawCircle(Vector3.up * h,r,Axes.X,endTheta: Mathf.PI);
            GizmoDraw.GizmoDraw.DrawCircle(Vector3.up * h,r,Axes.Z,endTheta: Mathf.PI);
            Gizmos.DrawLine(Vector3.right * r,Vector3.right * r + Vector3.up * h);
            Gizmos.DrawLine(Vector3.right * -r,Vector3.right * -r + Vector3.up * h);
            Gizmos.DrawLine(Vector3.forward * r,Vector3.forward * r + Vector3.up * h);
            Gizmos.DrawLine(Vector3.forward * -r,Vector3.forward * -r + Vector3.up * h);
            Gizmos.matrix = defaultMatrix;
        }
    }
}
