Shader "PixelRender/Leave"
{
    Properties
    {
        [NoScaleOffset]_MainTex ("Texture", 2D) = "white" {}
		_MainColor("MainColor",Color) = (1,1,1,1)
    	[IntRange] _SheetX("尺寸X",Range(1,8)) = 4
        [IntRange] _SheetY("尺寸Y",Range(1,8)) = 4
		[Gradient]_ShadowRampTex("阴影Ramp",2D) = "white"{}
    	_Scale("大小",Range(0.01,5)) = 1
    	_Offset("位置偏移",Range(-2,3)) = 0
    	_AngleInt("角度强度",Range(0,3)) = 1
    	_AngleOffset("角度偏移",Range(-180,180)) = 0
    	_CenterScale("中心边缘缩放",Range(1,3)) = 1
    	_NoiseTex("NoiseTex",3D) = "black"{}
    	_NoiseSize("漫反射噪声尺寸",Range(0.1,20)) = 1
    	_NoiseInt("漫反射噪声强度",Range(0,1)) = 1
    	_RotateNoiseSize("摆动噪声尺寸",Range(0.1,20)) = 1
    	_RotateNoiseInt("摆动噪声强度",Range(0,50)) = 1
    	_RotateSpeed("摆动速度",Range(0,20)) = 1
    	_SheetSpeed("序列帧速度",Range(0,30)) = 5
    	[HDR] _RimColor("边缘光颜色", Color) = (0.85023, 0.85034, 0.85045, 0.85056)
        [Gradient]_RimGradientTex("RimGradientTex",2D) = "white"{}
    	_RimSize("边缘光大小", Range(0, 1)) = 0.5
        _RimEdgeSmoothness("边缘光Smoothness", Range(0, 1)) = 0.5
    	
    	_AddLightLineInt("额外光描边强度",Range(0,1)) = 0.5
    	_OutlineColor("描边颜色",Color) = (0.5,0.5,0.5,0.5)
    	[Gradient]_OutlineGradientTex("EdgeGradientTex",2D) = "white"{}
    	[IntRange]_OutlineSort("描边排序",Range(1,16)) = 8
    	_OutlineDepth("自定义深度",Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
		#include "PixelFun.hlsl"
			
		//投影相关
		#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
		#pragma multi_compile _ _SHADOWS_SOFT //柔化阴影，得到软阴影
		#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
        #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS

        #pragma shader_feature _ _CIRCLE_ANGLE_ON//是否环形角度
        #pragma shader_feature _ _RIM_ON//是否开启边缘光
        #pragma shader_feature _ _OUTLINE_ON//是否开启描边
        #pragma shader_feature _ _LEAVE_OUTLINE_ON//每篇树叶单独描边
        #pragma shader_feature _ _OUTLINE_DEPTH_ON//是否开启自定义深度

        float4 _MapTex_ST;
		float4 _MainColor;
        float _SheetX;
        float _SheetY;
        float _Offset;
        float _Scale;
        float _AngleInt;
        float _AngleOffset;
        float _CenterScale;
        float _NoiseSize;
        float _NoiseInt;
        float _RotateNoiseSize;
        float _RotateNoiseInt;
        float _RotateSpeed;
        float _SheetSpeed;
        float4 _RimColor;
		float _RimSize;
		float _RimEdgeSmoothness;
        float _AddLightLineInt;
        float4 _OutlineColor;
        float _OutlineSort;
        float _OutlineDepth;
        
        TEXTURE2D(_MainTex);
		TEXTURE2D(_ShadowRampTex);
        TEXTURE3D(_NoiseTex);
        TEXTURE2D(_RimGradientTex);
        TEXTURE2D(_OutlineTexture);
        TEXTURE2D(_OutlineGradientTex);
        TEXTURE2D(_AddLightTex);

        SAMPLER(sampler_MainTex);
		SAMPLER(sampler_ShadowRampTex);
        SAMPLER(sampler_NoiseTex);
        SAMPLER(sampler_RimGradientTex);
        SAMPLER(sampler_OutlineTexture);
        SAMPLER(sampler_OutlineGradientTex);
        SAMPLER(sampler_AddLightTex);

        float3 _position;

         struct LeaveData
        {
	        float3 position;
        	float scale;
        	float3 normal;
        	float AngleOffset;
         	float SheetOffset;
        };
        
        struct Attributes
		{
			float3 positionOS : POSITION;
			float2 uv :TEXCOORD0;

			UNITY_VERTEX_INPUT_INSTANCE_ID
		};

		struct Varyings
		{
			float4 posCS : SV_POSITION;
            float2 uv :TEXCOORD0;
			float2 posUV : TEXCOORD1;
			float3 posWS : TEXCOORD2;
			float4 scrPos : TEXCOORD3;
			float4 posNDS : TEXCOORD4;
			float depth : TEXCOORD5;
			LeaveData GrassData : TEXCOORD6;
			UNITY_VERTEX_INPUT_INSTANCE_ID
		};

        UNITY_INSTANCING_BUFFER_START(Prop)
			StructuredBuffer<LeaveData> _LeafBuffer;
		UNITY_INSTANCING_BUFFER_END(Prop)
        

        Varyings Vert(Attributes v,uint id)
		{
			Varyings o = (Varyings)0;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v,o);
				o.GrassData = _LeafBuffer[id];
				float3 cameraDir = normalize(_WorldSpaceCameraPos - o.posWS);
				float nDotv =clamp(dot(cameraDir,o.GrassData.normal),0,1);

				float3 normal = o.GrassData.normal;
				float3 ndirVS = TransformWorldToViewDir(normal);
				float3 vDirY = normalize(ndirVS.xyz);
				float angle = 0;
				#if _CIRCLE_ANGLE_ON
				angle = acos(dot(normalize(vDirY.xy),float2(0,1)));
				angle *= _AngleInt;
				angle = min(PI,angle);
				float mark = cross(vDirY,float3(0,1,0)).z;
				if(mark>0)
				{
					angle = -angle;
				}
				#endif
				angle += _AngleOffset /180 * PI * o.GrassData.AngleOffset;
				float noise = SAMPLE_TEXTURE3D_LOD(_NoiseTex,sampler_NoiseTex,o.GrassData.position * _RotateNoiseSize,0).r;
				noise = (noise - 0.5) * _RotateNoiseInt;
				angle += sin(_Time.z * _RotateSpeed + o.GrassData.AngleOffset * 4) * noise / 180 * PI;

				v.positionOS = RotateZ(v.positionOS,-angle);
				v.positionOS *= _LeafBuffer[id].scale * _Scale * lerp(_CenterScale,1,nDotv);
				v.positionOS = Billboard(v.positionOS);
				v.positionOS += _LeafBuffer[id].position;
				v.positionOS += _LeafBuffer[id].normal * _Offset;
				v.positionOS = TransformWorldToObject(v.positionOS);
			
				VertexPositionInputs vertexInput = GetVertexPositionInputs(v.positionOS.xyz);
				o.posCS = TransformObjectToHClip(v.positionOS);
				o.posWS = TransformObjectToWorld(v.positionOS);
				o.posNDS = vertexInput.positionNDC;
				o.scrPos = ComputeScreenPos(TransformObjectToHClip(half3(0,0,0)));
			
				float2 newUV = v.uv;
			
                newUV.x = newUV.x/_SheetX + frac(floor((_Time.y + o.GrassData.AngleOffset) * _SheetSpeed)/_SheetX);
                //newUV.y = newUV.y/_SheetY + 1 - frac(floor(((_Time.y + o.GrassData.AngleOffset) * _SheetSpeed)/_SheetX)/_SheetY);
				newUV.y = newUV.y/_SheetY + 1 / _SheetY *  floor(o.GrassData.SheetOffset * (_SheetY - 1));

				#if _OUTLINE_DEPTH_ON
				o.depth = _OutlineDepth;
        		#else
        		o.depth = vertexInput.positionNDC.z / vertexInput.positionNDC.w;
				#endif
			
				o.uv = newUV;
			return  o;
		}

        ENDHLSL

        Pass
        {
            Name "Pixel"
			Tags {  "Queue" = "Geometry+0" "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
            Cull Off
            
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.5

			#pragma multi_compile_instancing
            
            // 顶点着色器
			Varyings vert(Attributes v,uint id : SV_InstanceID)
			{
				return Vert(v,id);
			}

            float4 frag(Varyings i) : SV_TARGET
			{  	
				UNITY_SETUP_INSTANCE_ID(i);
				
				float4 SHADOW_COORDS = TransformWorldToShadowCoord(i.GrassData.position + float3(0,0.01,0));
				Light mainLight = GetMainLight(SHADOW_COORDS);
				
				float3 nDirWS = normalize(i.GrassData.normal) ;

				float noise = SAMPLE_TEXTURE3D(_NoiseTex,sampler_NoiseTex,i.GrassData.position * _NoiseSize).r;
				noise = (noise - 0.5) * _NoiseInt;

				float diffuse = 1;
				float3 diffuseColor = CalculateDiffuse(nDirWS,mainLight.direction,noise,_ShadowRampTex,sampler_ShadowRampTex, diffuse);

				
				
				float4 Var_MainTex = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).rgba;
				float alpha = Var_MainTex.a;
				clip(alpha - 0.01);

				//描边
				float2 screenPos = i.posNDS.rg/i.posNDS.w;
				float3 outline = float3(0,0,0);
				#if _OUTLINE_ON
				outline = SAMPLE_TEXTURE2D(_OutlineTexture,sampler_OutlineTexture,screenPos).rgb;
				#endif

				//额外光
				float3 light = SAMPLE_TEXTURE2D(_AddLightTex,sampler_AddLightTex,screenPos).rgb;
				light *= min(1,outline.r + outline.g + outline.b) * _AddLightLineInt * Var_MainTex.g;
				
				float3 fanilColor =  _MainColor.rgb * diffuseColor;

				//边缘光
				#if _RIM_ON
				float3 vDirWS = normalize(_WorldSpaceCameraPos - i.posWS);
				float rimTransition = CalculateRim(nDirWS,vDirWS,_RimSize,_RimEdgeSmoothness);
				float4 rimColor = _RimColor.rgba * SAMPLE_TEXTURE2D(_RimGradientTex,sampler_RimGradientTex,float2(diffuse,0.5)).rgba;
				fanilColor = lerp(fanilColor,rimColor.rgb,rimColor.a * rimTransition);//边缘光
				#endif
				
				fanilColor = lerp(fanilColor,_OutlineColor.rgb,_OutlineColor.a * outline.r);
				fanilColor *= mainLight.color;
				fanilColor += light;
				
				return float4(fanilColor.rgb,1);
			}
            ENDHLSL
        }

		Pass
        {
            Name "Outlines"
			Tags {
				"RenderPipeline" = "UniversalRenderPipeline"
				"LightMode" = "Outlines"
				"DisableBatching" = "True"
			}
            Cull Off
            Blend Off
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.5
            
            
            // 顶点着色器
			Varyings vert(Attributes v,uint id : SV_InstanceID)
			{
				Varyings o = Vert(v,id);
			
				#if !_LEAVE_OUTLINE_ON
					VertexPositionInputs vertexInput = GetVertexPositionInputs(_position);
					#if _OUTLINE_DEPTH_ON
					o.depth = _OutlineDepth;
        			#else
        			o.depth = vertexInput.positionNDC.z / vertexInput.positionNDC.w;
					#endif
				#endif
				return o;
			}

            float4 frag(Varyings i) : SV_TARGET
			{  	
				UNITY_SETUP_INSTANCE_ID(i);
				float4 Var_MainTex = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).rgba;
				float alpha = Var_MainTex.a;
				clip(alpha - 0.01);
				return float4(i.depth,_OutlineSort,0,1);
			}
            ENDHLSL
        }

		Pass
        {
            Name "DepthNormals"
			Tags {
				"LightMode" = "DepthNormals"
			}
            Cull Off
            Blend Off
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.5
            
            
            // 顶点着色器
			Varyings vert(Attributes v,uint id : SV_InstanceID)
			{
				Varyings o = Vert(v,id);
			
				#if !_LEAVE_OUTLINE_ON
					VertexPositionInputs vertexInput = GetVertexPositionInputs(_position);
					#if _OUTLINE_DEPTH_ON
					o.depth = _OutlineDepth;
        			#else
        			o.depth = vertexInput.positionNDC.z / vertexInput.positionNDC.w;
					#endif
				#endif
				return o;
			}

            float4 frag(Varyings i) : SV_TARGET
			{  	
				UNITY_SETUP_INSTANCE_ID(i);
				float4 Var_MainTex = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).rgba;
				float alpha = Var_MainTex.a;
				clip(alpha - 0.01);
				float3 normal = normalize(i.GrassData.normal);
				return float4(normal,1);
			}
            ENDHLSL
        }

		Pass
        {
            Name "ShadowCaster"
			Tags
			{
				"LightMode" = "ShadowCaster"
			}
            Cull Off
            Blend Off
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.5
            
            
            // 顶点着色器
			Varyings vert(Attributes v,uint id : SV_InstanceID)
			{
				return Vert(v,id);
			}

            float4 frag(Varyings i) : SV_TARGET
			{  	
				UNITY_SETUP_INSTANCE_ID(i);
				float4 Var_MainTex = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).rgba;
				float alpha = Var_MainTex.a;
				clip(alpha - 0.01);
				
				return float4(1,1,1,1);
			}
            ENDHLSL
        }
    }
	CustomEditor "PixelRender.LeaveShaderGUI"
}
