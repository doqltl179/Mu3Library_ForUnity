Shader "_Mu3/PostEffect/DisplayWave"
{
    Properties {
        _Center("Center", Vector) = (0.5, 0.5, 0, 0)
    }

    HLSLINCLUDE
    #include "UnityCG.cginc"
    // copied from "Packages/com.unity.postprocessing/PostProcessing/Shaders/StdLib.hlsl"

    UNITY_DECLARE_TEX2D(_MainTex);

    struct VaryingsDefault
    {
        float4 vertex : SV_POSITION;
        float2 texcoord : TEXCOORD0;
        float2 texcoordStereo : TEXCOORD1;
    // #if STEREO_INSTANCING_ENABLED
    //     uint stereoTargetEyeIndex : SV_RenderTargetArrayIndex;
    // #endif
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

    float4 _Center;

    float4 Frag(VaryingsDefault i): SV_Target
    {
        float2 uv = i.texcoord;

        // 화면 중앙을 원점으로 변환
        //float2 center = float2(0.5, 0.5);
        float2 toCenter = uv - _Center;

        // 화면 비율 보정을 위해 비율 계산
        float aspectRatio = _ScreenParams.x / _ScreenParams.y;
        toCenter.x *= aspectRatio; // x 좌표를 비율에 맞게 조정

        float distance = length(toCenter);

        // 시간에 따라 변화하는 원형 웨이브
        float wave = sin(distance * 20.0 - _Time.y * 5.0) * 0.05;

        // 웨이브를 UV 좌표에 적용
        uv += normalize(toCenter) * wave / aspectRatio; // 비율에 맞게 웨이브 적용

        float4 c = _MainTex.SampleLevel(sampler_MainTex, uv, 0);
        return float4(c.r, c.r, c.r, 1.0);
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
