Shader "Mu3Library/SimpleToon"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Main Color", Color) = (1,1,1,1)

        [Space(20)]
        _ShadingColor ("Shading Color", Color) = (0,0,0,1)
        _ShadingStep ("Shading Step", Range(1.0, 5.0)) = 2.8
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode"="ForwardBase" }

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
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;

            fixed4 _ShadingColor;
            float _ShadingStep;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldNormal = normalize(UnityObjectToWorldNormal(v.normal));
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;

                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float ndotl = dot(i.worldNormal, -lightDir);
                float normalizedNDotL = (ndotl + 1) * 0.5;

                float standardShadingRatio = 1.0 / _ShadingStep;

                float f = floor(normalizedNDotL / standardShadingRatio);
                float shading = 1.0 / (f + 1);
                fixed4 shadowCol = _ShadingColor * shading;

                return fixed4(lerp(_ShadingColor.rgb, col.rgb, shading), col.a);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
