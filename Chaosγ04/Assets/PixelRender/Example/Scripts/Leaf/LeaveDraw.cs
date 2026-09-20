using System;
using System.Collections;
using System.Collections.Generic;
using PixelRender;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public struct GPULeaf_Draw
{
    public Vector3 Pos;
    public float Scale;
    public Vector3 Normal;
    public float AngleOffset;
    public float SheetOffset;
}

public class LeaveDraw : MonoBehaviour
{
    public List<GameObject> Objs;
    public Mesh LeafMesh;
    public Material LeafMat;
    public bool MatInstance = true;
    public Vector2 ScaleRange = new Vector2(0.9f,1.1f);
    public float Ratio = 1;
    public Vector3 BoundSize = new Vector3(1, 1, 1);

    public static GPULeaf_Draw[] _leafDatas;//所有草的数据
    private ComputeBuffer _leafBuffer;
    private ComputeBuffer _drawArgsBuffer;
    private MaterialPropertyBlock _props;
    private readonly int _bufferID = Shader.PropertyToID("_LeafBuffer");
    private readonly int _positionID = Shader.PropertyToID("_position");
    private Bounds _bounds;
    private void Start()
    {
        var leafDatas = new List<GPULeaf_Draw>();
        _bounds = new Bounds(transform.position,BoundSize);
        foreach (var obj in Objs)
        {
            var filter = obj.GetComponent<MeshFilter>();
            var terrianMesh = filter.mesh;
            var normals = terrianMesh.normals;
            var vertices = terrianMesh.vertices;

            for (var i = 0; i < vertices.Length; i++)
            {
                if(Random.Range(0,1f)>Ratio)
                    continue;
                var gpuData = new GPULeaf_Draw()
                {
                    Pos = obj.transform.TransformPoint(vertices[i]) ,
                    Scale =  Random.Range(ScaleRange.x,ScaleRange.y),
                    Normal = obj.transform.TransformVector(normals[i]).normalized,
                    AngleOffset = Random.Range(-1f,1f),
                    SheetOffset = Random.Range(0,1f),
                };
                leafDatas.Add(gpuData);
            }
        }
        _leafDatas = leafDatas.ToArray();
        
        
        //初始化缓存
        _drawArgsBuffer = new ComputeBuffer(
            1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments
        );
        _drawArgsBuffer.SetData(new uint[5] {
            LeafMesh.GetIndexCount(0), (uint) _leafDatas.Length, 0, 0, 0
        });
        //这个属性的作用是避免实例错误(具体没测试过，反正参考代码是这么写的)
        _props = new MaterialPropertyBlock();
        _props.SetFloat("_UniqueID", Random.value);

        _leafBuffer = new ComputeBuffer(_leafDatas.Length, 36);//(1+3+1+3+1+1) * 4
        _leafBuffer.SetData(_leafDatas);

        if(MatInstance)
            LeafMat = Instantiate(LeafMat);
    }
    private void Update()
    {
        LeafMat.SetVector(_positionID,transform.position);
        LeafMat.SetBuffer(_bufferID, _leafBuffer);
        Graphics.DrawMeshInstancedIndirect(LeafMesh,0,LeafMat,
            _bounds,_drawArgsBuffer,0,_props);
    }
    

    private void OnDestroy()
    {
        if(_leafBuffer != null)_leafBuffer.Release();
        if(_drawArgsBuffer != null)_drawArgsBuffer.Release();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position, BoundSize);
    }
}
