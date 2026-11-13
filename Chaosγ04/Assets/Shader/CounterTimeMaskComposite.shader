Shader "Hidden/Chaos/CounterTimeMaskComposite"
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
            Name "CounterTimeMaskComposite"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_CounterTimeMaskTexture);
            SAMPLER(sampler_CounterTimeMaskTexture);
            float _CounterTimeDarkenAmount;

            float4 Frag(Varyings input) : SV_Target
            {
                float3 sceneColor = SAMPLE_TEXTURE2D_X(
                    _BlitTexture,
                    sampler_LinearClamp,
                    input.texcoord).rgb;
                float mask = SAMPLE_TEXTURE2D(
                    _CounterTimeMaskTexture,
                    sampler_CounterTimeMaskTexture,
                    input.texcoord).a;
                float brightness =
                    1.0 - _CounterTimeDarkenAmount * (1.0 - mask);
                return float4(sceneColor * brightness, 1.0);
            }
            ENDHLSL
        }
    }
}
