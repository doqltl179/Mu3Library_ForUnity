Shader "Mu3Library/PostEffect/CommandBufferEffect/Blur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Strength("Blur Strength", Range(0, 1)) = 1.0
        _BlurAmount("Blur Amount", Range(0, 10)) = 5.0
        _KernelSize("Blur Kernel Size", Range(1, 10)) = 2
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

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                if(_ProjectionParams.x > 0) {
                    o.uv.y = 1.0 - o.uv.y;
                }
                return o;
            }

            float _Strength;
            float _BlurAmount;
            float _KernelSize;

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 blurCol = fixed4(0, 0, 0, 0);
                float2 offset = float2(1.0 / _ScreenParams.x, 1.0 / _ScreenParams.y) * _BlurAmount;

                for (int x = -_KernelSize; x <= _KernelSize; x++)
                {
                    for (int y = -_KernelSize; y <= _KernelSize; y++)
                    {
                        float2 offsetUV = i.uv + float2(x, y) * offset;
                        blurCol += tex2D(_MainTex, offsetUV);
                    }
                }

                int kernalLength = _KernelSize * 2 + 1;
                int kernalPixelCount = kernalLength * kernalLength;
                blurCol /= kernalPixelCount; // 평균값 계산

                fixed4 col = tex2D(_MainTex, i.uv);

                return lerp(col, blurCol, _Strength);
            }
            ENDCG
        }
    }
}
