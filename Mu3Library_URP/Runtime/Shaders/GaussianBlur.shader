Shader "Hidden/Mu3Library/GaussianBlur"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Blend Off Cull Off

        Pass
        {
            Name "GaussianBlur"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Weight;
            float _BlurRadius;

            half4 Frag(Varyings input) : SV_Target
            {
                half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
                float2 texelOffset = _BlitTexture_TexelSize.xy * _BlurRadius;
                const float kernel0 = 0.0625;
                const float kernel1 = 0.25;
                const float kernel2 = 0.375;

                float3 accumulated = 0.0;
                float totalWeight = 0.0;

                [unroll]
                for (int y = -2; y <= 2; y++)
                {
                    float weightY = abs(y) == 2 ? kernel0 : (abs(y) == 1 ? kernel1 : kernel2);

                    [unroll]
                    for (int x = -2; x <= 2; x++)
                    {
                        float weightX = abs(x) == 2 ? kernel0 : (abs(x) == 1 ? kernel1 : kernel2);
                        float weight = weightX * weightY;
                        float2 sampleUv = input.texcoord + (float2(x, y) * texelOffset);

                        accumulated += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, sampleUv).rgb * weight;
                        totalWeight += weight;
                    }
                }

                float3 blurred = accumulated / max(totalWeight, 0.0001);
                source.rgb = lerp(source.rgb, blurred, saturate(_Weight));

                return source;
            }
            ENDHLSL
        }
    }
}