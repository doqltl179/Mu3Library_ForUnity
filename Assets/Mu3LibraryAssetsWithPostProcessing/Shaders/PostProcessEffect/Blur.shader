Shader "Mu3Library/PostEffect/PostProcessEffect/Blur"
{
    Properties {
        _Strength("Blur Strength", Range(0, 1)) = 1.0
        _BlurAmount("Blur Amount", Range(0, 10)) = 5.0
        _KernelSize("Blur Kernel Size", Range(1, 10)) = 2
    }

    HLSLINCLUDE
    #include "UnityCG.cginc"

    struct appdata
    {
        float3 vertex : POSITION;
    };

    struct v2f
    {
        float4 vertex : SV_POSITION;
        float2 uv : TEXCOORD0;
    };

    float2 TransformTriangleVertexToUV(float2 vertex)
    {
        float2 uv = (vertex + 1.0) * 0.5;
        return uv;
    }

    v2f vert(appdata v)
    {
        v2f o;
        o.vertex = float4(v.vertex.xy, 0.0, 1.0);

        o.uv = TransformTriangleVertexToUV(v.vertex.xy);
    #if UNITY_UV_STARTS_AT_TOP
        o.uv = o.uv * float2(1.0, -1.0) + float2(0.0, 1.0);
    #endif

        return o;
    }

    UNITY_DECLARE_TEX2D(_MainTex);

    float _BlurAmount;
    float _Strength;
    int _KernelSize;

    float4 frag(v2f i) : SV_Target
    {
        float2 uv = i.uv;
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
                #pragma vertex vert
                #pragma fragment frag
            ENDHLSL
        }
    }
}
