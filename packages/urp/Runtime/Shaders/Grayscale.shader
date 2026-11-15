Shader "Hidden/Mu3Library/Grayscale"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Blend Off Cull Off

        Pass
        {
            Name "Grayscale"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Weight;

            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
                half gray = dot(color.rgb, half3(0.2126, 0.7152, 0.0722));
                color.rgb = lerp(color.rgb, half3(gray, gray, gray), _Weight);
                return color;
            }
            ENDHLSL
        }
    }
}
