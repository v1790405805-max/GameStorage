Shader "Hidden/PixelRender/Outline"
{
    Properties
    {
        
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
			"RenderType" = "Opaque"
			"PreviewType" = "Plane"
        }
        
        Pass
        {
            Cull Off
			ZWrite On
			ZTest Off
			
			HLSLINCLUDE
				#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			ENDHLSL

			HLSLPROGRAM
			#pragma target 2.5
			#pragma vertex vert
			#pragma fragment frag

			TEXTURE2D(_OutlineTex);
			SAMPLER(sampler_OutlineTex);
			float4 _OutlineTex_TexelSize;
			TEXTURE2D(_NormalTex);
			SAMPLER(sampler_NormalTex);
			float4 _NormalTex_TexelSize;

			struct Attributes
			{
				uint vertexID : SV_VertexID;
			};
			
			struct Varyings
			{
				float4 vertex : SV_POSITION;
                float2 uv[5] : TEXCOORD0;
			};

			Varyings vert(Attributes input)
			{
				Varyings o;
				
				float4 pos = GetFullScreenTriangleVertexPosition(input.vertexID);
				float2 uv = GetFullScreenTriangleTexCoord(input.vertexID);
				o.uv[0] = uv;
                o.uv[1] = uv + _OutlineTex_TexelSize.xy * half2(1,0);
                o.uv[2] = uv + _OutlineTex_TexelSize.xy * half2(-1,0);
                o.uv[3] = uv + _OutlineTex_TexelSize.xy * half2(0,1);
                o.uv[4] = uv + _OutlineTex_TexelSize.xy * half2(0,-1);
				o.vertex = pos;
				return o;
			}

			float4 frag(Varyings i) : SV_Target
			{
				//贴图采样
                float3 outlineTex0 = SAMPLE_TEXTURE2D(_OutlineTex, sampler_OutlineTex,i.uv[0]).rgb;
                float3 outlineTex1 = SAMPLE_TEXTURE2D(_OutlineTex, sampler_OutlineTex,i.uv[1]).rgb;
                float3 outlineTex2 = SAMPLE_TEXTURE2D(_OutlineTex, sampler_OutlineTex,i.uv[2]).rgb;
                float3 outlineTex3 = SAMPLE_TEXTURE2D(_OutlineTex, sampler_OutlineTex,i.uv[3]).rgb;
                float3 outlineTex4 = SAMPLE_TEXTURE2D(_OutlineTex, sampler_OutlineTex,i.uv[4]).rgb;

				float3 normal0 = normalize(SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex,i.uv[0]).rgb);
				float3 normal1 = normalize(SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex,i.uv[1]).rgb);
				float3 normal2 = normalize(SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex,i.uv[3]).rgb);

				float isObj1 = step(abs(outlineTex0.r - outlineTex1.r),0);
				float isObj2 = step(abs(outlineTex0.r - outlineTex2.r),0);
				float isObj3 = step(abs(outlineTex0.r - outlineTex3.r),0);
				float isObj4 = step(abs(outlineTex0.r - outlineTex4.r),0);
				
				//外描边
				float outline1 = 1 - step(outlineTex0.r,outlineTex1.r);
                float outline2 = 1 - step(outlineTex0.r,outlineTex2.r);
                float outline3 = 1 - step(outlineTex0.r,outlineTex3.r);
                float outline4 = 1 - step(outlineTex0.r,outlineTex4.r);
                float outline = min(1,outline1+outline2+outline3+outline4);

				//法线描边
				float edgeAgle = outlineTex0.g;
				float angle1 = acos(dot(normal0,normal1));
                float angle2 = acos(dot(normal0,normal2));
				float edge1 = step(edgeAgle,angle1) * isObj1;
				float edge2 = step(edgeAgle,angle2) * isObj3;
				float edge = min(1,edge1+edge2) - outline;

				//额外线
				float addline1 = (1 - step(outlineTex0.b - 0.001,outlineTex1.b)) * isObj1;
                float addline2 = (1 - step(outlineTex0.b - 0.001,outlineTex2.b)) * isObj2;
                float addline3 = (1 - step(outlineTex0.b - 0.001,outlineTex3.b)) * isObj3;
                float addline4 = (1 - step(outlineTex0.b - 0.001,outlineTex4.b)) * isObj4;
				float addline = min(1,addline1+addline2+addline3+addline4) - outline;
				return float4(outline,edge,addline,1);
			}
			
			ENDHLSL
        }
    }
}
