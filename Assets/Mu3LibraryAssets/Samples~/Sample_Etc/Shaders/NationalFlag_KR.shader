Shader "Mu3Library/NationalFlag/KR"
{
    Properties{
        _BackgroundColor ("Background Color", Color) = (1, 1, 1, 1)
        _UpperCircleColor ("Upper Circle Color", Color) = (1, 0, 0, 1)
        _LowerCircleColor ("Lower Circle Color", Color) = (0, 0, 1, 1)
        _CheckersColor ("Checkers Color", Color) = (0, 0, 0, 1)
        _OutRangeColor ("Out Range Color", Color) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            #define PI 3.14159265

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

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float CheckSide(float2 a, float2 b) {
                float cross = a.x * b.y - a.y * b.x;
            
                // 왼쪽
                if (cross > 0.0) {
                    return 1.0;
                }
                // 오른쪽
                if (cross < 0.0) {
                    return -1.0;
                }

                return 0.0;
            }

            fixed4 _BackgroundColor;
            fixed4 _UpperCircleColor;
            fixed4 _LowerCircleColor;
            fixed4 _CheckersColor;
            fixed4 _OutRangeColor;

            fixed4 frag (v2f i) : SV_Target
            {
                // 국기의 가로세로 비율은 "3:2"다.
                float flagAspect = 3.0 / 2.0;

                float3 objectScale = float3(
                    length(float3(unity_ObjectToWorld._m00, unity_ObjectToWorld._m10, unity_ObjectToWorld._m20)), // X축 스케일
                    length(float3(unity_ObjectToWorld._m01, unity_ObjectToWorld._m11, unity_ObjectToWorld._m21)), // Y축 스케일
                    length(float3(unity_ObjectToWorld._m02, unity_ObjectToWorld._m12, unity_ObjectToWorld._m22))  // Z축 스케일
                );
                float scaleAspect = objectScale.x / objectScale.y;

                float2 aspectUV = float2(1, 1);
                if(flagAspect < scaleAspect) {
                    aspectUV.y = flagAspect / scaleAspect;
                    // 색깔 계산 범위 체크
                    float outRatio = (1.0 - aspectUV.y) * 0.5;
                    if(i.uv.x < outRatio || 1.0 - outRatio < i.uv.x) {
                        return _OutRangeColor;
                    }
                }
                else {
                    aspectUV.x = scaleAspect / flagAspect;
                    // 색깔 계산 범위 체크
                    float outRatio = (1.0 - aspectUV.x) * 0.5;
                    if(i.uv.y < outRatio || 1.0 - outRatio < i.uv.y) {
                        return _OutRangeColor;
                    }
                }

                // uv의 중심을 (0, 0)으로 이동
                float2 uvPivotToCenter = i.uv - 0.5;

                // 완벽한 원을 그리기 위해 화면 비율 조정
                uvPivotToCenter.x *= objectScale.x / objectScale.y;
                // 현재 uv의 거리값
                float uvDist = distance(uvPivotToCenter, float2(0, 0));

                // 국기의 대각선 각도
                float flagAngle = atan2(2.0, 3.0);
                // 현재 uv의 각도
                float uvAngle = atan2(uvPivotToCenter.y, uvPivotToCenter.x);

                float2 flagDirLT = float2(cos(PI - flagAngle), sin(PI - flagAngle));
                float2 flagDirLB = float2(cos(flagAngle - PI), sin(flagAngle - PI));
                float2 flagDirRT = float2(cos(flagAngle), sin(flagAngle));
                float2 flagDirRB = float2(cos(-flagAngle), sin(-flagAngle));

                // 중앙원의 반지름 값
                float radius = aspectUV.x / 2.0 * 0.5;
                // 반지름의 반지름
                float rr = radius * 0.5;

                float2 normalizedUvCenter = normalize(uvPivotToCenter);

                // 중앙원
                if(uvDist < radius) {
                    // 하단
                    if(CheckSide(flagDirLT, normalizedUvCenter) > 0) {
                        float2 uvLT = flagDirLT * rr;
                        if(distance(uvPivotToCenter, uvLT) < rr) {
                            return _UpperCircleColor;
                        }
                        else {
                            return _LowerCircleColor;
                        }
                    }
                    // 상단
                    if(CheckSide(flagDirRB, normalizedUvCenter) > 0) {
                        float2 uvRB = flagDirRB * rr;
                        if(distance(uvPivotToCenter, uvRB) < rr) {
                            return _LowerCircleColor;
                        }
                        else {
                            return _UpperCircleColor;
                        }
                    }
                }

                float sideDistMin = radius + rr;
                // 건곤감리
                if(sideDistMin < uvDist) {
                    // 두께
                    float thickness = radius * 2.0 / 12.0;
                    // 간격
                    float space = radius * 2.0 / 24.0;
                    for(int i = 0; i < 4; i++) {
                        float2 flagDir = float2(0, 0);
                        if(i == 0) flagDir = flagDirLT;
                        else if(i == 1) flagDir = flagDirLB;
                        else if(i == 2) flagDir = flagDirRT;
                        else if(i == 3) flagDir = flagDirRB;

                        float2 rightAngleDir = float2(flagDir.y, -flagDir.x);

                        // j => 0: 안쪽
                        // j => 1: 중간
                        // j => 2: 바깥쪽
                        for(int j = 0; j < 3; j++) {
                            float forwardDistMin = sideDistMin + thickness * j + space * j;
                            float2 localMin = uvPivotToCenter - flagDir * forwardDistMin;
                            float dotForwardMin = dot(localMin, flagDir);
                            float dotRightMin = dot(localMin, rightAngleDir);
                            if(0 < dotForwardMin && abs(dotRightMin) < rr) {
                                float forwardDistMax = sideDistMin + thickness * (j + 1) + space * j;
                                float2 localMax = uvPivotToCenter - flagDir * forwardDistMax;
                                float dotForwardMax = dot(localMax, flagDir);
                                // 여기서 건곤감리를 그린다.
                                if(dotForwardMax < 0) {
                                    fixed4 returnCol = _CheckersColor;

                                    if(i == 0) {
                                        // 아무것도 하지 않음
                                    }
                                    else if(i == 1) {
                                        if(j == 1 && abs(dotRightMin) < space * 0.5) {
                                            returnCol = _BackgroundColor;
                                        }
                                    }
                                    else if(i == 2) {
                                        if((j == 0 || j == 2) && abs(dotRightMin) < space * 0.5) {
                                            returnCol = _BackgroundColor;
                                        }
                                    }
                                    else if(i == 3) {
                                        if(abs(dotRightMin) < space * 0.5) {
                                            returnCol = _BackgroundColor;
                                        }
                                    }

                                    return returnCol;
                                }
                            }
                        }
                    }
                }

                return _BackgroundColor;
            }
            ENDCG
        }
    }
}
