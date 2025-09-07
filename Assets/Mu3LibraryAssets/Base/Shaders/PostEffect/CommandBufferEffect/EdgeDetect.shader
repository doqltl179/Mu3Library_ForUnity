Shader "Mu3Library/PostEffect/CommandBufferEffect/EdgeDetect"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        // _CameraDepthTexture ("Depth Texture", 2D) = "white" {}
        _EdgeColor("Edge Color", Color) = (0, 0, 0, 1)
        _EdgeFactor("Edge Factor", Range(0.0, 1.0)) = 0.1
        _EdgeThickness("Edge Thickness", Range(1, 10)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            sampler2D _CameraDepthTexture;
            float4 _CameraDepthTexture_ST;

            /*
            [ _ProjectionParams ]
            x: 현재 렌더링 중인 투영 행렬(projection matrix)이 뒤집혔는지 여부를 나타냄. 1.0: 안뒤집힘, -1.0: 뒤집힘
            y: 카메라의 Near Plane 값을 나타냄.
            z: 카메라의 Far Plane 값을 나타냄.
            w: Far Plane의 역수(Reciprocal) 값. (1.0 / Far Plane)
            */

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                if(_ProjectionParams.x > 0) {
                    o.uv.y = 1.0 - o.uv.y;
                }
                return o;
            }

            float4 _EdgeColor;
            float _EdgeFactor;
            float _EdgeThickness;

            float RecalculateDepthColor(float input) {
                float diff = 1.0 - input;
                return 1.0 - exp(log(diff) * (_ProjectionParams.z * _EdgeFactor));
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 depthTexUV = float2(i.uv.x, 1.0 - i.uv.y);
                float depthCenter = tex2D(_CameraDepthTexture, depthTexUV).r;
                depthCenter = RecalculateDepthColor(depthCenter);

                float uvThicX = _EdgeThickness / _ScreenParams.x;
                float uvThicY = _EdgeThickness / _ScreenParams.y;
                // 8방향 오프셋 정의: 상하좌우 + 대각선
                float2 offsets[8] = {
                    float2(-uvThicX, 0),  // 왼쪽
                    float2(uvThicX, 0),   // 오른쪽
                    float2(0, -uvThicY),  // 아래쪽
                    float2(0, uvThicY),   // 위쪽
                    float2(-uvThicX, -uvThicY), // 왼쪽 아래
                    float2(-uvThicX, uvThicY),  // 왼쪽 위
                    float2(uvThicX, -uvThicY),  // 오른쪽 아래
                    float2(uvThicX, uvThicY)    // 오른쪽 위
                };
    
                float isEdge = 0.0;
    
                // 8방향 샘플링
                for (int k = 0; k < 8; k++)
                {
                    float2 offsetUV = depthTexUV + offsets[k];
                    float depthNeighbor = tex2D(_CameraDepthTexture, offsetUV).r;
                    depthNeighbor = RecalculateDepthColor(depthNeighbor);
                    if (abs(depthCenter - depthNeighbor) > 0.01)
                    {
                        isEdge = 1.0;

                        break;
                    }
                }
    
                // 경계인지 확인 후 색상 반환
                if (isEdge > 0.0)
                {
                    return _EdgeColor;
                }
                else
                {
                    return tex2D(_MainTex, i.uv);
                }
            }
            ENDCG
        }
    }
}
