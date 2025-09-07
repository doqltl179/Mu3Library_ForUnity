Shader "Mu3Library/PostEffect/RenderingPipeline/Toon"
{
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
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _CameraGBufferTexture0; // Diffuse color (RGB), occlusion (A)
            sampler2D _CameraGBufferTexture2; // World space normal (RGB), unused (A)

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = o.vertex.xy / o.vertex.w * 0.5 + 0.5; // Clip Space to UV
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 normals = tex2D(_CameraGBufferTexture2, i.uv); // Normals

                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float ndotl = dot(normals, -lightDir);
                float normalizedNDotL = (ndotl + 1) * 0.5;

                float shadingStep = 2.8;
                float standardShadingRatio = 1.0 / shadingStep;

                float f = floor(normalizedNDotL / standardShadingRatio);
                float shading = 1.0 / (f + 1);

                fixed4 col = tex2D(_CameraGBufferTexture0, i.uv);

                return col * shading;
            }
            ENDCG
        }
    }
}
