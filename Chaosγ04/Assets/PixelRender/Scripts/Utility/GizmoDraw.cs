using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GizmoDraw
{
    public class GizmoDraw
    {
        /// <summary>
        /// 绘制圆
        /// </summary>
        /// <param name="center">中心点</param>
        /// <param name="radius">半径</param>
        /// <param name="axes">轴向</param>
        /// <param name="thetaStep">步进角度</param>
        public static void DrawCircle(Vector3 center, float radius,Axes axes = Axes.X, float startTheta = 0, float endTheta = 2 * Mathf.PI ,float lineNum = 30)
        {
            var axesX = Vector3.forward;
            var axesY = Vector3.up;
            switch (axes)
            {
                case Axes.Y:
                {
                    axesX = Vector3.right;
                    axesY = Vector3.forward;
                }
                    break;
                case Axes.Z:
                {
                    axesX = Vector3.right;
                    axesY = Vector3.up;   
                }
                    break;
                    
            }
            var beginPoint = Vector3.zero;
            var firstPoint = Vector3.zero;
            for(int i=0; i<= lineNum;i++)
            {
                var theta = Mathf.Lerp(startTheta, endTheta, (float)i / (float)lineNum);
                var x = radius * Mathf.Cos(theta);
                var y = radius * Mathf.Sin(theta);
                var endPoint = axesX * x + axesY * y + center;
                if (theta == 0)
                {
                    firstPoint = endPoint;
                }
                else
                {
                    Gizmos.DrawLine(beginPoint,endPoint);
                }
                beginPoint = endPoint;
            }
        }
    }

    public enum Axes
    {
        X = 0,
        Y,
        Z,
    }
}

