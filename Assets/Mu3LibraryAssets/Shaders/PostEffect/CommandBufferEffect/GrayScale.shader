Shader "Mu3Library/PostEffect/CommandBufferEffect/GrayScale"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Strength("GrayScale Strength", Range(0, 1)) = 1.0
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

            float _Strength;

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);

                float grayScale = (col.r + col.g + col.b) / 3.0;
                float3 grayColor = float3(grayScale, grayScale, grayScale);

                return float4(lerp(col.rgb, grayColor, _Strength), col.a);
            }
            ENDCG
        }
    }
}
