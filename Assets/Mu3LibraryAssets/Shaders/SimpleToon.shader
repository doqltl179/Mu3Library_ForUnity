Shader "Mu3Library/SimpleToon"
{
    Properties
    {
        _MainColor ("Main Color", Color) = (1, 1, 1, 1)
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

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

                float3 normal : NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;

                float3 normal : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
            };

            float InverseLerp(float from, float to, float a) {
                return (a - from) / (to - from);
            }

            sampler2D _MainTex;
            float4 _MainTex_ST;

            fixed4 _MainColor;

            v2f vert (appdata v)
            {
                v2f o;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                o.normal = normalize(mul(v.normal, (float3x3)unity_WorldToObject));
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * _MainColor;

                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);

                float colorStrength = (dot(lightDir, i.normal) + 1) * 0.5;
                if(colorStrength > 0.75) colorStrength = 1.0;
                else if(colorStrength > 0.5) colorStrength = 0.75;
                else if(colorStrength > 0.25) colorStrength = 0.5;
                else colorStrength = 0.25;
                // if(colorStrength > 0.8) colorStrength = 1.0;
                // else if(colorStrength > 0.6) colorStrength = 0.8;
                // else if(colorStrength > 0.4) colorStrength = 0.6;
                // else if(colorStrength > 0.2) colorStrength = 0.4;
                // else colorStrength = 0.2;

                return col * colorStrength;
            }
            ENDCG
        }
    }
}
