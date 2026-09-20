Shader "PixelRender/Pixel"
{
    Properties
    {
    	//纹理
        _MainTex ("Texture", 2D) = "white" {}
    	_Color("颜色",Color) = (1.0,1.0,1.0,1.0)
    	
    	//法线
    	[NoScaleOffset][Normal]_NormalTex ("_NormalTex", 2D) = "bump"{}
        _NormalScale("_NormalScale",Range(0,1)) = 1.0
    	
    	//自定义光照角度
    	_LightRow("LightRow",Range(0,360)) = 45.0
    	_LightYaw("LightYaw",Range(0,180)) = 45.0
    	
    	//投影
    	[MinMax]_LightAttenuation("投影衰减", Vector) = (0, 1, 0, 0)
    	[Gradient]_ShadowRampTex("阴影Ramp",2D) = "white"{}
    	_ShadingOffset("阴影RampOffset",Range(0,1)) = 0.0
    	_ShadowColor("投影颜色",Color) = (1.0,1.0,1.0,1.0)
    	
    	//高光
    	[HDR] _SpecularColor("高光颜色", Color) = (0.85023, 0.85034, 0.85045, 0.85056)
    	_SpecularSize("高光大小", Range(0.0, 1.0)) = 0.1
        _SpecularEdgeSmoothness("高光平滑", Range(0.0, 1.0)) = 0.0
    	
    	//边缘光
    	[HDR] _RimColor("边缘光颜色", Color) = (0.85023, 0.85034, 0.85045, 0.85056)
        [Gradient]_RimGradientTex("RimGradientTex",2D) = "white"{}
    	_RimSize("边缘光大小", Range(0, 1)) = 0.5
        _RimEdgeSmoothness("边缘光Smoothness", Range(0, 1)) = 0.5
    	
    	//描边
    	_OutlineColor("描边颜色",Color) = (0.5,0.5,0.5,0.5)
    	[Gradient]_OutlineGradientTex("表面漫反射梯度",2D) = "white"{}
    	
    	_EdgeColor("边缘颜色",Color) = (0.5,0.5,0.5,0)
    	_EdgeAngle("边缘角度",Range(1,180)) = 60.0
    	[Gradient]_EdgeGradientTex("边缘漫反射梯度",2D) = "white"{}
    	
    	_AddLineColor("额外线颜色",Color) = (0.5,0.5,0.5,0.5)
    	_AddLineEdge("额外线边缘",Range(0,1)) = 0.0
    	[Gradient]_AddLineGradientTex("边缘漫反射梯度",2D) = "white"{}
    	
    	[IntRange]_OutlineSort("描边排序",Range(1,16)) = 8.0
    	
    	//额外光描边增强
    	_AddLightLineInt("额外光描边强度",Range(0,1)) = 0.0
    	
    	_AlphaClip("Clip",Range(0,1)) = 0.0
    	_Alpha("Alpha",Range(0,1)) = 1.0
    	
    	//自发光
    	_EmissionMask("自发光遮罩",2D) = "white" {}
    	[HDR]_EmissionColor("自发光颜色",Color) = (0.5,0.5,0.5,0.5)
    	_EmissionOutlineInt("自发光描边增强",Range(0,1)) = 1.0
    	
    	[Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 2.0
    	[Toggle(_True)] _ZWrite("ZWrite", Float) = 1.0
	    [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 4.0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"

		#include "PixelFun.hlsl"
		
        //全局KeyCode
        //投影相关
		#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
		#pragma multi_compile _ _SHADOWS_SOFT //柔化阴影，得到软阴影
		#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
		#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
		
		//自定义灯光方向
		#pragma shader_feature _ _LIGHT_DIR_ON//自定义灯光方向
		#pragma shader_feature _ _LOCAL_LIGHT_ON//自定义灯光是否使用局部坐标

		//光照
		#pragma shader_feature _ _NORMAL_MAP_ON//使用法线贴图
		#pragma shader_feature _ _SHADOW_ATTENUATION_ON//开启阴影
		#pragma shader_feature _ _SPECULAR_ON//开启高光
		#pragma shader_feature _ _RIM_ON//开启边缘光

		//描边
		#pragma shader_feature _ _OUTLINE_ON//开启描边

		//透明
		#pragma shader_feature _ _ALPHA_CLIP_ON//开启AlphaClip
		#pragma shader_feature _ _DITHER_ON//开启透明抖动
		#pragma shader_feature _ _DITHER_OUTLINE_ON//开启透明抖动描边

		//自发光
		#pragma shader_feature _ _EMISSION_ON//开启自发光

		//贴花
		#pragma shader_feature _ _DECAL_ON//开启贴花
		
        CBUFFER_START(UnityPerMaterial)
			float4 _MainTex_ST;
		    float4 _Color;

			float _NormalScale;

			float _LightRow;
			float _LightYaw;

			float2 _LightAttenuation;
			float _ShadingOffset;
			float4 _ShadowColor;

			float4 _SpecularColor;
			float _SpecularSize;
		    float _SpecularEdgeSmoothness;

			float4 _RimColor;
		    float _RimSize;
		    float _RimEdgeSmoothness;

			float4 _OutlineColor;
			float4 _EdgeColor;
			float _EdgeAngle;
			float4 _AddLineColor;
			float _AddLineEdge;
			float _OutlineSort;

			float _AlphaClip;
		    float _Alpha;

			float _AddLightLineInt;

			float4 _EmissionColor;
			float _EmissionOutlineInt;
		
		CBUFFER_END
		
		TEXTURE2D(_MainTex);
		SAMPLER(sampler_MainTex);

		TEXTURE2D (_NormalTex);
		SAMPLER(sampler_NormalTex);
		
		TEXTURE2D(_ShadowRampTex);
		SAMPLER(sampler_ShadowRampTex);

		TEXTURE2D(_PixelOutlineTex);
		SAMPLER(sampler_PixelOutlineTex);

		TEXTURE2D(_RimGradientTex);
		SAMPLER(sampler_RimGradientTex);

		TEXTURE2D(_OutlineGradientTex);
		SAMPLER(sampler_OutlineGradientTex);
		TEXTURE2D(_EdgeGradientTex);
		SAMPLER(sampler_EdgeGradientTex);
		TEXTURE2D(_AddLineGradientTex);
		SAMPLER(sampler_AddLineGradientTex);

		TEXTURE2D(_SdfAddLightTex);
		SAMPLER(sampler_SdfAddLightTex);

		TEXTURE2D(_EmissionMask);
		SAMPLER(sampler_EmissionMask);

        struct Attributes
		{
			float3 positionOS : POSITION;
			float4 normal : NORMAL;
        	float4 tangent : TANGENT;
			float2 uv :TEXCOORD0;
        	float3 color : COLOR;
		};

		struct Varyings
		{
			float4 posCS : SV_POSITION;
			float3 posWS : TEXCOORD0;
            float2 uv :TEXCOORD1;
            float3 nDirWS : TEXCOORD2;
            float4 posNDS : TEXCOORD3;
			float3 tDirWS : TEXCOORD4;
            float3 bDIrWS : TEXCOORD5;
			float3 color : TEXCOORD6;
			float depth : TEXCOORD7;
		};

		Varyings Vert(Attributes v)
		{
			Varyings o = (Varyings)0;
				VertexPositionInputs vertexInput = GetVertexPositionInputs(v.positionOS.xyz);
        		o.depth = vertexInput.positionNDC.z / vertexInput.positionNDC.w;
				o.posCS = TransformObjectToHClip(v.positionOS);
        		o.posWS = TransformObjectToWorld(v.positionOS);
        		o.nDirWS = TransformObjectToWorldNormal(v.normal.xyz);
        		o.posNDS = vertexInput.positionNDC;
        		o.uv = TRANSFORM_TEX(v.uv,_MainTex);
        		#if _WORLD_UV_ON
				o.uv = TRANSFORM_TEX(o.posWS.xz,_MainTex);
        		#endif
        		o.tDirWS.xyz = TransformObjectToWorldDir(v.tangent.xyz);
                o.bDIrWS.xyz = cross(o.nDirWS.xyz,o.tDirWS.xyz) * v.tangent.w * unity_WorldTransformParams.w;    //计算出B  这里输入一个不同平台的法线向量。
        		o.color = v.color;
				return o;
		}
        ENDHLSL

        Pass
        {
        	Name "Pixel"
			Tags {  "Queue" = "Geometry+0" "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}
			ZWrite [_ZWrite]
			ZTest [_ZTest]
			Cull [_Cull]
			
			HLSLPROGRAM
			
			#pragma vertex vert
            #pragma fragment frag
			
			Varyings vert(Attributes v)
			{
				return Vert(v);
			}

			float4 frag(Varyings i) : SV_TARGET
			{
				float4 SHADOW_COORDS = TransformWorldToShadowCoord(i.posWS);
				Light mainLight = GetMainLight(SHADOW_COORDS);

				//视方向
				float3 vDirWS = UNITY_MATRIX_V[2].xyz;
				
				//主光源
				float3 lightColor = mainLight.color.rgb;

				//设置光源方向
				#if _LIGHT_DIR_ON
				float3 lightDir = GetDir(_LightYaw - 90,_LightRow);
					#if _LOCAL_LIGHT_ON
					lightDir = TransformObjectToWorldDir(lightDir);
					#endif
				mainLight.direction = lightDir;
				#endif

				//纹理
				float4 MainTex = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).rgba;
				float3 fanilColor = float3(1,1,1);

				//投影
				float shadowAttenuation = 1;
				//return float4(mainLight.shadowAttenuation,0,0,1);
				#if _SHADOW_ATTENUATION_ON
				shadowAttenuation = mainLight.shadowAttenuation;
			    shadowAttenuation = RangeRemap(_LightAttenuation.x, _LightAttenuation.y, shadowAttenuation);
				#endif

				//法线
				float3 nDirWS = i.nDirWS;
				#if _NORMAL_MAP_ON
				float3x3 TBN = {i.tDirWS.xyz,i.bDIrWS.xyz,i.nDirWS.xyz};          
				nDirWS = CalculateNormalMap(_NormalTex,sampler_NormalTex,i.uv,TBN,_NormalScale);
				#endif

				//描边
				float2 screenPos = i.posNDS.rg/i.posNDS.w;
				float3 outline = float3(0,0,0);
				#if _OUTLINE_ON
				outline = SAMPLE_TEXTURE2D(_PixelOutlineTex,sampler_PixelOutlineTex,screenPos).rgb;
				#endif

				//光照部分
				float diffuse = 1;
				fanilColor *= CalculateDiffuse(nDirWS,mainLight.direction,_ShadingOffset,_ShadowRampTex,sampler_ShadowRampTex, diffuse);
				fanilColor *= lerp(_ShadowColor.rgb,float3(1,1,1),shadowAttenuation);
				
				//高光
				float3 specularColor = float3(0,0,0);
				#if _SPECULAR_ON
				float specularTransition = CalculateSpecular(nDirWS,mainLight.direction,vDirWS,_SpecularSize,_SpecularEdgeSmoothness);
				specularColor = _SpecularColor.rgb * _SpecularColor.a * specularTransition;
				#endif
				
				fanilColor *= _Color.rgb * MainTex.rgb;
				fanilColor += specularColor;

				//边缘光
				#if _RIM_ON
				float rimTransition = CalculateRim(nDirWS,vDirWS,_RimSize,_RimEdgeSmoothness);
				float4 rimColor = _RimColor.rgba * SAMPLE_TEXTURE2D(_RimGradientTex,sampler_RimGradientTex,float2(diffuse,0.5)).rgba;
				fanilColor = lerp(fanilColor,rimColor,rimColor.a * rimTransition);//边缘光
				#endif

				//环境光
				float3 IndirColor = _GlossyEnvironmentColor * _Color.rgb * MainTex.rgb;
				fanilColor += IndirColor;

				//描边
				float4 outlineGradient = SAMPLE_TEXTURE2D(_OutlineGradientTex,sampler_OutlineGradientTex,float2(diffuse,0.5)).rgba;//描边梯度
				float4 edgeGradient = SAMPLE_TEXTURE2D(_EdgeGradientTex,sampler_EdgeGradientTex,float2(diffuse,0.5)).rgba;//边缘梯度
				float4 addLineGradient = SAMPLE_TEXTURE2D(_AddLineGradientTex,sampler_AddLineGradientTex,float2(diffuse,0.5)).rgba;//额外线梯度
				fanilColor = lerp(fanilColor,_EdgeColor.rgb * edgeGradient.rgb,_EdgeColor.a * edgeGradient.a * outline.g);//先计算边缘
				fanilColor = lerp(fanilColor,_AddLineColor.rgb * addLineGradient.rgb ,_AddLineColor.a * addLineGradient.a * outline.b);//额外线
				fanilColor = lerp(fanilColor,_OutlineColor.rgb * outlineGradient.rgb,_OutlineColor.a * outlineGradient.a * outline.r);//描边

				fanilColor *= lightColor;
				float alpha = MainTex.a;

				//自发光
				#if _EMISSION_ON
				float3 emission = SAMPLE_TEXTURE2D(_EmissionMask,sampler_EmissionMask,i.uv).rgb;
				emission *= _EmissionColor.rgb;
				emission += min(1,outline.r + outline.g + outline.b) * _EmissionOutlineInt * emission;
				fanilColor += emission;
				#endif

				//额外光描边增强
				float3 light = SAMPLE_TEXTURE2D(_SdfAddLightTex,sampler_SdfAddLightTex,screenPos).rgb;
				light *= min(1,outline.r + outline.g + outline.b);
				light *= _AddLightLineInt;
				fanilColor += light;

				#if _DECAL_ON
				ApplyDecalToBaseColor(i.posCS, fanilColor);
				#endif
				
				//抖动
				#if _DITHER_ON
				alpha *= _Alpha;
				#if _DITHER_OUTLINE_ON
				alpha += outline.r;
				#endif
				Unity_Dither_float4(screenPos,alpha);
				clip(alpha);
				#endif
				
				#if _ALPHA_CLIP_ON
				clip(alpha - _AlphaClip);
				#endif
				
				return float4(fanilColor,1);
			}
			
			ENDHLSL
        }
		
		pass
		{
			Name "Outlines"
			Tags
			{
				"RenderPipeline" = "UniversalRenderPipeline"
				"LightMode" = "Outlines"
				//"DisableBatching" = "True"
			}
			
			ZWrite [_ZWrite]
			ZTest [_ZTest]
			Cull [_Cull]
			Blend Off
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			Varyings vert(Attributes v)
			{
				VertexPositionInputs vertexInput = GetVertexPositionInputs(float3(0,0,0));
				Varyings o = Vert(v);
				o.depth = vertexInput.positionNDC.z / vertexInput.positionNDC.w;
				return o;
			}
			void frag(Varyings i, out float4 color : COLOR)
			{
        	//纹理采样
				float4 MainTex = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).rgba;
				#if _ALPHA_CLIP_ON
				float alpha = MainTex.a;
				clip(alpha - _AlphaClip);
				#endif
        		float Depth = i.posNDS.z / i.posNDS.w;
        		#if _DEPTH_TEST_ON
        		float2 screenPos = i.posNDS.rg/i.posNDS.w;
				float myDepth = SAMPLE_TEXTURE2D(_DepthTexture,sampler_DepthTexture,screenPos).r;
				clip(step(myDepth,Depth)-0.1);
				#endif
        		#if !_OUTLINE_ON
				clip(-1);
        		#endif
        		
				color = float4(i.depth + _OutlineSort,radians(_EdgeAngle),i.color.r * _AddLineEdge,1);
			}
			ENDHLSL
		}

		pass
		{
			Name "DepthOnly"
			Tags {
				"LightMode" = "DepthOnly"
			}
			
			ZWrite [_ZWrite]
			ZTest [_ZTest]
			Cull [_Cull]
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			Varyings vert(Attributes v)
			{
				return Vert(v);
			}

			void frag(Varyings i, out float4 color : COLOR)
			{
        		float4 MainTex = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).rgba;
        		float alpha = MainTex.a;
				//抖动
				#if _DITHER_ON
        		float2 screenPos = i.posNDS.rg/i.posNDS.w;
				alpha *= _Alpha;
        		#if _DITHER_OUTLINE_ON
        		float2 outline = SAMPLE_TEXTURE2D(_PixelOutlineTex,sampler_PixelOutlineTex,screenPos).rg;
				alpha += outline.r;
				#endif
				Unity_Dither_float4(screenPos,alpha);
				clip(alpha);
				#endif
        	
				#if _ALPHA_CLIP_ON
				alpha = MainTex.a;
				clip(alpha - _AlphaClip);
				#endif

        		float Depth = i.posNDS.z / i.posNDS.w;
				color = float4(Depth,Depth,Depth,1);
			}
			ENDHLSL
		}
		
		pass
		{
			Name "DepthNormals"
			Tags {
				"LightMode" = "DepthNormals"
			}
			
			ZWrite [_ZWrite]
			ZTest [_ZTest]
			Cull [_Cull]
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			Varyings vert(Attributes v)
			{
				return Vert(v);
			}

			void frag(Varyings i, out float4 color : COLOR)
			{
        		float4 MainTex = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).rgba;
        		float alpha = MainTex.a;
				//抖动
				#if _DITHER_ON
        		float2 screenPos = i.posNDS.rg/i.posNDS.w;
				alpha *= _Alpha;
        		#if _DITHER_OUTLINE_ON
        		float2 outline = SAMPLE_TEXTURE2D(_PixelOutlineTex,sampler_PixelOutlineTex,screenPos).rg;
				alpha += outline.r;
				#endif
				Unity_Dither_float4(screenPos,alpha);
				clip(alpha);
				#endif
        	
				#if _ALPHA_CLIP_ON
				alpha = MainTex.a;
				clip(alpha - _AlphaClip);
				#endif

        		float3 normal = i.nDirWS;
				color = float4(normal,1);
			}
			ENDHLSL
		}

//投影
		pass
		{
			Name "ShadowCaster"
			Tags
			{
				"LightMode" = "ShadowCaster"
			}
			
			ZWrite [_ZWrite]
			ZTest [_ZTest]
			Cull [_Cull]
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 2.5
			float3 _LightDirection;

			// 获取裁剪空间下的阴影坐标
            float4 GetShadowPositionHClips(Attributes input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normal.xyz);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));                
                return positionCS;
            }

			// 顶点着色器
			Varyings vert(Attributes v)
			{
				Varyings o = (Varyings)0;
				o.posCS = GetShadowPositionHClips(v);
				o.uv = TRANSFORM_TEX(v.uv,_MainTex);
            	return o;
			}

			float4 frag(Varyings i) : SV_TARGET
			{
				float4 MainTex = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).rgba;
				#if _ALPHA_CLIP_ON
				float alpha = MainTex.a;
				clip(alpha - _AlphaClip);
				#endif
				return 0;
			}
			ENDHLSL
		}
		
    }
	CustomEditor "PixelRender.PixelShaderGUI"
}
