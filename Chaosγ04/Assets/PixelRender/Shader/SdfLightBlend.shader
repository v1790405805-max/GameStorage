Shader "Hidden/PixelRender/SdfLightBlend"
{
    Properties
    {
    }
    SubShader
    {
        Tags {"RenderType"="Transparent"
            "RenderPipeline" = "UniversalPipeline" 
            "IgnoreProjector"="True"
            "Queue"="Transparent"}
        HLSLINCLUDE
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
		
        struct Attributes
		{
			uint vertexID : SV_VertexID;
		};

		struct Varyings
		{
			float4 posCS : SV_POSITION;
			float2 uv : TEXCOORD0;
		};

		TEXTURE2D(_AddLightTex);
		SAMPLER(sampler_AddLightTex);
		TEXTURE2D(_CameraTex);
		SAMPLER(sampler_CameraTex);
		
        ENDHLSL
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            

            // 顶点着色器
			Varyings vert(Attributes v)
			{
				Varyings o;
				
				float4 pos = GetFullScreenTriangleVertexPosition(v.vertexID);
				float2 uv = GetFullScreenTriangleTexCoord(v.vertexID);

				o.posCS = pos;
        		o.uv = uv;
				return o;
			}

            float4 frag(Varyings i) : SV_TARGET
			{
				float4 addLightColor = SAMPLE_TEXTURE2D(_AddLightTex,sampler_AddLightTex,i.uv).rgba;
				float4 cameraColor = SAMPLE_TEXTURE2D(_CameraTex,sampler_CameraTex,i.uv).rgba;
				float4 finalColor = cameraColor + addLightColor;
				return float4(finalColor.rgb,1);
			}
            ENDHLSL
        }
    }
}
