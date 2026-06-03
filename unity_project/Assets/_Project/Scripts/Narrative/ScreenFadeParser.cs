using System.Globalization;
using LoveAlgo.Events; // ScreenFadeKind

namespace LoveAlgo.Story
{
    /// <summary>
    /// 스크린 페이드 파싱 결과(순수). 이번 family 범위(FadeOut/FadeIn/Flash)면 <see cref="IsValid"/>=true.
    /// 범위 밖 FX(카메라/Eye/Tint/흔들기/캐릭터/매크로)는 <see cref="IsValid"/>=false → 엔진이 다른 파서로 위임/스킵.
    /// <see cref="Duration"/>&lt;0이면 CSV 미지정(엔진이 동결 수치로 해석).
    /// </summary>
    public readonly struct ScreenFadeIntent
    {
        public readonly ScreenFadeKind Kind;
        public readonly float Duration;
        public readonly bool IsValid;

        public ScreenFadeIntent(ScreenFadeKind kind, float duration, bool isValid)
        {
            Kind = kind;
            Duration = duration;
            IsValid = isValid;
        }

        public static ScreenFadeIntent Invalid => new ScreenFadeIntent(ScreenFadeKind.FadeOut, -1f, false);
    }

    /// <summary>
    /// 스크린 페이드 Value 순수 파서(M3 슬라이스2: 전체화면 색 페이드). EventBus·UnityEngine 비의존(EditMode 테스트).
    /// 요구사항(REWRITE_FEATURE_INVENTORY §FX 화면)에서 도출 — 구 ScreenFX(싱글톤+DOTween, 카메라/캐릭터까지 한
    /// 클래스) 구조를 답습하지 않고, 이 family가 다루는 화면 페이드/플래시만 인식한다(나머지는 형제 파서 소관).
    ///
    /// 문법: <c>FadeOut[:dur]</c> · <c>FadeIn[:dur]</c> · <c>Flash[:dur]</c>(케이스 무시). dur 생략 시 -1(기본 위임).
    /// 그 외 FX 키워드는 <see cref="ScreenFadeIntent.Invalid"/> 반환 — 엔진이 "이 family 밖" 처리.
    /// </summary>
    public static class ScreenFadeParser
    {
        public static ScreenFadeIntent Parse(string value)
        {
            if (string.IsNullOrEmpty(value)) return ScreenFadeIntent.Invalid;

            var parts = value.Split(':');
            string head = parts[0].Trim().ToLowerInvariant();

            ScreenFadeKind kind;
            switch (head)
            {
                case "fadeout": kind = ScreenFadeKind.FadeOut; break;
                case "fadein": kind = ScreenFadeKind.FadeIn; break;
                case "flash": kind = ScreenFadeKind.Flash; break;
                default: return ScreenFadeIntent.Invalid; // 흔들기=ShakeParser, 카메라/Eye/Tint = 각 파서 소관
            }

            float duration = -1f;
            if (parts.Length >= 2 && TryParseFloat(parts[1], out float d))
                duration = d;

            return new ScreenFadeIntent(kind, duration, true);
        }

        static bool TryParseFloat(string s, out float value) =>
            float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
