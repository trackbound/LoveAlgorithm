using System.Globalization;
using LoveAlgo.Events; // CameraKind

namespace LoveAlgo.Story
{
    /// <summary>
    /// 카메라 FX 파싱 결과(순수). 범위 밖이면 <see cref="IsValid"/>=false → 엔진이 스킵.
    /// <see cref="Duration"/>&lt;0이면 미지정 — 엔진이 종류별 동결 기본값으로 해석.
    /// <see cref="ZoomScale"/>은 Zoom에만, <see cref="PanX"/>/<see cref="PanY"/>는 Pan에만 의미.
    /// </summary>
    public readonly struct CameraIntent
    {
        public readonly CameraKind Kind;
        public readonly float ZoomScale;
        public readonly float PanX;
        public readonly float PanY;
        public readonly float Duration;
        public readonly bool IsValid;

        public CameraIntent(CameraKind kind, float zoomScale, float panX, float panY, float duration, bool isValid)
        {
            Kind = kind;
            ZoomScale = zoomScale;
            PanX = panX;
            PanY = panY;
            Duration = duration;
            IsValid = isValid;
        }

        public static CameraIntent Invalid => new CameraIntent(CameraKind.Reset, 1f, 0f, 0f, -1f, false);
    }

    /// <summary>
    /// 카메라 FX Value 순수 파서(M3 슬라이스2). EventBus·UnityEngine 비의존(EditMode 테스트).
    /// 요구사항(REWRITE_FEATURE_INVENTORY §FX 화면)에서 도출 — 구 ScreenFX의 stageTransform DOScale/DOAnchorPos
    /// 의미만 가져오고 구조는 답습하지 않는다. 시간 동결값은 CameraTuningSO로(ADR-012).
    ///
    /// 문법(구 동작 의미 1:1):
    ///   <c>CamZoom[:배율[:지속]]</c> — 배율 1.0=기본 크기, 1.5=확대. 배율 생략 시 1.0(줌 해제).
    ///   <c>CamPan:x:y[:지속]</c> — x,y = 절대 오프셋(px), 0:0=원점 복귀.
    ///   <c>CamReset[:지속]</c> — 줌+팬 동시 원점 복귀.
    /// 그 외(스크린/흔들기/Eye/Tint/매크로) = <see cref="CameraIntent.Invalid"/>. CamShake는 ShakeParser 소관.
    /// </summary>
    public static class CameraParser
    {
        public static CameraIntent Parse(string value)
        {
            if (string.IsNullOrEmpty(value)) return CameraIntent.Invalid;

            var parts = value.Split(':');
            string head = parts[0].Trim().ToLowerInvariant();

            switch (head)
            {
                case "camzoom":
                {
                    float scale = parts.Length >= 2 && TryFloat(parts[1], out float s) ? s : 1f;
                    float dur = parts.Length >= 3 && TryFloat(parts[2], out float d) ? d : -1f;
                    return new CameraIntent(CameraKind.Zoom, scale, 0f, 0f, dur, true);
                }
                case "campan":
                {
                    float x = parts.Length >= 2 && TryFloat(parts[1], out float px) ? px : 0f;
                    float y = parts.Length >= 3 && TryFloat(parts[2], out float py) ? py : 0f;
                    float dur = parts.Length >= 4 && TryFloat(parts[3], out float d) ? d : -1f;
                    return new CameraIntent(CameraKind.Pan, 1f, x, y, dur, true);
                }
                case "camreset":
                {
                    float dur = parts.Length >= 2 && TryFloat(parts[1], out float d) ? d : -1f;
                    return new CameraIntent(CameraKind.Reset, 1f, 0f, 0f, dur, true);
                }
                default:
                    return CameraIntent.Invalid;
            }
        }

        static bool TryFloat(string s, out float value) =>
            float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
