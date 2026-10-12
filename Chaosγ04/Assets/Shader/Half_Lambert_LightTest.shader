Shader "Custom/HalfLambert_Gooch_Combined"
{
    Properties
    {
        [MainColor] _MainColor ("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        
        [Header(Half Lambert Ramp)]
        _ShadowThreshold ("Threshold", Range(0, 1)) = 0.5   
        _Smoothness ("Shadow Transition Smoothness", Range(0.001, 0.2)) = 0.01   

        [Header(Gooch Cool Warm Tone)]
        _CoolColor ("Cool Color", Color) = (0.1, 0.1, 0.35, 1)  
        _WarmColor ("Warm Color", Color) = (0.85, 0.7, 0.4, 1)   
        _CoolFactor ("Cool Factor", Range(0, 1)) = 0.3                    
        _WarmFactor ("Warm Factor", Range(0, 1)) = 0.2                    

        [Header(Specular Highlight)]
        [HDR] _SpecColor ("Specular Color", Color) = (1, 1, 1, 1)
        _SpecGloss ("Specular Glossiness / Power", Range(1, 256)) = 32
        _SpecSmoothness ("Specular Smoothness (Toon)", Range(0.001, 0.2)) = 0.02
        
        [Header(Fresnel And Rim Light)]
        [HDR] _RimColor ("Rim / Fresnel Color", Color) = (1, 1, 1, 1)
        _RimPower ("Rim / Fresnel Power", Range(0.1, 10)) = 3.0
        _RimThreshold ("Rim Threshold", Range(0, 1)) = 0.5
        _RimSmoothness ("Rim Smoothness", Range(0.001, 0.2)) = 0.05
        
        [Header(Environment)]
        _EnvironmentIntensity ("Environment Intensity", Range(0, 2)) = 1.0               
    }
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fragment _ _AMBIENT_PROBE_OCCLUSION
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 normalWS     : TEXCOORD0;
                float2 uv           : TEXCOORD1;
                float3 positionWS   : TEXCOORD2;
                half fogFactor      : TEXCOORD3;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _MainColor;
                float4 _BaseMap_ST;
                half _ShadowThreshold;
                half _Smoothness;

                half4 _CoolColor;
                half4 _WarmColor;
                half _CoolFactor;
                half _WarmFactor;

                half4 _SpecColor;
                half _SpecGloss;
                half _SpecSmoothness;

                half4 _RimColor;
                half _RimPower;
                half _RimThreshold;
                half _RimSmoothness;

                half _EnvironmentIntensity;
            CBUFFER_END

            float GetOrthographicFogFactor(float3 positionWS, float clipZ)
            {
                #if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
                    if (unity_OrthoParams.w == 1.0)
                    {
                        float eyeDepth = -TransformWorldToView(positionWS).z;
                        float deviceDepth = (eyeDepth - _ProjectionParams.y) / (_ProjectionParams.z - _ProjectionParams.y);
                        
                        #if UNITY_REVERSED_Z
                            deviceDepth = 1.0 - deviceDepth;
                        #endif

                        float eyeZ = (deviceDepth - _ZBufferParams.w) / _ZBufferParams.z;
                        return ComputeFogFactor(eyeZ);
                    }
                    else
                    {
                        return ComputeFogFactor(clipZ);
                    }
                #else
                    return 0.0;
                #endif
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap); 

                output.fogFactor = GetOrthographicFogFactor(positionInputs.positionWS, positionInputs.positionCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 1. 获取基础向量与光源
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half3 N = normalize(input.normalWS);
                half3 L = normalize(mainLight.direction);
                half3 V = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 H = normalize(L + V);

                // 2. 半兰伯特与阴影衰减计算
                half NdotL = dot(N, L);
                half halfLambert = NdotL * 0.5 + 0.5;
                half finalRampInput = halfLambert * mainLight.shadowAttenuation;

                // 3. 生成卡漫分界 Mask
                half rampMask = smoothstep(_ShadowThreshold - _Smoothness, _ShadowThreshold + _Smoothness, finalRampInput);

                // 4. 高光计算 (Blinn-Phong + 卡漫切分)
                half NdotH = saturate(dot(N, H));
                half specIntensity = pow(NdotH, _SpecGloss);
                half specMask = smoothstep(0.5 - _SpecSmoothness, 0.5 + _SpecSmoothness, specIntensity);
                half3 specular = _SpecColor.rgb * specMask * mainLight.shadowAttenuation * mainLight.color;

                // 5. 菲涅尔与边缘光计算 (Rim Light / Fresnel)
                half NdotV = saturate(dot(N, V));
                half fresnel = pow(1.0 - NdotV, _RimPower);
                half rimMask = smoothstep(_RimThreshold - _RimSmoothness, _RimThreshold + _RimSmoothness, fresnel);
                half3 rimLight = _RimColor.rgb * rimMask * mainLight.shadowAttenuation;

                // 6. 采样固有色
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _MainColor;

                // 7. 使用 lerp 控制 Gooch 冷暖色彩覆盖比例
                half3 coolTone = lerp(albedo.rgb, _CoolColor.rgb, _CoolFactor);
                half3 warmTone = lerp(albedo.rgb, _WarmColor.rgb, _WarmFactor);

                // 8. 采样环境光 (Skybox Ambient / Light Probe)
                half3 envColor = SampleSH(N) * _EnvironmentIntensity;

                // 9. 优化后的暗部与亮部颜色计算
                // 暗部：同时包含 Gooch 冷色调与 Skybox 环境色
                half3 shadowColor = coolTone * envColor;
                // 亮部：直接叠加主光源与完整环境光
                half3 litColor = warmTone * mainLight.color + (albedo.rgb * envColor);

                // 10. 混合基础漫反射、高光与边缘光
                half3 baseLighting = lerp(shadowColor, litColor, rampMask);
                half3 finalColor = saturate(baseLighting + specular + rimLight);

                // 11. 混合 Fog
                finalColor = MixFog(finalColor, input.fogFactor);

                return half4(finalColor, albedo.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"

            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }


        // 在 ShadowCaster Pass 后面添加以下 DepthOnly Pass
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            // 引入 URP 标准深度 Pass 实现
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On

            HLSLPROGRAM
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }

    }
    CustomEditor "CustomGoochShaderGUI"
}