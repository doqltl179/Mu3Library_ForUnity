Shader "Mu3Library/PostEffect/Blur"
{
    Properties {
        _BlurAmount("Blur Amount", Range(0, 10)) = 5.0
        _Strength("Blur Strength", Range(0, 1)) = 1.0
        _KernelSize("Blur Kernel Size", Range(1, 10)) = 2
    }

    HLSLINCLUDE
    #include "UnityCG.cginc"

    UNITY_DECLARE_TEX2D(_MainTex);

    struct VaryingsDefault
    {
        float4 vertex : SV_POSITION;
        float2 texcoord : TEXCOORD0;
        float2 texcoordStereo : TEXCOORD1;
    };

    // float2 TransformTriangleVertexToUV(float2 vertex)
    // {
    //     float2 uv = (vertex + 1.0) * 0.5;
    //     return uv;
    // }

    struct AttributesDefault
    {
        float3 vertex : POSITION;
    };

    VaryingsDefault VertDefault(AttributesDefault v)
    {
        VaryingsDefault o;
        o.vertex = float4(v.vertex.xy, 0.0, 1.0);
        o.texcoord = TransformTriangleVertexToUV(v.vertex.xy);

    #if UNITY_UV_STARTS_AT_TOP
        o.texcoord = o.texcoord * float2(1.0, -1.0) + float2(0.0, 1.0);
    #endif

        o.texcoordStereo = TransformStereoScreenSpaceTex(o.texcoord, 1.0);

        return o;
    }

    float _BlurAmount;
    float _Strength;
    int _KernelSize;

    float4 Frag(VaryingsDefault i) : SV_Target
    {
        float2 uv = i.texcoord;

        if(_Strength > 0) {
            float4 color = float4(0, 0, 0, 0);
            float2 offset = float2(1.0 / _ScreenParams.x, 1.0 / _ScreenParams.y) * _BlurAmount;

            for (int x = -_KernelSize; x <= _KernelSize; x++)
            {
                for (int y = -_KernelSize; y <= _KernelSize; y++)
                {
                    color += _MainTex.SampleLevel(sampler_MainTex, uv + float2(x, y) * offset, 0);
                }
            }

            int kernalLength = _KernelSize * 2 + 1;
            int kernalPixelCount = kernalLength * kernalLength;
            color /= kernalPixelCount; // 평균값 계산
            return lerp(_MainTex.SampleLevel(sampler_MainTex, uv, 0), color, _Strength);
        }
        else {
            float4 color = _MainTex.SampleLevel(sampler_MainTex, uv, 0);

            return color;
        }
    }
    ENDHLSL

    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Tags
        {
            "RenderType" = "Opaque"
        }
        Pass
        {
            HLSLPROGRAM
                #pragma vertex VertDefault
                #pragma fragment Frag
            ENDHLSL
        }
    }
}
