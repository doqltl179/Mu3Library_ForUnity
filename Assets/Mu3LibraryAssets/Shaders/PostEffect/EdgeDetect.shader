Shader "Mu3Library/PostEffect/EdgeDetect"
{
    Properties
    {
        _EdgeColor("Edge Color", Color) = (0, 0, 0, 1)
        _EdgeThickness("Edge Thickness", Range(1, 10)) = 1.0
    }

    HLSLINCLUDE
    #include "UnityCG.cginc"

    UNITY_DECLARE_TEX2D(_MainTex);
    UNITY_DECLARE_TEX2D(_CameraDepthTexture);

    struct VaryingsDefault
    {
        float4 vertex : SV_POSITION;
        float2 texcoord : TEXCOORD0;
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
        o.vertex = float4(v.vertex, 1.0);

        o.texcoord = TransformTriangleVertexToUV(v.vertex.xy);
    #if UNITY_UV_STARTS_AT_TOP
        o.texcoord = o.texcoord * float2(1.0, -1.0) + float2(0.0, 1.0);
    #endif

        return o;
    }

    float4 _EdgeColor;
    float _EdgeThickness;

    float4 Frag(VaryingsDefault i) : SV_Target
    {
        float2 uv = i.texcoord;
        float depthCenter = _CameraDepthTexture.Sample(sampler_CameraDepthTexture, uv).r;
    
        // 8방향 오프셋 정의: 상하좌우 + 대각선
        float2 offsets[8] = {
            float2(-_EdgeThickness / _ScreenParams.x, 0),  // 왼쪽
            float2(_EdgeThickness / _ScreenParams.x, 0),   // 오른쪽
            float2(0, -_EdgeThickness / _ScreenParams.y),  // 아래쪽
            float2(0, _EdgeThickness / _ScreenParams.y),   // 위쪽
            float2(-_EdgeThickness / _ScreenParams.x, -_EdgeThickness / _ScreenParams.y), // 왼쪽 아래
            float2(-_EdgeThickness / _ScreenParams.x, _EdgeThickness / _ScreenParams.y),  // 왼쪽 위
            float2(_EdgeThickness / _ScreenParams.x, -_EdgeThickness / _ScreenParams.y),  // 오른쪽 아래
            float2(_EdgeThickness / _ScreenParams.x, _EdgeThickness / _ScreenParams.y)    // 오른쪽 위
        };
    
        float isEdge = 0.0;
    
        // 8방향 샘플링
        for (int k = 0; k < 8; k++)
        {
            float depthNeighbor = _CameraDepthTexture.Sample(sampler_CameraDepthTexture, uv + offsets[k]).r;
            if (abs(depthCenter - depthNeighbor) > 0.01)
            {
                isEdge = 1.0;
            }
        }
    
        // 경계인지 확인 후 색상 반환
        if (isEdge > 0.0)
        {
            return _EdgeColor;
        }
        else
        {
            return _MainTex.Sample(sampler_MainTex, uv);
        }
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
                #pragma vertex VertDefault
                #pragma fragment Frag
            ENDHLSL
        }
    }
}