Shader "_MyCustom/Grid"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}

        [HDR] _GridColor ("Color", Color) = (1,1,1,1)
        _GridDist ("Grid Distance", Range(0.01, 100.0)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf NoLighting noambient noshadow novertexlights nolightmap noforwardadd nometa
        #pragma target 3.0

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
        };

        fixed4 _Color;

        fixed4 _GridColor;
        float _GridDist;

        fixed4 LightingNoLighting(SurfaceOutput s, fixed3 lightDir, fixed atten) {
            return fixed4(s.Albedo, s.Alpha);
        }

        float inverseLerp(float from, float to, float value) {
            return (value - from) / (to - from);
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex);

            float normalizedX = inverseLerp(0.0, _GridDist, abs(IN.worldPos.x % _GridDist));
            float normalizedZ = inverseLerp(0.0, _GridDist, abs(IN.worldPos.z % _GridDist));
            const float offset = 0.05;

            bool drawGridX = normalizedX < offset || normalizedX > 1.0 - offset;
            bool drawGridZ = normalizedZ < offset || normalizedZ > 1.0 - offset;
            if(!drawGridX && !drawGridZ) {
                c *= _Color;
            }
            else {
                if(drawGridX) {
                    if(normalizedX > 0.5) normalizedX = 1.0 - normalizedX;
                    float colorStrength = 1.0 - (normalizedX / offset);
                    c *= lerp(_Color, _GridColor, colorStrength);
                }
                if(drawGridZ) {
                    if(normalizedZ > 0.5) normalizedZ = 1.0 - normalizedZ;
                    float colorStrength = 1.0 - (normalizedZ / offset);
                    c *= lerp(_Color, _GridColor, colorStrength);
                }
            }

            o.Albedo = c.rgb;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
