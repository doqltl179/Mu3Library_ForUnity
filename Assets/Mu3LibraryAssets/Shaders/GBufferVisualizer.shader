Shader "Mu3Library/GBufferVisualizer"
{
    Properties
    {
        /*
        0: Deferred shading G-buffer (typically diffuse color).
        1: Deferred shading G-buffer (typically specular + roughness).
        2: Deferred shading G-buffer (typically normals).
        3: Deferred shading G-buffer (typically emission/lighting).
        4: Deferred shading G-buffer (typically occlusion mask for static lights if any).
        5: G-buffer Available.
        6: G-buffer Available.
        7: G-buffer Available.
        */
        [IntRange] _GBufferIndex("GBuffer Index", Range(0, 7)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Name "GBufferVisualizer"
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _CameraGBufferTexture0; // Diffuse color (RGB), occlusion (A)
            sampler2D _CameraGBufferTexture1; // Specular color(RGB), smoothness (A)
            sampler2D _CameraGBufferTexture2; // World space normal (RGB), unused (A)
            sampler2D _CameraGBufferTexture3; // Emission + lighting + lightmaps + reflection probes buffer.
            float _GBufferIndex;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                /*
                [ Perspective Projection을 위한 계산 ]

                o.vertex.w는 투영 행렬에 의해 계산된 값으로, 원근감을 나타낸다.

                ex)
                x_ndc = x_clip / w
                y_ndc = y_clip / w
                z_ndc = z_clip / w

                가까운 물체와 먼 물체의 Clip Space x 값이 동일하더라도, w 값에 따라 화면에 표시되는 좌표가 달라진다.
                - 가까운 물체: w 값이 작아, x / w 결과가 크게 나타남.
                - 먼 물체: w 값이 커서, x / w 결과가 작아짐.
                */
                o.uv = o.vertex.xy / o.vertex.w * 0.5 + 0.5; // Clip Space to UV
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                if (_GBufferIndex == 0.0)
                {
                    return tex2D(_CameraGBufferTexture0, i.uv); // Albedo
                }
                else if (_GBufferIndex == 1.0)
                {
                    return tex2D(_CameraGBufferTexture1, i.uv); // Normals
                }
                else if (_GBufferIndex == 2.0)
                {
                    return tex2D(_CameraGBufferTexture2, i.uv); // Specular
                }
                else if (_GBufferIndex == 3.0)
                {
                    return tex2D(_CameraGBufferTexture3, i.uv); // Emission
                }
                return float4(1, 0, 1, 1); // Error color (magenta)
            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}
