#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "SDF.hlsl"

// float4x4 _InverseProjectionMatrix;//转换矩阵
// float4x4 _InverseViewMatrix;//转换矩阵
//
// //通过深度重建世界空间坐标
// float4 GetWorldSpacePosition(float depth, float2 uv)
// {
//     // 屏幕空间 --> 视锥空间
//     float4 view_vector = mul(_InverseProjectionMatrix, float4(2.0 * uv - 1.0, depth, 1.0));
//     view_vector.xyz /= view_vector.w;
//     //视锥空间 --> 世界空间
//     float4x4 l_matViewInv = _InverseViewMatrix;
//     float4 world_vector = mul(l_matViewInv, float4(view_vector.xyz, 1));
//     return world_vector;
// }



