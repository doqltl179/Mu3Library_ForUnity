Shader "Hidden/Mu3Library/Shake"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Blend Off Cull Off

        Pass
        {
            Name "Shake"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Weight;
            float _Amplitude;
            float _Period;
            float _ElapsedTime;
            float _PhaseOffset;

            half4 Frag(Varyings input) : SV_Target
            {
                float phase = TWO_PI * (_ElapsedTime / max(_Period, 0.0001)) + _PhaseOffset;
                float2 shake = float2(
                sin(phase + 1.5) * cos(phase * 2.0),
                cos(phase * 3.0 + 2.7) * sin(phase * 2.0));
                float2 uv = clamp(input.texcoord + shake * _Amplitude * _Weight, 0.0, 1.0);
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
            }
            ENDHLSL
        }
    }
}
