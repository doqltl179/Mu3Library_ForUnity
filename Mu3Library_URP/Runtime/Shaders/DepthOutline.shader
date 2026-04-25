Shader "Hidden/Mu3Library/DepthOutline"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Blend Off Cull Off

        Pass
        {
            Name "DepthOutline"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float _Weight;
            float _DepthThreshold;
            float _OutlineStrength;
            float _OutlineThickness;

            float SampleLinearDepthAtUv(float2 uv)
            {
                return Linear01Depth(SampleSceneDepth(uv), _ZBufferParams);
            }

            float GetDepthEdge(float2 uv, float2 texelSize)
            {
                float center = SampleLinearDepthAtUv(uv);
                float left = SampleLinearDepthAtUv(uv - float2(texelSize.x, 0.0));
                float right = SampleLinearDepthAtUv(uv + float2(texelSize.x, 0.0));
                float up = SampleLinearDepthAtUv(uv + float2(0.0, texelSize.y));
                float down = SampleLinearDepthAtUv(uv - float2(0.0, texelSize.y));

                return max(abs(left - right), abs(up - down)) + abs(center - left) + abs(center - up);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);

                float2 texelSize = _BlitTexture_TexelSize.xy * max(_OutlineThickness, 1.0);
                float edgeMetric = GetDepthEdge(input.texcoord, texelSize) * 120.0;
                float threshold = lerp(0.02, 1.2, saturate(_DepthThreshold));
                float edgeMask = smoothstep(threshold, threshold + 0.15, edgeMetric);
                float edgeBlend = saturate(edgeMask * _OutlineStrength);

                float3 outlined = lerp(source.rgb, 0.0, edgeBlend);
                source.rgb = lerp(source.rgb, outlined, saturate(_Weight));

                return source;
            }
            ENDHLSL
        }
    }
}