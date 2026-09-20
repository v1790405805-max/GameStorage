Shader "PixelRender/Grass"
{
    Properties
    {
        [NoScaleOffset]_MainTex ("Texture", 2D) = "white" {}
		_MainColor("MianColor",Color) = (1,1,1,1)
    	[IntRange] _SheetX("尺寸X",Range(1,8)) = 4
        [IntRange] _SheetY("尺寸Y",Range(1,8)) = 4
    	_MapTex ("MapTex",2D) = "white"{}
		[Gradient]_ShadowRampTex("阴影Ramp",2D) = "white"{}
    	_ScaleX("x",Range(0.01,5)) = 1
    	_ScaleY("y",Range(0.01,5)) = 1
    	_ShadowColor("投影颜色",Color) = (1,1,1,1)
    	[MinMax]_LightAttenuation("Attenuation Remap", Vector) = (0, 1, 0, 0)
    	_SheetSpeed("序列帧速度",Range(0,30)) = 5
    	_AngleOffset("角度偏移",Range(-180,180)) = 0
    	[HDR]_WindColor("WindColor",Color) = (1,1,1,0)
    	[MinMax]_WindAttenuation("WindAttenuatiton",Vector) = (0,1,0,0)
    	_RotateNoiseAngle("摆动噪声角度",Range(-180,180)) = 0
    	_RotateNoiseSize("摆动噪声尺寸",Range(0.1,100)) = 20
    	_RotateNoiseRatio("摆动噪声尺寸",Range(0.1,10)) = 1
    	_RotateNoiseSpeed("摆动噪声速度",Range(-3,3)) = 1
    	_RotateNoiseInt("摆动噪声强度",Range(0,180)) = 1
    	_RotateSpeed("摆动速度",Range(0,20)) = 1
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

        #pragma shader_feature _ _CIRCLE_ANGLE_ON
        
        float4 _MapTex_ST;
		float4 _MainColor;
        float4 _ShadowColor;
        float2 _LightAttenuation;
        float _SheetX;
        float _SheetY;
        float _ScaleX;
        float _ScaleY;
        float _SheetSpeed;
        float _AngleOffset;
        float4 _WindColor;
        float4 _WindAttenuation;
        float _RotateNoiseAngle;
        float _RotateNoiseSize;
        float _RotateNoiseRatio;
        float _RotateNoiseSpeed;
        float _RotateNoiseInt;
        float _RotateSpeed;
        
        TEXTURE2D(_MainTex);
        TEXTURE2D(_MapTex);
		TEXTURE2D(_ShadowRampTex);

        SAMPLER(sampler_MainTex);
        SAMPLER(sampler_MapTex);
		SAMPLER(sampler_ShadowRampTex);
        

        struct GrassData
        {
	        float3 position;
        	float scale;
        	float3 normal;
        	float sheetOffset;
        	float angleOffset;
        };
        
        struct Attributes
		{
			float3 positionOS : POSITION;
			float2 uv :TEXCOORD0;
        	float3 color : COLOR;

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
			float3 nDirWS : TEXCOORD5;
			float noise : TRXCOORD6;
			GrassData GrassData : TEXCOORD7;
			UNITY_VERTEX_INPUT_INSTANCE_ID
		};
        

        UNITY_INSTANCING_BUFFER_START(Prop)
			StructuredBuffer<GrassData> _GrassBuffer;
		UNITY_INSTANCING_BUFFER_END(Prop)

        Varyings Vert(Attributes v,uint id)
		{
			Varyings o = (Varyings)0;

				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v,o);
				o.GrassData = _GrassBuffer[id];

				float angle = o.GrassData.angleOffset * _AngleOffset / 180 * PI;
				float2 posUV = o.GrassData.position.xz / _RotateNoiseSize;
				float2 noiseY = RotateZ(float3(0,1,0),-_RotateNoiseAngle / 180 * PI).xy;
				posUV += _Time.x * _RotateNoiseSpeed * noiseY;
				posUV = RotateZ(float3(posUV,0),_RotateNoiseAngle / 180 * PI).xy;
				posUV.y *= _RotateNoiseRatio;
				float noise;
				Unity_SimpleNoise_float(posUV,10,noise);
				o.noise = noise;
				noise = (noise - 0.5) * _RotateNoiseInt;
				angle += sin(_Time.z * _RotateSpeed + o.GrassData.angleOffset * 4) * noise / 180 * PI;
				angle *= v.color.r;
				
				v.positionOS *= _GrassBuffer[id].scale * float3(_ScaleX,_ScaleY,1);
				v.positionOS = RotateZ(v.positionOS,angle);
				v.positionOS = Billboard(v.positionOS);
				v.positionOS += _GrassBuffer[id].position;
				v.positionOS = TransformWorldToObject(v.positionOS);
			
				VertexPositionInputs vertexInput = GetVertexPositionInputs(v.positionOS.xyz);
				o.posCS = TransformObjectToHClip(v.positionOS);
				o.posWS = TransformObjectToWorld(v.positionOS);
				o.posNDS = vertexInput.positionNDC;
				o.scrPos = ComputeScreenPos(TransformObjectToHClip(half3(0,0,0)));
				o.nDirWS = o.GrassData.normal;
			
				float2 newUV = v.uv;
                newUV.x = newUV.x/_SheetX + frac(floor((_Time.y + o.GrassData.angleOffset) * _SheetSpeed)/_SheetX);
                //newUV.y = newUV.y/_SheetY + 1 - frac(floor(((_Time.y + o.GrassData.AngleOffset) * _SheetSpeed)/_SheetX)/_SheetY);
				newUV.y = newUV.y/_SheetY + 1 / _SheetY *  floor(o.GrassData.sheetOffset * (_SheetY - 1));
				o.uv = newUV;
			
				o.posUV = TRANSFORM_TEX((_GrassBuffer[id].position).xz,_MapTex);
			
				return o;
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

				float3 nDirWS = i.nDirWS;
				float shadowAttenuation = 1;
				shadowAttenuation = mainLight.shadowAttenuation * mainLight.distanceAttenuation;
			    shadowAttenuation = RangeRemap(_LightAttenuation.x, _LightAttenuation.y, shadowAttenuation);

				float diffuse = 1;
				float3 diffuseColor = CalculateDiffuse(nDirWS,mainLight.direction,0,_ShadowRampTex,sampler_ShadowRampTex,diffuse);
				float3 mapColor = SAMPLE_TEXTURE2D(_MapTex,sampler_MapTex,i.posUV).rgb;
				float4 Var_MainTex = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).rgba;
				float alpha = Var_MainTex.a;
				clip(alpha - 0.01);

				float wind = RangeRemap(_WindAttenuation.x, _WindAttenuation.y, i.noise);
				float3 fanilColor = mapColor * _MainColor.rgb * diffuseColor;
				fanilColor *= lerp(_ShadowColor.rgb,float3(1,1,1),shadowAttenuation);
				fanilColor = lerp(fanilColor,_WindColor.rgb,wind * _WindColor.a);
				fanilColor *= mainLight.color;
				return float4(fanilColor.rgb,1);
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

    }
	CustomEditor "PixelRender.GrassShaderGUI"
}
