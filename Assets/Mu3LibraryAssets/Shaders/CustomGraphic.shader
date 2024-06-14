Shader "_MyCustom/CustomGraphic"
{
     Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        // _Color ("Tint", Color) = (1,1,1,1)

        // _StencilComp ("Stencil Comparison", Float) = 8
        // _Stencil ("Stencil ID", Float) = 0
        // _StencilOp ("Stencil Operation", Float) = 0
        // _StencilWriteMask ("Stencil Write Mask", Float) = 255
        // _StencilReadMask ("Stencil Read Mask", Float) = 255

        // _ColorMask ("Color Mask", Float) = 15

        // [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0

        [Space(20)]
        [Toggle(USE_SHADOW)] _UseShadow ("Use Shadow", float) = 1
        _ShadowColor ("Shadow Color", Color) = (0,0,0,1)
        _ShadowHeight ("Shadow Height", Range(0, 1)) = 0.15
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 1
        _ShadowDensity ("Shadow Density", Range(1, 8)) = 1

        [Space(20)]
        [Toggle(USE_Highlight)] _UseHighlight ("Use Highlight", float) = 1
        _HighlightHeight ("Highlight Height", Range(0, 1)) = 0.15
        _HighlightStrength ("Highlight Strength", Range(0, 1)) = 0.15
        _HighlightDensity ("Highlight Density", Range(1, 64)) = 16
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        // Stencil
        // {
        //     Ref [_Stencil]
        //     Comp [_StencilComp]
        //     Pass [_StencilOp]
        //     ReadMask [_StencilReadMask]
        //     WriteMask [_StencilWriteMask]
        // }

        Cull Off
        Lighting Off
        ZWrite Off
        // ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        //ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            // #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            // #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #pragma multi_compile_local _ USE_SHADOW
            #pragma multi_compile_local _ USE_Highlight

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                // UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                // UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            // fixed4 _Color;
            // fixed4 _TextureSampleAdd;
            // float4 _ClipRect;
            float4 _MainTex_ST;

            float4 _ShadowColor;
            half _ShadowHeight;
            half _ShadowStrength;
            half _ShadowDensity;

            half _HighlightHeight;
            half _HighlightStrength;
            half _HighlightDensity;

            float inverseLerp(float a, float b, float l) {
                return (l - a) / (b - a);
            }

            v2f vert(appdata_t v)
            {
                v2f OUT;
                // UNITY_SETUP_INSTANCE_ID(v);
                // UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);

                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);

                // OUT.color = v.color * _Color;
                OUT.color = v.color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;
                half4 color = tex2D(_MainTex, IN.texcoord) * IN.color;

                // #ifdef UNITY_UI_CLIP_RECT
                // color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                // #endif

                // #ifdef UNITY_UI_ALPHACLIP
                // clip (color.a - 0.001);
                // #endif

                // Volume
                #ifdef USE_SHADOW
                fixed4 middleColor = (_ShadowColor + IN.color) * 0.5;
                fixed4 shadowCol;
                if(IN.texcoord.y < _ShadowHeight) {
                    shadowCol = color * pow(lerp(_ShadowColor, middleColor, inverseLerp(0, _ShadowHeight, IN.texcoord.y)), _ShadowDensity);
                }
                else {
                    shadowCol = color * pow(lerp(middleColor, IN.color, inverseLerp(_ShadowHeight, _ShadowHeight * 2, IN.texcoord.y)), _ShadowDensity);
                }
                color = lerp(color, shadowCol, _ShadowStrength);

                #ifdef USE_Highlight
                half highlightValue = abs(1.0 - abs(IN.texcoord.y - _HighlightHeight));
                // Highlight
                color += IN.color * pow(highlightValue, _HighlightDensity) * _HighlightStrength;
                #endif
                #endif

                // Outline


                return color;
            }
        ENDCG
        }
    }
}
