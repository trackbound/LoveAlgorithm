// 캐릭터 외곽 글리치 셰이더(CharFX). 스크린 글리치(UIGlitch)와 달리 변위·고스트를 스프라이트
// 알파(실루엣)와 외곽에 집중시켜, 캐릭터 윤곽이 가로로 찢기고 색수차 잔상이 외곽을 감싸며
// 배경(투명)은 건드리지 않는다. _GlitchAmount 1=완전 깨짐 → 0=원본. StageView가 로아 Enter에 1→0 구동.
Shader "LoveAlgo/UICharacterGlitch"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _GlitchAmount ("Glitch Amount", Range(0,1)) = 0
        _SliceCount   ("Slice Rows", Float) = 120
        _SliceSpeed   ("Slice Speed", Float) = 22
        _SliceThreshold ("Slice Sparsity", Range(0,1)) = 0.6
        _SliceShift   ("Slice Max Shift", Float) = 0.06
        _RGBSplit     ("Body Chroma Split", Float) = 0.01
        _ScanIntensity("Scanline Intensity", Range(0,1)) = 0.25
        _ScanDensity  ("Scanline Density", Float) = 300
        _EchoOffset   ("Echo Offset", Float) = 0.02
        _EchoStrength ("Echo Strength", Float) = 0.85
        _EchoColorA   ("Echo Color L", Color) = (0.30, 0.90, 1.0, 1)
        _EchoColorB   ("Echo Color R", Color) = (1.0, 0.30, 0.85, 1)

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
            float _SliceCount;
            float _SliceSpeed;
            float _SliceThreshold;
            float _SliceShift;
            float _RGBSplit;
            float _ScanIntensity;
            float _ScanDensity;
            float _EchoOffset;
            float _EchoStrength;
            fixed4 _EchoColorA;
            fixed4 _EchoColorB;

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

                // ── 외곽 찢김: 촘촘한 가로 슬라이스 일부를 가로로 어긋나게. 솔리드 내부에선 안 보이고
                //    실루엣 경계(외곽)에서만 어긋남이 드러나 윤곽이 찢긴다.
                float row = floor(uv.y * _SliceCount);
                float tick = floor(t * _SliceSpeed);
                float n = hash21(float2(row, tick));
                float act = step(_SliceThreshold, n) * g;
                float shift = (hash21(float2(row, tick + 1.0)) - 0.5) * _SliceShift * act;
                float2 duv = float2(uv.x + shift, uv.y);

                // ── 본체: 약한 색수차 분할 + 찢긴 실루엣 샘플 ──
                float cs = _RGBSplit * g;
                fixed4 col;
                col.r = tex2D(_MainTex, duv + float2(cs, 0)).r;
                col.g = tex2D(_MainTex, duv).g;
                col.b = tex2D(_MainTex, duv - float2(cs, 0)).b;
                float aMain = tex2D(_MainTex, duv).a;   // 찢긴 실루엣의 원본 알파
                col.a = aMain;

                // ── 스캔라인: 실루엣 내부에만 어둡게(알파 가중) ──
                float scan = 1.0 - _ScanIntensity * g * step(0.5, frac(uv.y * _ScanDensity));
                col.rgb *= scan;

                col += _TextureSampleAdd;
                col *= IN.color;   // 틴트 + 인계 알파(CanvasGroup)

                // ── 고스트 잔상: 실루엣을 좌/우로 약간 민 알파를 외곽 바깥에 색입혀 덧댄다(색수차 윤곽 잔상) ──
                float eo = _EchoOffset * g;
                float aL = tex2D(_MainTex, duv + float2(eo, 0)).a;  // 오른쪽 샘플 → 왼쪽 잔상
                float aR = tex2D(_MainTex, duv - float2(eo, 0)).a;
                float fringeL = saturate(aL - aMain);              // 현재 외곽 바깥으로 삐져나온 부분만
                float fringeR = saturate(aR - aMain);
                float echoRaw = max(fringeL, fringeR);
                float echoA = echoRaw * g * _EchoStrength * IN.color.a;
                fixed3 echoCol = (fringeL >= fringeR) ? _EchoColorA.rgb : _EchoColorB.rgb;
                col.rgb += echoCol * echoA;
                col.a = saturate(col.a + echoA);

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
