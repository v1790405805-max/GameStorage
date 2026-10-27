Shader "Custom/EnvironmentCartoonShader"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [Normal] _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalScale ("Normal Scale", Range(-2, 2)) = 1
        [NoScaleOffset] _OcclusionMap ("Occlusion Map", 2D) = "white" {}
        _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 1
        _VertexColorStrength ("Vertex Color Strength", Range(0, 1)) = 0

        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clip", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5

        _ShadowColor ("Shadow Color", Color) = (0.55, 0.58, 0.72, 1)
        _ShadowThreshold ("Shadow Threshold", Range(0, 1)) = 0.5
        _ShadowSmoothness ("Shadow Smoothness", Range(0.001, 0.25)) = 0.04
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 1
        _AdditionalLightStrength ("Additional Light Strength", Range(0, 2)) = 1

        [HDR] _SpecColor ("Specular Color", Color) = (1, 1, 1, 1)
        _SpecThreshold ("Specular Threshold", Range(0.5, 0.999)) = 0.92
        _SpecSmoothness ("Specular Smoothness", Range(0.001, 0.2)) = 0.025
        _SpecStrength ("Specular Strength", Range(0, 2)) = 0.5

        [HDR] _RimColor ("Rim Color", Color) = (1, 1, 1, 1)
        _RimPower ("Rim Power", Range(0.1, 10)) = 4
        _RimThreshold ("Rim Threshold", Range(0, 1)) = 0.55
        _RimSmoothness ("Rim Smoothness", Range(0.001, 0.2)) = 0.04
        _RimStrength ("Rim Strength", Range(0, 2)) = 0.5

        [NoScaleOffset] _EmissionMap ("Emission Map", 2D) = "white" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)

        _EnvironmentIntensity ("Environment Intensity", Range(0, 2)) = 1
        _EnvironmentReflectionIntensity ("Environment Reflection Intensity", Range(0, 2)) = 0.25
        _EnvironmentReflectionRoughness ("Environment Reflection Roughness", Range(0.01, 1)) = 0.65
        _EnvironmentReflectionFresnelPower ("Environment Reflection Fresnel Power", Range(0.1, 10)) = 4
        _FogInfluence ("Fog Influence", Range(0, 1)) = 1
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

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature_local_fragment _ALPHATEST_ON

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ROTATION

            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            #include "EnvironmentCartoonShaderInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS       : POSITION;
                float3 normalOS         : NORMAL;
                float4 tangentOS        : TANGENT;
                float2 uv               : TEXCOORD0;
                float2 staticLightmapUV : TEXCOORD1;
                half4 color             : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 normalWS     : TEXCOORD0;
                float4 tangentWS    : TEXCOORD1;
                float2 uv           : TEXCOORD2;
                float3 positionWS   : TEXCOORD3;
                half fogFactor      : TEXCOORD4;
                half4 color         : TEXCOORD5;
            #if defined(LIGHTMAP_ON)
                float2 lightmapUV   : TEXCOORD6;
            #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float GetSceneFogFactor(float3 positionWS, float clipZ)
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

                return ComputeFogFactor(clipZ);
            #else
                return 0.0;
            #endif
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.tangentWS = half4(normalInputs.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color;
                output.fogFactor = GetSceneFogFactor(positionInputs.positionWS, positionInputs.positionCS.z);

            #if defined(LIGHTMAP_ON)
                output.lightmapUV = input.staticLightmapUV * unity_LightmapST.xy + unity_LightmapST.zw;
            #endif

                return output;
            }

            half4 GetShadowMask(Varyings input)
            {
            #if defined(SHADOWS_SHADOWMASK) && defined(LIGHTMAP_ON)
                return SAMPLE_SHADOWMASK(input.lightmapUV);
            #elif !defined(LIGHTMAP_ON)
                return unity_ProbesOcclusion;
            #else
                return half4(1, 1, 1, 1);
            #endif
            }

            half3 GetBakedLighting(Varyings input, half3 normalWS)
            {
            #if defined(LIGHTMAP_ON)
                return SampleLightmap(input.lightmapUV, normalWS);
            #else
                return SampleSH(normalWS);
            #endif
            }

            half EvaluateToonRamp(half dotNL)
            {
                half halfLambert = dotNL * 0.5 + 0.5;
                return smoothstep(
                    _ShadowThreshold - _ShadowSmoothness,
                    _ShadowThreshold + _ShadowSmoothness,
                    halfLambert);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 albedo = SampleAlbedo(input.uv, input.color);

            #if defined(_ALPHATEST_ON)
                clip(albedo.a - _Cutoff);
            #endif

                half occlusion = SampleOcclusion(input.uv);
                half3 normalWS = SampleNormalWS(input.normalWS, input.tangentWS, input.uv);
                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                half4 shadowMask = GetShadowMask(input);
                Light mainLight = GetMainLight(shadowCoord, input.positionWS, shadowMask);

                half3 lightDirWS = normalize(mainLight.direction);
                half mainVisibility = mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                half mainRamp = EvaluateToonRamp(dot(normalWS, lightDirWS)) * mainVisibility;

                half3 bakedGI = GetBakedLighting(input, normalWS);
                half shadowAmount = (1.0h - mainRamp) * _ShadowStrength;
                half3 ambientAlbedo = lerp(albedo.rgb, albedo.rgb * _ShadowColor.rgb, shadowAmount);
                half3 indirectDiffuse = ambientAlbedo * bakedGI * _EnvironmentIntensity;
                half3 directDiffuse = albedo.rgb * mainLight.color * mainRamp;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                half3 additionalLights = 0;
                uint additionalLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(additionalLightCount)
                    Light additionalLight = GetAdditionalLight(lightIndex, input.positionWS, shadowMask);
                    half additionalVisibility = additionalLight.distanceAttenuation * additionalLight.shadowAttenuation;
                    half additionalRamp = EvaluateToonRamp(dot(normalWS, additionalLight.direction)) * additionalVisibility;
                    additionalLights += albedo.rgb * additionalLight.color * additionalRamp * _AdditionalLightStrength;
                LIGHT_LOOP_END

                half3 halfDir = normalize(lightDirWS + viewDirWS);
                half specularValue = saturate(dot(normalWS, halfDir));
                half specularMask = smoothstep(
                    _SpecThreshold - _SpecSmoothness,
                    _SpecThreshold + _SpecSmoothness,
                    specularValue);
                half3 specular = _SpecColor.rgb
                    * specularMask
                    * mainLight.color
                    * mainLight.shadowAttenuation
                    * _SpecStrength;

                half rimValue = pow(1.0h - saturate(dot(normalWS, viewDirWS)), _RimPower);
                half rimMask = smoothstep(
                    _RimThreshold - _RimSmoothness,
                    _RimThreshold + _RimSmoothness,
                    rimValue);
                half3 rim = _RimColor.rgb
                    * rimMask
                    * lerp(0.35h, 1.0h, mainVisibility)
                    * _RimStrength;

                half3 reflectionDir = reflect(-viewDirWS, normalWS);
            #if USE_CLUSTER_LIGHT_LOOP
                half3 environmentReflection = GlossyEnvironmentReflection(
                    reflectionDir,
                    input.positionWS,
                    saturate(_EnvironmentReflectionRoughness),
                    occlusion,
                    inputData.normalizedScreenSpaceUV);
            #else
                half3 environmentReflection = GlossyEnvironmentReflection(
                    reflectionDir,
                    input.positionWS,
                    saturate(_EnvironmentReflectionRoughness),
                    occlusion);
            #endif
                half environmentFresnel = pow(
                    1.0h - saturate(dot(normalWS, viewDirWS)),
                    _EnvironmentReflectionFresnelPower);
                half reflectionMask = lerp(0.25h, 1.0h, environmentFresnel);
                half3 reflection = environmentReflection
                    * reflectionMask
                    * _EnvironmentReflectionIntensity;

                half3 emission = SampleEmission(input.uv);
                half3 finalColor = indirectDiffuse
                    + directDiffuse
                    + additionalLights
                    + specular
                    + rim
                    + reflection
                    + emission;
                finalColor = max(finalColor, 0.0h);

                half fogFactor = saturate(input.fogFactor * _FogInfluence);
                finalColor = MixFog(finalColor, fogFactor);
                return half4(finalColor, 1.0h);
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
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma shader_feature_local_fragment _ALPHATEST_ON

            #include "EnvironmentCartoonShaderInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma shader_feature_local_fragment _ALPHATEST_ON

            #include "EnvironmentCartoonShaderInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma shader_feature_local_fragment _ALPHATEST_ON

            #include "EnvironmentCartoonShaderInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }

            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex UniversalVertexMeta
            #pragma fragment EnvironmentCartoonMetaFragment
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature EDITOR_VISUALIZATION

            #include "EnvironmentCartoonShaderInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UniversalMetaPass.hlsl"

            half4 EnvironmentCartoonMetaFragment(Varyings input) : SV_Target
            {
                half4 albedo = SampleAlbedo(input.uv, half4(1, 1, 1, 1));

            #if defined(_ALPHATEST_ON)
                clip(albedo.a - _Cutoff);
            #endif

                MetaInput metaInput = (MetaInput)0;
                metaInput.Albedo = albedo.rgb;
                metaInput.Emission = SampleEmission(input.uv);
                return UniversalFragmentMeta(input, metaInput);
            }
            ENDHLSL
        }
    }

    CustomEditor "EnvironmentCartoonShaderCustomGUI"
}
