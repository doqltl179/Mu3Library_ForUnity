Shader "Mu3Library/PostEffect/PostProcessEffect/GrayScale"
{
    Properties {
        _Strength("GrayScale Strength", Range(0, 1)) = 1.0
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
        o.vertex = float4(v.vertex, 1.0);
        
        o.uv = TransformTriangleVertexToUV(v.vertex.xy);
    #if UNITY_UV_STARTS_AT_TOP
        o.uv = o.uv * float2(1.0, -1.0) + float2(0.0, 1.0);
    #endif

        return o;
    }

    UNITY_DECLARE_TEX2D(_MainTex);

    float _Strength;

    float4 frag(v2f i) : SV_Target
    {
        float2 uv = i.uv;
        float4 color = _MainTex.SampleLevel(sampler_MainTex, uv, 0);

        float grayScale = (color.x + color.y + color.z) / 3.0;
        float3 grayColor = float3(grayScale, grayScale, grayScale);

        return float4(lerp(color.xyz, grayColor, _Strength), color.w);
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
