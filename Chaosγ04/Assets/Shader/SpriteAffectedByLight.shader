// Sprite 2D 光照 Shader：Sprite 贴图受 URP 光照（Lit/PBR）与场景雾效（Fog）影响。
Shader "Custom/SpriteAffectedByLight"
{
    Properties
    {
        _MainTex ("MainTex", 2D) = "white" {}

        [Header(Lighting)]
        _LightIntensity ("Light Intensity", Range(0.0, 1.0)) = 1.0
        _ShadowStrength ("Shadow Strength", Range(0.0, 1.0)) = 1.0

        [Header(Normal Offset for 2D Sprite)]
        _SpriteNormalOffset ("Normal Offset (X, Y)", Vector) = (0, 1, 0, 0)

        // 使用 [HideInInspector] 在材质面板中隐藏 Alpha Cutoff 的控制
        [HideInInspector] _Cutoff ("Alpha Cutoff", Float) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
            "UniversalMaterialType" = "Lit"
        }
        LOD 100

        // ------------------------------------------------------------------
        // Forward Lit Pass
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            Blend One Zero
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // URP 光照关键字
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            // 雾关键字
            #pragma multi_compile_fog
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fragment _ LIGHTMAP_BICUBIC_SAMPLING
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile_fragment _ DEBUG_DISPLAY

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 uv           : TEXCOORD0;
                float2 staticLightmapUV   : TEXCOORD1;
                float2 dynamicLightmapUV  : TEXCOORD2;
                float4 color        : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
                float3 positionWS   : TEXCOORD1;
                float3 normalWS     : TEXCOORD2;
                half4 fogFactorAndVertexLight : TEXCOORD3;
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 4);
                #ifdef DYNAMICLIGHTMAP_ON
                float2 dynamicLightmapUV : TEXCOORD5;
                #endif
                #ifdef USE_APV_PROBE_OCCLUSION
                float4 probeOcclusion : TEXCOORD6;
                #endif
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Cutoff;
                float _LightIntensity;
                float _ShadowStrength;

                // Normal Offset CBUFFER 声明
                float4 _SpriteNormalOffset;
            CBUFFER_END

            half ApplyShadowStrength(half shadowAttenuation)
            {
                return lerp(1.0h, shadowAttenuation, saturate(_ShadowStrength));
            }

            // 与 UniversalFragmentPBR 保持一致，仅在直接光照前缩放阴影衰减。
            half4 UniversalFragmentPBRWithShadowStrength(InputData inputData, SurfaceData surfaceData)
            {
                #if defined(_SPECULARHIGHLIGHTS_OFF)
                bool specularHighlightsOff = true;
                #else
                bool specularHighlightsOff = false;
                #endif

                BRDFData brdfData;
                InitializeBRDFData(surfaceData, brdfData);

                #if defined(DEBUG_DISPLAY)
                half4 debugColor;

                if (CanDebugOverrideOutputColor(inputData, surfaceData, brdfData, debugColor))
                {
                    return debugColor;
                }
                #endif

                BRDFData brdfDataClearCoat = CreateClearCoatBRDFData(surfaceData, brdfData);
                half4 shadowMask = CalculateShadowMask(inputData);
                AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(inputData, surfaceData);
                uint meshRenderingLayers = GetMeshRenderingLayer();
                Light mainLight = GetMainLight(inputData, shadowMask, aoFactor);

                MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI);
                mainLight.shadowAttenuation = ApplyShadowStrength(mainLight.shadowAttenuation);

                LightingData lightingData = CreateLightingData(inputData, surfaceData);

                lightingData.giColor = GlobalIllumination(
                    brdfData,
                    brdfDataClearCoat,
                    surfaceData.clearCoatMask,
                    inputData.bakedGI,
                    aoFactor.indirectAmbientOcclusion,
                    inputData.positionWS,
                    inputData.normalWS,
                    inputData.viewDirectionWS,
                    inputData.normalizedScreenSpaceUV);

                #ifdef _LIGHT_LAYERS
                if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
                #endif
                {
                    lightingData.mainLightColor = LightingPhysicallyBased(
                        brdfData,
                        brdfDataClearCoat,
                        mainLight,
                        inputData.normalWS,
                        inputData.viewDirectionWS,
                        surfaceData.clearCoatMask,
                        specularHighlightsOff);
                }

                #if defined(_ADDITIONAL_LIGHTS)
                uint pixelLightCount = GetAdditionalLightsCount();

                #if USE_CLUSTER_LIGHT_LOOP
                [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                {
                    CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK

                    Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
                    light.shadowAttenuation = ApplyShadowStrength(light.shadowAttenuation);

                    #ifdef _LIGHT_LAYERS
                    if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
                    #endif
                    {
                        lightingData.additionalLightsColor += LightingPhysicallyBased(
                            brdfData,
                            brdfDataClearCoat,
                            light,
                            inputData.normalWS,
                            inputData.viewDirectionWS,
                            surfaceData.clearCoatMask,
                            specularHighlightsOff);
                    }
                }
                #endif

                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
                    light.shadowAttenuation = ApplyShadowStrength(light.shadowAttenuation);

                    #ifdef _LIGHT_LAYERS
                    if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
                    #endif
                    {
                        lightingData.additionalLightsColor += LightingPhysicallyBased(
                            brdfData,
                            brdfDataClearCoat,
                            light,
                            inputData.normalWS,
                            inputData.viewDirectionWS,
                            surfaceData.clearCoatMask,
                            specularHighlightsOff);
                    }
                LIGHT_LOOP_END
                #endif

                #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                lightingData.vertexLightingColor += inputData.vertexLighting * brdfData.diffuse;
                #endif

                #if REAL_IS_HALF
                return min(CalculateFinalColor(lightingData, surfaceData.alpha), HALF_MAX);
                #else
                return CalculateFinalColor(lightingData, surfaceData.alpha);
                #endif
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;

                half3 vertexLight = VertexLighting(vertexInput.positionWS, normalInput.normalWS);
                half fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);

                OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
                #ifdef DYNAMICLIGHTMAP_ON
                output.dynamicLightmapUV = input.dynamicLightmapUV.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
                #endif
                OUTPUT_SH4(vertexInput.positionWS, output.normalWS.xyz, GetWorldSpaceNormalizeViewDir(vertexInput.positionWS), output.vertexSH, output.probeOcclusion);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // 1. 采样纹理 & 顶点色
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 texAndVertex = input.color * texColor;

                // 2. 构建与修正 2D Sprite 法线
                half3 baseNormalWS = NormalizeNormalPerPixel(input.normalWS);
                // 将法线进行适度偏置，使其在 2D 平面上具有“上下/左右”弧度倾向，产生平滑光照过渡
                half3 adjustedNormalWS = normalize(baseNormalWS + TransformObjectToWorldDir(float3(_SpriteNormalOffset.xy, 0.0)));

                // 获取阴影采样坐标（光照本身由 URP 的 UniversalFragmentPBR 处理）
                #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif

                // 3. 固有色：贴图 * 顶点色，光照与雾效全部交给 URP 标准流程
                half3 albedo = texAndVertex.rgb;

                // 4. Alpha Clip
                clip(texColor.a - _Cutoff);
                half alpha = texColor.a;

                // 5. 构建 URP Surface & Input Data
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                // 使用偏置后的法线参与光照，让平的 Sprite 也能产生明暗过渡
                inputData.normalWS = adjustedNormalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    inputData.shadowCoord = shadowCoord;
                #else
                    inputData.shadowCoord = float4(0,0,0,0);
                #endif
                inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactorAndVertexLight.x);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.metallic = 0.0;
                surfaceData.specular = 0.0;
                surfaceData.smoothness = 0.5;
                surfaceData.occlusion = 1.0;
                surfaceData.emission = float3(0,0,0);
                surfaceData.alpha = alpha;
                surfaceData.normalTS = float3(0,0,1);
                surfaceData.clearCoatMask = 0;
                surfaceData.clearCoatSmoothness = 1;

                #if defined(_DBUFFER)
                    ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
                #endif

                #if defined(_SCREEN_SPACE_IRRADIANCE)
                    inputData.bakedGI = SAMPLE_GI(_ScreenSpaceIrradiance, input.positionCS.xy);
                #elif defined(DYNAMICLIGHTMAP_ON)
                    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.dynamicLightmapUV, input.vertexSH, inputData.normalWS);
                    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
                #elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
                    inputData.bakedGI = SAMPLE_GI(input.vertexSH,
                        GetAbsolutePositionWS(inputData.positionWS),
                        inputData.normalWS,
                        inputData.viewDirectionWS,
                        input.positionCS.xy,
                        input.probeOcclusion,
                        inputData.shadowMask);
                #else
                    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
                    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
                #endif

                // 6. PBR 光照与雾混合
                half4 color = UniversalFragmentPBRWithShadowStrength(inputData, surfaceData);
                // 光照强度：0 = 完全不受光照影响（原图直出），1 = 完整光照
                color.rgb = lerp(albedo, color.rgb, saturate(_LightIntensity));
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = 1.0;

                return color;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        // ShadowCaster Pass（向 3D 场景投射阴影）
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            Cull Off
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            // 平行光与点光/聚光灯的法线偏移算法不同，靠这个关键字区分
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            // URP 每帧设置：平行光用 _LightDirection，点光/聚光灯用 _LightPosition
            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv           : TEXCOORD0;
                float4 positionCS   : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // 必须与 ForwardLit 的布局完全一致，SRP Batcher 要求各 Pass 的 UnityPerMaterial 相同
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Cutoff;
                float _LightIntensity;
                float _ShadowStrength;
                float4 _SpriteNormalOffset;
            CBUFFER_END

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                // 与 URP 标准 ShadowCaster 一致：施加深度/法线偏移，再钳制到近裁剪面
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                output.positionCS = ApplyShadowClamping(positionCS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                // 按贴图 alpha 抠出 Sprite 轮廓，透明区域不投影
                half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                clip(alpha - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
