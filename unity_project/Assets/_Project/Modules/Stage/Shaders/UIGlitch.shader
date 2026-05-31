Shader "LoveAlgo/UI/Glitch"
{
    // UI Image 호환 글리치 셰이더.
    // - RGB 색수차 (Chromatic Aberration)
    // - 가로 띠 블록 변위 (Horizontal Block Displacement)
    // - 스캔라인 + 컬러 노이즈
    // 단일 파라미터 _Strength (0~1)로 모든 효과 통합 제어.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _Strength       ("Glitch Strength (0-1)", Range(0, 1)) = 0
        _RGBShift       ("RGB Shift Max", Range(0, 0.2)) = 0.04
        _BlockShift     ("Block Shift Max", Range(0, 0.3)) = 0.08
        _BlockSize      ("Block Density", Range(1, 200)) = 60
        _Scanline       ("Scanline Strength", Range(0, 1)) = 0.35
        _NoiseAmount    ("Color Noise", Range(0, 1)) = 0.15
        _TimeScale      ("Time Scale", Range(0, 60)) = 18

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
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

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
            };
            struct v2f
            {
                float4 vertex   : SV_POSITION;
                float4 worldPos : TEXCOORD1;
                float4 color    : COLOR;
                float2 uv       : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float4 _ClipRect;

            float _Strength;
            float _RGBShift;
            float _BlockShift;
            float _BlockSize;
            float _Scanline;
            float _NoiseAmount;
            float _TimeScale;

            // 빠른 의사난수 (uv + time 기반)
            float hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = v.vertex;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y * _TimeScale;
                float s = saturate(_Strength);

                // 1) 가로 띠 블록 변위
                float band = floor(i.uv.y * _BlockSize);
                float bandSeed = hash(float2(band, floor(t * 0.5)));
                // 띠의 일부만 변위 발생 (강도에 따라 더 많은 띠가 흔들림)
                float displace = (bandSeed > (1.0 - s * 0.7))
                    ? (hash(float2(band, t)) - 0.5) * 2.0 * _BlockShift * s
                    : 0.0;

                float2 uvShifted = i.uv + float2(displace, 0);

                // 2) RGB 색수차 — R/B를 좌우로, G는 원본
                float shift = _RGBShift * s;
                float2 uvR = uvShifted + float2( shift, 0);
                float2 uvB = uvShifted + float2(-shift, 0);

                fixed4 colR = tex2D(_MainTex, uvR);
                fixed4 colG = tex2D(_MainTex, uvShifted);
                fixed4 colB = tex2D(_MainTex, uvB);

                fixed4 col;
                col.r = colR.r;
                col.g = colG.g;
                col.b = colB.b;
                col.a = colG.a; // 알파는 원본 기준

                // 3) 스캔라인 (가로 줄)
                float scan = sin(i.uv.y * _BlockSize * 6.2831 + t) * 0.5 + 0.5;
                col.rgb *= lerp(1.0, scan, _Scanline * s);

                // 4) 컬러 노이즈 (픽셀 RGB jitter)
                float n = hash(i.uv * 1000.0 + t);
                col.rgb += (n - 0.5) * _NoiseAmount * s;

                col *= i.color;

                // UI Mask/Clip 대응
                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(i.worldPos.xy, _ClipRect);
                #endif

                return col;
            }
            ENDCG
        }
    }
}
