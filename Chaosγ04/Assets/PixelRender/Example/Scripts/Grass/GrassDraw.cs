using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace PixelRender
{
    public struct GPUGrass_Draw
    {
        public Vector3 Pos;
        public float Scale;
        public Vector3 Normal;
        public float SheetOffset;
        public float AngleOffset;
    }

    public class GrassDraw : MonoBehaviour
    {
        public List<GameObject> Objs;
        public Mesh GrassMesh;
        public Material GrassMat;
        [Range(1,200)]
        public float GrassCount = 10;
        public Vector3 BoundSize = new Vector3(1, 1, 1);
        private GPUGrass_Draw[] _grassDatas;
        private ComputeBuffer _grassBuffer;
        private ComputeBuffer _drawArgsBuffer;
        private MaterialPropertyBlock _props;
        private readonly int _bufferID = Shader.PropertyToID("_GrassBuffer");
        private Bounds _bounds;

        private void Start()
        {
            var grassDatas = new List<GPUGrass_Draw>();
            _bounds = new Bounds(transform.position,BoundSize);
            foreach (var obj in Objs)
            {
                var filter = obj.GetComponent<MeshFilter>();
                var terrianMesh = filter.mesh;
                var indices = terrianMesh.triangles;
                var vertices = terrianMesh.vertices;
                for (var j = 0; j < indices.Length / 3; j++)
                {
                    var index1 = indices[j * 3];
                    var index2 = indices[j * 3 + 1];
                    var index3 = indices[j * 3 + 2];
                    var v1 = vertices[index1];
                    var v2 = vertices[index2];
                    var v3 = vertices[index3];

                    //面得到法向
                    var normal = GrassUtil.GetFaceNormal(v1, v2, v3);
                    normal = obj.transform.TransformDirection(normal).normalized;

                    //计算up到faceNormal的旋转四元数
                    var upToNormal = Quaternion.FromToRotation(Vector3.up, normal);

                    // v1 = obj.transform.TransformPoint(v1);
                    // v2 = obj.transform.TransformPoint(v2);
                    // v3 = obj.transform.TransformPoint(v3);

                    //三角面积
                    var arena = GrassUtil.GetAreaOfTriangle(v1, v2, v3);

                    //计算在该三角面中，需要种植的数量
                    var countPerTriangle = GrassCount * arena;
                    if (countPerTriangle < 1)
                    {
                        countPerTriangle = Random.Range(0f,1f) < GrassCount ? 1 : 0;
                    }


                    for (var i = 0; i < countPerTriangle; i++)
                    {

                        var positionInTerrian = GrassUtil.RandomPointInsideTriangle(v1, v2, v3);
                        float rot = Random.Range(0, 180);
                        var dir = (positionInTerrian - Vector3.zero);
                        dir = obj.transform.TransformPoint(positionInTerrian);

                        var gpuData = new GPUGrass_Draw()
                        {
                            Pos = dir,
                            Scale =  Random.Range(0.7f,1.2f),
                            Normal = normal,
                            AngleOffset = Random.Range(-1f,1f),
                            SheetOffset = Random.Range(0,1f),
                        };
                    
                        grassDatas.Add(gpuData);
                    }
                }
            }

            _grassDatas = grassDatas.ToArray();
            
            //初始化缓存
            _drawArgsBuffer = new ComputeBuffer(
                1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments
            );
            _drawArgsBuffer.SetData(new uint[5] {
                GrassMesh.GetIndexCount(0), (uint) _grassDatas.Length, 0, 0, 0
            });
            //这个属性的作用是避免实例错误(具体没测试过，反正参考代码是这么写的)
            _props = new MaterialPropertyBlock();
            _props.SetFloat("_UniqueID", Random.value);

            _grassBuffer = new ComputeBuffer(_grassDatas.Length, 36);//(3+1+3++1+1) * 4
            _grassBuffer.SetData(_grassDatas);

        }
        
        private void Update()
        {
            GrassMat.SetBuffer(_bufferID, _grassBuffer);
            Graphics.DrawMeshInstancedIndirect(GrassMesh,0,GrassMat,
                _bounds,_drawArgsBuffer,0,_props);
        }
        
        private void OnDestroy()
        {
            if(_grassBuffer != null)_grassBuffer.Release();
            if(_drawArgsBuffer != null)_drawArgsBuffer.Release();
        }
        
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position, BoundSize);
        }
    }
}


