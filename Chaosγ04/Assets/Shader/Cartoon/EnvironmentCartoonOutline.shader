Shader "Hidden/EnvironmentCartoon/Outline"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        Pass
        {
            Cull Off
            ZWrite On
            ZTest Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #define ENVIRONMENT_OUTLINE_MAX_WIDTH 10

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_EnvironmentCartoonOutlineMetadataTex);
            SAMPLER(sampler_EnvironmentCartoonOutlineMetadataTex);
            float4 _EnvironmentCartoonOutlineMetadataTex_TexelSize;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            float3 GetOutlineCameraSignature()
            {
                uint hash = 2166136261u;
                hash = (hash ^ asuint(unity_MatrixVP._m00)) * 16777619u;
                hash = (hash ^ asuint(unity_MatrixVP._m03)) * 16777619u;
                hash = (hash ^ asuint(unity_MatrixVP._m11)) * 16777619u;
                hash = (hash ^ asuint(unity_MatrixVP._m13)) * 16777619u;
                hash = (hash ^ asuint(unity_MatrixVP._m22)) * 16777619u;
                hash = (hash ^ asuint(unity_MatrixVP._m23)) * 16777619u;
                hash = (hash ^ asuint(_WorldSpaceCameraPos.x)) * 16777619u;
                hash = (hash ^ asuint(_WorldSpaceCameraPos.y)) * 16777619u;
                hash = (hash ^ asuint(_WorldSpaceCameraPos.z)) * 16777619u;
                return float3(
                    (hash & 255u) / 255.0,
                    ((hash >> 8u) & 255u) / 255.0,
                    ((hash >> 16u) & 255u) / 255.0);
            }

            float EvaluateNeighbor(
                float2 uv,
                float centerDepth,
                uint centerObjectID)
            {
                float4 metadata = SAMPLE_TEXTURE2D_LOD(
                    _EnvironmentCartoonOutlineMetadataTex,
                    sampler_EnvironmentCartoonOutlineMetadataTex,
                    uv,
                    0);
                float neighborDepth = metadata.r - floor(metadata.r);
                uint neighborObjectID = asuint(metadata.a) & 0x007FFFFFu;

                float differentObject = centerObjectID == neighborObjectID ? 0.0 : 1.0;
                return differentObject * (1.0 - step(centerDepth, neighborDepth));
            }

            float4 frag(Varyings input) : SV_Target
            {
                float4 center = SAMPLE_TEXTURE2D(
                    _EnvironmentCartoonOutlineMetadataTex,
                    sampler_EnvironmentCartoonOutlineMetadataTex,
                    input.uv);

                float centerWidth = floor(center.r);
                float3 cameraSignature = GetOutlineCameraSignature();
                if (centerWidth < 0.5)
                {
                    return float4(0, cameraSignature);
                }

                float centerDepth = center.r - centerWidth;
                uint centerObjectID = asuint(center.a) & 0x007FFFFFu;
                float outline = 0;
                float maxWidth = min(centerWidth, ENVIRONMENT_OUTLINE_MAX_WIDTH);

                [loop]
                for (int stepIndex = 1; stepIndex <= ENVIRONMENT_OUTLINE_MAX_WIDTH; stepIndex++)
                {
                    if (stepIndex > maxWidth)
                    {
                        break;
                    }

                    float2 offset = _EnvironmentCartoonOutlineMetadataTex_TexelSize.xy * stepIndex;
                    float2 horizontal = float2(offset.x, 0);
                    float2 vertical = float2(0, offset.y);
                    float2 diagonal = offset * 0.70710678;

                    outline = max(outline, EvaluateNeighbor(input.uv + horizontal, centerDepth, centerObjectID));
                    outline = max(outline, EvaluateNeighbor(input.uv - horizontal, centerDepth, centerObjectID));
                    outline = max(outline, EvaluateNeighbor(input.uv + vertical, centerDepth, centerObjectID));
                    outline = max(outline, EvaluateNeighbor(input.uv - vertical, centerDepth, centerObjectID));
                    outline = max(outline, EvaluateNeighbor(input.uv + diagonal, centerDepth, centerObjectID));
                    outline = max(outline, EvaluateNeighbor(input.uv + float2(offset.x, -offset.y) * 0.70710678, centerDepth, centerObjectID));
                    outline = max(outline, EvaluateNeighbor(input.uv + float2(-offset.x, offset.y) * 0.70710678, centerDepth, centerObjectID));
                    outline = max(outline, EvaluateNeighbor(input.uv - diagonal, centerDepth, centerObjectID));
                }

                return float4(outline, cameraSignature);
            }
            ENDHLSL
        }
    }
}
