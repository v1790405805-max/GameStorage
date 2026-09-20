Shader "Custom/URP_CameraSandDust"
{
    Properties
    {
        _SandColor ("Sand Color", Color) = (0.76, 0.62, 0.42, 1.0)
        _AlphaMultiplier ("Global Intensity", Range(0, 2)) = 0.6

        _MainTex ("Main Dust Noise (Grayscale)", 2D) = "white" {}
        _DistortTex ("Distortion Flow Noise (Grayscale)", 2D) = "bump" {}

        _Speed1 ("Layer 1 Speed (XY)", Vector) = (0.15, 0.05, 0, 0)
        _Tiling1 ("Layer 1 Tiling (XY)", Vector) = (1.5, 1.5, 0, 0)

        _Speed2 ("Layer 2 Speed (XY)", Vector) = (-0.08, 0.12, 0, 0)
        _Tiling2 ("Layer 2 Tiling (XY)", Vector) = (2.2, 2.2, 0, 0)

        _DistortStrength ("Distortion Strength", Range(0, 0.2)) = 0.04
        _DistortSpeed ("Distortion Speed (XY)", Vector) = (0.05, 0.08, 0, 0)

        _EdgeFade ("Edge Softness", Range(0.01, 0.5)) = 0.2
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent+100" 
            "RenderPipeline" = "UniversalPipeline" 
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        ZTest Always

        Pass
        {
            Name "SandPass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_DistortTex);
            SAMPLER(sampler_DistortTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _SandColor;
                float _AlphaMultiplier;

                float4 _MainTex_ST;
                float4 _DistortTex_ST;

                float4 _Speed1;
                float4 _Tiling1;

                float4 _Speed2;
                float4 _Tiling2;

                float _DistortStrength;
                float4 _DistortSpeed;

                float _EdgeFade;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                // 1. 扰动计算
                float2 distortUV = uv * _DistortTex_ST.xy + _Time.y * _DistortSpeed.xy;
                half2 distortion = (SAMPLE_TEXTURE2D(_DistortTex, sampler_DistortTex, distortUV).rg * 2.0 - 1.0) * _DistortStrength;

                // 2. 双层流动采样
                float2 uvLayer1 = (uv + distortion) * _Tiling1.xy + _Time.y * _Speed1.xy;
                float2 uvLayer2 = (uv - distortion * 0.5) * _Tiling2.xy + _Time.y * _Speed2.xy;

                half sample1 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvLayer1).r;
                half sample2 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvLayer2).r;

                half blendedSand = sample1 * sample2 * 2.0;

                // 3. 屏幕四周边缘渐变
                float2 edgeFactor = smoothstep(0.0, _EdgeFade, uv) * smoothstep(0.0, _EdgeFade, 1.0 - uv);
                float edgeMask = edgeFactor.x * edgeFactor.y;

                // 4. 颜色与 Alpha
                half finalAlpha = blendedSand * edgeMask * _SandColor.a * _AlphaMultiplier;

                return half4(_SandColor.rgb, saturate(finalAlpha));
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}