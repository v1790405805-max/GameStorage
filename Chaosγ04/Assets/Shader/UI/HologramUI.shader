Shader "Custom/UI/HologramUI"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _Color ("Main HDR Tint", Color) = (0.0, 0.85, 1.0, 1.0)
        
        [Header(Blend Mode)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10

        [Header(Scanlines)]
        _ScanlineDensity ("Scanline Density", Range(10, 500)) = 180
        _ScanlineStrength ("Scanline Strength", Range(0, 1)) = 0.25
        _ScanlineSpeed ("Scanline Scroll Speed", Range(-10, 10)) = 1.5

        [Header(Holo Glint)]
        [HDR] _GlintColor ("Glint HDR Color", Color) = (0.5, 1.8, 2.0, 1.0)
        _GlintWidth ("Glint Width", Range(0.01, 0.5)) = 0.08
        _GlintSpeed ("Glint Speed", Range(0, 5)) = 0.6
        _GlintAngle ("Glint Angle", Range(-2, 2)) = 0.5

        [Header(UI Mask Support)]
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend [_SrcBlend] [_DstBlend]
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float4 _ClipRect;

            float _ScanlineDensity;
            float _ScanlineStrength;
            float _ScanlineSpeed;

            float4 _GlintColor;
            float _GlintWidth;
            float _GlintSpeed;
            float _GlintAngle;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.screenPos = ComputeScreenPos(OUT.vertex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 texColor = tex2D(_MainTex, IN.texcoord);
                half4 color = texColor * IN.color;

                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                float scanline = sin((screenUV.y * _ScanlineDensity) + (_Time.y * _ScanlineSpeed));
                scanline = lerp(1.0 - _ScanlineStrength, 1.0, scanline * 0.5 + 0.5);
                color.rgb *= scanline;

                float glintCoord = IN.texcoord.x + IN.texcoord.y * _GlintAngle;
                float glintCycle = frac(_Time.y * _GlintSpeed);
                float glintPos = lerp(-0.5, 2.0, glintCycle);
                float dist = abs(glintCoord - glintPos);
                float glintMask = smoothstep(_GlintWidth, 0.0, dist);

                color.rgb += _GlintColor.rgb * glintMask * texColor.a * IN.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                return color;
            }
            ENDCG
        }
    }
}