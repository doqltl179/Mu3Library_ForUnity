Shader "_MyCustom/PostEffect/Blur"
{
    Properties {
        _BlurAmount("Blur Amount", Range(0, 10)) = 5.0
        _Strength("Blur Strength", Range(0, 1)) = 1.0
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

    float2 TransformTriangleVertexToUV(float2 vertex)
    {
        float2 uv = (vertex + 1.0) * 0.5;
        return uv;
    }

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

    float4 Frag(VaryingsDefault i) : SV_Target
    {
        float2 uv = i.texcoord;
        float4 color = float4(0, 0, 0, 0);
        float2 offset = float2(1.0 / _ScreenParams.x, 1.0 / _ScreenParams.y) * _BlurAmount;

        for (int x = -2; x <= 2; x++)
        {
            for (int y = -2; y <= 2; y++)
            {
                color += _MainTex.SampleLevel(sampler_MainTex, uv + float2(x, y) * offset, 0);
            }
        }

        color /= 25.0; // Æò±Õ°ª °è»ê
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
                #pragma vertex VertDefault
                #pragma fragment Frag
            ENDHLSL
        }
    }
}
