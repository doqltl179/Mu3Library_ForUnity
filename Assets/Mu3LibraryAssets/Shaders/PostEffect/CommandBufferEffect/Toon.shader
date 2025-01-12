Shader "Mu3Library/PostEffect/CommandBufferEffect/Toon"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Threshold("Light Threshold", Float) = 0.5
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
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
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

            float _Threshold;

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);

                float grayScale = col.r;

                float toonValue = 0;
                if(grayScale > 0.75) {
                    toonValue = 1.0;
                }
                else if(grayScale > 0.5) {
                    toonValue = 0.75;
                }
                else if(grayScale > 0.25) {
                    toonValue = 0.5;
                }

                return fixed4(toonValue, toonValue, toonValue, 1.0);
            }
            ENDCG
        }
    }
}
