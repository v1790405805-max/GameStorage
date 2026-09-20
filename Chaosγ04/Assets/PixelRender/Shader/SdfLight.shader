Shader "Hidden/PixelRender/SdfLight"
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
		#include "SDF.hlsl"
		
        struct Attributes
		{
			uint vertexID : SV_VertexID;
		};

		struct Varyings
		{
			float4 posCS : SV_POSITION;
			float2 uv : TEXCOORD0;
		};
		
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
				float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,sampler_CameraDepthTexture,i.uv);
				float3 worldNormal = SAMPLE_TEXTURE2D(_CameraNormalsTexture,sampler_CameraNormalsTexture,i.uv).rgb;
				float4 worldPos = GetWorldSpacePosition(depth, i.uv);
				float3 sdf = _SDFColor(worldPos.xyz,worldNormal,i.uv);
				return half4(sdf,1);
			}
            ENDHLSL
        }
    }
}
