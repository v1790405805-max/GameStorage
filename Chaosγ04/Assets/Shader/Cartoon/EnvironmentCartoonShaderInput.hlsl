#ifndef STYLIZED_SCENE_TOON_INPUT_INCLUDED
#define STYLIZED_SCENE_TOON_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);
TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);
TEXTURE2D(_OcclusionMap);
SAMPLER(sampler_OcclusionMap);
TEXTURE2D(_EmissionMap);
SAMPLER(sampler_EmissionMap);

CBUFFER_START(UnityPerMaterial)
    half4 _BaseColor;
    float4 _BaseMap_ST;
    half _NormalScale;
    half _OcclusionStrength;
    half _VertexColorStrength;
    half _AlphaClip;
    half _Cutoff;
    half4 _ShadowColor;
    half _ShadowThreshold;
    half _ShadowSmoothness;
    half _ShadowStrength;
    half _AdditionalLightStrength;
    half4 _SpecColor;
    half _SpecThreshold;
    half _SpecSmoothness;
    half _SpecStrength;
    half4 _RimColor;
    half _RimPower;
    half _RimThreshold;
    half _RimSmoothness;
    half _RimStrength;
    half4 _EmissionColor;
    half _EnvironmentIntensity;
    half _EnvironmentReflectionIntensity;
    half _EnvironmentReflectionRoughness;
    half _EnvironmentReflectionFresnelPower;
    half _FogInfluence;
CBUFFER_END

half4 SampleAlbedo(float2 uv, half4 vertexColor)
{
    half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
    albedo.rgb *= lerp(1.0h, vertexColor.rgb, _VertexColorStrength);
    albedo.a *= lerp(1.0h, vertexColor.a, _VertexColorStrength);
    return albedo;
}

half4 SampleAlbedoAlpha(float2 uv, TEXTURE2D_PARAM(albedoAlphaMap, sampler_albedoAlphaMap))
{
    return SAMPLE_TEXTURE2D(albedoAlphaMap, sampler_albedoAlphaMap, uv);
}

half Alpha(half albedoAlpha, half4 color, half cutoff)
{
    half alpha = albedoAlpha * color.a;
    return AlphaDiscard(alpha, cutoff);
}

half SampleOcclusion(float2 uv)
{
    half occlusionSample = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv).r;
    return lerp(1.0h, occlusionSample, _OcclusionStrength);
}

half3 SampleNormalWS(float3 normalWS, float4 tangentWS, float2 uv)
{
    half4 normalSample = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv);
    half3 normalTS = UnpackNormalScale(normalSample, _NormalScale);
    half3 bitangentWS = tangentWS.w * cross(normalWS, tangentWS.xyz);
    half3x3 tangentToWorld = half3x3(tangentWS.xyz, bitangentWS, normalWS);
    return NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, tangentToWorld));
}

half3 SampleEmission(float2 uv)
{
    return SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb * _EmissionColor.rgb;
}

#endif
