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

            half4 Frag(Varyings input) : SV_Target
            {
                float t = _Time.y;
                float2 shake = float2(
                sin(t * 37.3 + 1.5) * cos(t * 19.7),
                cos(t * 23.1 + 2.7) * sin(t * 41.3));
                float2 uv = clamp(input.texcoord + shake * _Amplitude * _Weight, 0.0, 1.0);
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
            }
            ENDHLSL
        }
    }
}
