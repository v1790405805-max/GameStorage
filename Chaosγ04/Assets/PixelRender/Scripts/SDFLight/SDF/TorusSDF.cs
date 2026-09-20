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
    public class TorusSDF : SDFLight
    {
        public Vector3 t = Vector3.one;

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
            return SDFGraph.Torus;
        }

        public TorusData GetData()
        {
            var data = new TorusData();
            data.LightData = GetLightData();
            data.T = t;
            return data;
        }

        private void OnDrawGizmos()
        {
            Gizmos.DrawIcon(transform.position, "TorusGizmos", true, this.Color);
        }
        
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = this.Color;
            Matrix4x4 defaultMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            //GizmoDraw.GizmoDraw.DrawCircle(Vector3.zero, t.x, Axes.Y,lineNum:60);
            GizmoDraw.GizmoDraw.DrawCircle(Vector3.zero, t.x - t.y, Axes.Y,lineNum:60);
            GizmoDraw.GizmoDraw.DrawCircle(Vector3.zero, t.x + t.y, Axes.Y,lineNum:60);
            GizmoDraw.GizmoDraw.DrawCircle(Vector3.zero + Vector3.up * t.y, t.x, Axes.Y,lineNum:60);
            GizmoDraw.GizmoDraw.DrawCircle(Vector3.zero + Vector3.down * t.y, t.x, Axes.Y,lineNum:60);
            GizmoDraw.GizmoDraw.DrawCircle(Vector3.zero + Vector3.right * t.x, t.y,Axes.Z);
            GizmoDraw.GizmoDraw.DrawCircle(Vector3.zero - Vector3.right * t.x, t.y,Axes.Z);
            GizmoDraw.GizmoDraw.DrawCircle(Vector3.zero + Vector3.forward * t.x, t.y,Axes.X);
            GizmoDraw.GizmoDraw.DrawCircle(Vector3.zero - Vector3.forward * t.x, t.y,Axes.X);
            Gizmos.matrix = defaultMatrix;
        }
    }
}
