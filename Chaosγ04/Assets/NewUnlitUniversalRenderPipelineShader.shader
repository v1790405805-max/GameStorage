Shader "Custom/DissolveParticle_URP_HLSL"
{
    Properties
    {
        // ================= 1. 基础设置 =================
        [Space(10)]
        [Header(1. Base Settings)]
        [MainTexture] _BaseMap ("【主贴图】 基础纹理", 2D) = "white"{}
        [MainColor] _BaseColor ("【主颜色】 基础颜色", Color) = (1, 1, 1, 1)
        
        // ================= 2. 颜色与透明度 =================
        [Space(10)]
        [Header(2. Color and Alpha)]
        [HDR] _SingleColor ("整体染色 (HDR发光)", Color) = (1, 1, 1, 1)
        _GlobalAlpha ("整体透明度", Range(0, 1)) = 1.0
        
        // ================= 3. 遮罩 =================
        [Space(10)]
        [Header(3. Mask)]
        _MaskTex ("遮罩贴图 (用R通道)", 2D) = "white"{}
        _MaskStrength ("遮罩强度", Range (0,1)) = 1.0

        // ================= 4. 高级溶解 =================
        [Space(10)]
        [Header(4. Advanced Dissolve)]
        [Toggle(_USE_DISSOLVE)] _UseDissolve ("★ 是否启用溶解？", Float) = 1
        [Toggle(_DISSOLVE_CUSTOM_DATA)] _DissolveCustomData ("使用粒子CustomData(X)控制进度", Float) = 1
        _DissolveProgress ("溶解进度 (拖动滑块测试)", Range(0, 1)) = 0.0
        
        _DissolveTex ("溶解噪波 / 黑白图", 2D) = "white"{}
        
        [Toggle(_DISSOLVE_HARD_EDGE)] _DissolveHardEdge ("开启溶解硬边模式", Float) = 0
        _DissolveSoftness ("溶解软边", Range(0, 1)) = 0.08
        _DissolveContrast ("溶解对比度", Range(0, 5)) = 1.4
        _DissolveEdgeWidth ("溶解边缘宽度", Range(0, 1)) = 0.08
        [HDR] _DissolveEdgeColor ("溶解边缘颜色 HDR", Color) = (1,1,1,1)
        _DissolveInnerStrength ("溶解内边缘强度", Range(0, 10)) = 2.2

        // ================= 5. 副贴图层 =================
        [Space(10)]
        [Header(5. Secondary Texture)]
        [Toggle(_USE_SECONDARY_TEX)] _UseSecondaryTex("★ 是否启用副贴图层？", Float) = 0
        _SecondaryTex ("副贴图 (黑白细节图)", 2D) = "white" {}
        [HDR] _SecondaryColor ("副贴图染色 (HDR)", Color) = (1, 1, 1, 1)
        _SecondaryStrength ("副贴图混合强度", Range(0, 5)) = 0.54

        // ================= 6. 扭曲 / 热浪效果 =================
        [Space(10)]
        [Header(6. Distortion)]
        [Toggle(_USE_DISTORTION)] _UseDistortion("★ 是否启用扭曲流动？", Float) = 0
        _DistortionTex ("扭曲采样图 (黑白图)", 2D) = "grey" {}
        _DistortionSpeedX ("X轴 流动速度", Float) = -0.45
        _DistortionSpeedY ("Y轴 流动速度", Float) = -1.07
        _DistortionFlowMulti ("流动速度 整体倍率", Float) = 1.26
        _DistortionStrength ("扭曲变形强度", Range(0, 1)) = 0.12
        [Space(5)]
        [Toggle(_DISTORT_MAIN)] _DistortMain ("扭曲是否影响主贴图？", Float) = 1
        [Toggle(_DISTORT_DISSOLVE)] _DistortDissolve ("扭曲是否影响溶解效果？", Float) = 1
    }
    
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }
        
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // 宏开关
            #pragma shader_feature_local _USE_SECONDARY_TEX
            #pragma shader_feature_local _USE_DISTORTION
            #pragma shader_feature_local _DISTORT_MAIN
            #pragma shader_feature_local _DISTORT_DISSOLVE
            
            // 溶解宏开关
            #pragma shader_feature_local _USE_DISSOLVE
            #pragma shader_feature_local _DISSOLVE_CUSTOM_DATA
            #pragma shader_feature_local _DISSOLVE_HARD_EDGE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float4 customData : TEXCOORD1; //x = 溶解进度 y/z = 贴图偏移
            };
            
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 vertexColor : COLOR;
                float dissolveAmount : TEXCOORD1;
                float2 mainTexOffset : TEXCOORD2;
            };
            
            TEXTURE2D(_BaseMap);        SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MaskTex);        SAMPLER(sampler_MaskTex);
            TEXTURE2D(_DissolveTex);    SAMPLER(sampler_DissolveTex);
            TEXTURE2D(_SecondaryTex);   SAMPLER(sampler_SecondaryTex);
            TEXTURE2D(_DistortionTex);  SAMPLER(sampler_DistortionTex);
            
            CBUFFER_START (UnityPerMaterial)
            half4 _BaseColor;
            float4 _BaseMap_ST;
            float4 _MaskTex_ST;
            half _MaskStrength;

            // 颜色属性
            half4 _SingleColor;
            half _GlobalAlpha;

            // 高级溶解属性
            float4 _DissolveTex_ST;
            half _DissolveProgress;
            half _DissolveSoftness;
            half _DissolveContrast;
            half _DissolveEdgeWidth;
            half4 _DissolveEdgeColor;
            half _DissolveInnerStrength;

            // 副贴图属性
            float4 _SecondaryTex_ST;
            half4 _SecondaryColor;
            half _SecondaryStrength;

            // 扭曲属性
            float4 _DistortionTex_ST;
            float _DistortionSpeedX;
            float _DistortionSpeedY;
            float _DistortionFlowMulti;
            half _DistortionStrength;
            CBUFFER_END
            
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv; 
                OUT.vertexColor = IN.color;
                OUT.dissolveAmount = IN.customData.x;
                OUT.mainTexOffset = float2(IN.customData.y, IN.customData.z);
                return OUT;
            }
            
            half4 frag(Varyings IN) : SV_Target
            {
                // ================= 1. 扭曲计算 =================
                float2 distOffset = float2(0.0, 0.0);
                #if defined(_USE_DISTORTION)
                    float2 distUV = TRANSFORM_TEX(IN.uv, _DistortionTex) + _Time.y * float2(_DistortionSpeedX, _DistortionSpeedY) * _DistortionFlowMulti;
                    half distRaw = SAMPLE_TEXTURE2D(_DistortionTex, sampler_DistortionTex, distUV).r;
                    distOffset = (distRaw - 0.5) * 2.0 * _DistortionStrength;
                #endif

                // ================= 2. 主贴图计算 =================
                float2 mainUV = TRANSFORM_TEX(IN.uv, _BaseMap) + IN.mainTexOffset;
                #if defined(_USE_DISTORTION) && defined(_DISTORT_MAIN)
                    mainUV += distOffset; 
                #endif
                
                half4 mainTexCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, mainUV) * _BaseColor * IN.vertexColor;

                // ================= 3. 副贴图层 =================
                #if defined(_USE_SECONDARY_TEX)
                    float2 secUV = TRANSFORM_TEX(IN.uv, _SecondaryTex);
                    #if defined(_USE_DISTORTION) && defined(_DISTORT_MAIN)
                        secUV += distOffset; 
                    #endif
                    half4 secTexCol = SAMPLE_TEXTURE2D(_SecondaryTex, sampler_SecondaryTex, secUV);
                    half3 secBlend = secTexCol.rgb * _SecondaryColor.rgb;
                    mainTexCol.rgb = lerp(mainTexCol.rgb, mainTexCol.rgb * secBlend, _SecondaryStrength);
                #endif

                // ================= 4. 整体颜色与透明度 =================
                mainTexCol.rgb *= _SingleColor.rgb;
                mainTexCol.a *= _GlobalAlpha;

                // ================= 5. 遮罩计算 =================
                float2 maskUV = TRANSFORM_TEX(IN.uv, _MaskTex);
                half maskRaw = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, maskUV).r;
                half maskValue = lerp(1.0, maskRaw, _MaskStrength);
                mainTexCol.a *= maskValue;

                // ================= 6. 高级溶解计算 =================
                #if defined(_USE_DISSOLVE)
                    float2 dissolveUV = TRANSFORM_TEX(IN.uv, _DissolveTex);
                    #if defined(_USE_DISTORTION) && defined(_DISTORT_DISSOLVE)
                        dissolveUV += distOffset; 
                    #endif
                    
                    // 采样溶解噪波
                    half rawNoise = SAMPLE_TEXTURE2D(_DissolveTex, sampler_DissolveTex, dissolveUV).r;
                    
                    // 调节噪波对比度
                    half noise = saturate((rawNoise - 0.5) * _DissolveContrast + 0.5);
                    
                    // 确定溶解进度 (从粒子CustomData读取，或者手动滑块)
                    half progress = _DissolveProgress;
                    #if defined(_DISSOLVE_CUSTOM_DATA)
                        progress = IN.dissolveAmount; 
                    #endif
                    
                    // 映射进度：防止进度为 0 或 1 时由于软边导致溶解不彻底
                    progress = lerp(-_DissolveEdgeWidth - _DissolveSoftness, 1.0 + _DissolveSoftness, progress);
                    
                    half alphaMask = 1.0;
                    half edgeMask = 0.0;

                    #if defined(_DISSOLVE_HARD_EDGE)
                        // 【硬边模式】使用 step 产生绝对锐利的边缘
                        alphaMask = step(progress, noise);
                        edgeMask = step(progress, noise) - step(progress + _DissolveEdgeWidth, noise);
                    #else
                        // 【软边模式】使用 smoothstep 产生柔和的过渡和发光带
                        // 1. 计算透明度遮罩
                        alphaMask = smoothstep(progress, progress + _DissolveSoftness + 0.0001, noise);
                        
                        // 2. 计算边缘发光带遮罩 (中间区域亮，两边暗)
                        half edgeStart = progress;
                        half edgeEnd = progress + _DissolveEdgeWidth;
                        edgeMask = smoothstep(edgeStart - _DissolveSoftness, edgeStart + 0.0001, noise) 
                                 - smoothstep(edgeEnd, edgeEnd + _DissolveSoftness + 0.0001, noise);
                    #endif
                    
                    // 叠加发光颜色 (边缘颜色 * 边缘遮罩 * 强度倍数)
                    half3 edgeColorFinal = _DissolveEdgeColor.rgb * edgeMask * _DissolveInnerStrength;
                    
                    // 将边缘发光加到主颜色上，并将溶解遮罩乘到透明度上
                    mainTexCol.rgb += edgeColorFinal;
                    mainTexCol.a *= alphaMask;
                #endif

                // 性能优化：剔除完全透明的像素
                if(mainTexCol.a <= 0.001)
                    discard;

                return mainTexCol;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}