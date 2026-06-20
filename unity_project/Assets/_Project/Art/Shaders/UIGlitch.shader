// 로아 "가상공간 순간이동 등장" UI 글리치 셰이더.
// 표준 UI-Default 골격(스텐실 마스킹/클립렉트/버텍스 틴트) 위에 글리치 항을 얹는다:
//   _GlitchAmount 1 = 완전 깨짐(RGB 분할·데이터모시 블록·스캔라인·깜빡임 최대),
//   _GlitchAmount 0 = 원본 그대로. StageLayerView가 Enter 전환 동안 1→0으로 구동(broken→clean).
// 알파(페이드)는 버텍스 컬러(Image.color.a)로 들어오므로 셰이더는 변위/색분할만 담당한다.
Shader "LoveAlgo/UIGlitch"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _GlitchAmount ("Glitch Amount", Range(0,1)) = 0
        _BlockCount   ("Block Rows", Float) = 18
        _BlockShift   ("Block Max Shift", Float) = 0.34
        _RGBSplit     ("RGB Split", Float) = 0.022
        _ScanIntensity("Scanline Intensity", Range(0,1)) = 0.22
        _Flicker      ("Flicker Boost", Float) = 0.3

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            float _GlitchAmount;
            float _BlockCount;
            float _BlockShift;
            float _RGBSplit;
            float _ScanIntensity;
            float _Flicker;

            // 값싼 해시 노이즈(시간/블록 인덱스 기반 의사난수).
            float hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float g = saturate(_GlitchAmount);
                float2 uv = IN.texcoord;
                float t = _Time.y;

                // ── 데이터모시: 가로 밴드 일부를 시간에 따라 가로로 어긋나게 밀어낸다.
                float band = floor(uv.y * _BlockCount);
                float tick = floor(t * 14.0);
                float pick = hash21(float2(band, tick));
                float active = step(0.72, pick) * g;                 // 일부 밴드만, 강도는 g 비례
                float shift = (hash21(float2(band, tick + 1.0)) - 0.5) * _BlockShift * active;
                uv.x += shift;

                // ── RGB 색수차 분할: 기본 + 활성 밴드에서 가중.
                float split = _RGBSplit * g + active * 0.03;
                fixed4 col;
                col.r = tex2D(_MainTex, uv + float2(split, 0)).r;
                col.g = tex2D(_MainTex, uv).g;
                col.b = tex2D(_MainTex, uv - float2(split, 0)).b;
                col.a = tex2D(_MainTex, uv).a;
                col += _TextureSampleAdd;
                col *= IN.color;                                     // UI 틴트 + 알파(페이드)

                // ── 스캔라인: g에 비례해 홀수 라인을 약간 어둡게.
                float scan = 1.0 - _ScanIntensity * g * step(0.5, frac(uv.y * 380.0));
                col.rgb *= scan;

                // ── 깜빡임: 활성 밴드에 순간적 밝기 가산(전기 노이즈 느낌).
                col.rgb += g * active * _Flicker;

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
        ENDCG
        }
    }
}
