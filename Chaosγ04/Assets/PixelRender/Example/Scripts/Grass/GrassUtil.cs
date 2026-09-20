using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PixelRender
{
    //用于根据Mesh获取随机顶点(主要用于生成随机化草)
    public class GrassUtil
    {
        public static List<Vector3> RandomPointsInMesh(Mesh mesh,int count)
        {
            var points = new List<Vector3>();
            var indices = mesh.triangles;
            var vertices = mesh.vertices;
            for (int j = 0; j < indices.Length / 3; j++)
            {
                var index1 = indices[j * 3];
                var index2 = indices[j * 3 + 1];
                var index3 = indices[j * 3 + 2];
                var v1 = vertices[index1];
                var v2 = vertices[index2];
                var v3 = vertices[index3];
                //三角面积
                var arena = GrassUtil.GetAreaOfTriangle(v1, v2, v3);
                //计算在该三角面中，需要种植的数量
                var countPerTriangle = Mathf.Max(1, count * arena);
                for (int i = 0; i < countPerTriangle; i++)
                {
                    var positionInTerrian = GrassUtil.RandomPointInsideTriangle(v1, v2, v3);
                    points.Add(positionInTerrian);
                }
            }
            return points;
        }
        
        /// <summary>
        /// 三角形内部，取平均分布的随机点
        /// </summary>
        public static Vector3 RandomPointInsideTriangle(Vector3 p1,Vector3 p2,Vector3 p3){
            var x = Random.Range(0,1f);
            var y = Random.Range(0,1f);
            if(y > 1 - x){
                //如果随机到了右上区域，那么反转到左下
                var temp = y;
                y = 1 - x;
                x = 1 - temp;
            }
            var vx = p2 - p1;
            var vy = p3 - p1;
            return p1 + x * vx + y * vy;
        }


        //计算三角形面积
        public static float GetAreaOfTriangle(Vector3 p1,Vector3 p2,Vector3 p3){
            var vx = p2 - p1;
            var vy = p3 - p1;
            var dotvxy = Vector3.Dot(vx,vy);
            var sqrArea = vx.sqrMagnitude * vy.sqrMagnitude -  dotvxy * dotvxy;
            return 0.5f * Mathf.Sqrt(sqrArea);
        }

        //得到面的法向
        public static Vector3 GetFaceNormal(Vector3 p1,Vector3 p2,Vector3 p3){
            var vx = p2 - p1;
            var vy = p3 - p1;
            return Vector3.Cross(vx,vy);
        }
    }
}

    

