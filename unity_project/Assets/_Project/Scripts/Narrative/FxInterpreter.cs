using System.Globalization;
using LoveAlgo.Events; // ScreenFxKind

namespace LoveAlgo.Story
{
    /// <summary>
    /// 스크린 FX 파싱 결과(순수). 이번 슬라이스 범위(FadeOut/FadeIn/Flash)면 <see cref="IsValid"/>=true.
    /// 범위 밖 FX(카메라/Eye/Tint/흔들기/캐릭터/매크로)는 <see cref="IsValid"/>=false → 엔진이 스킵.
    /// <see cref="Duration"/>&lt;0이면 CSV 미지정(엔진이 동결 수치로 해석).
    /// </summary>
    public readonly struct ScreenFxIntent
    {
        public readonly ScreenFxKind Kind;
        public readonly float Duration;
        public readonly bool IsValid;

        public ScreenFxIntent(ScreenFxKind kind, float duration, bool isValid)
        {
            Kind = kind;
            Duration = duration;
            IsValid = isValid;
        }

        public static ScreenFxIntent Invalid => new ScreenFxIntent(ScreenFxKind.FadeOut, -1f, false);
    }

    /// <summary>
    /// FX 명령 Value 순수 파서(M3 슬라이스2: 스크린 오버레이만). EventBus·UnityEngine 비의존(EditMode 테스트).
    /// 요구사항(REWRITE_FEATURE_INVENTORY §FX)에서 도출 — 구 ScreenFX(싱글톤+DOTween, 카메라/캐릭터까지
    /// 한 클래스)의 구조는 가져오지 않고, 이번 슬라이스가 다루는 화면 페이드/플래시만 인식한다.
    ///
    /// 문법: <c>FadeOut[:dur]</c> · <c>FadeIn[:dur]</c> · <c>Flash[:dur]</c>(케이스 무시). dur 생략 시 -1(기본 위임).
    /// 그 외 FX 키워드는 <see cref="ScreenFxIntent.Invalid"/> 반환 — 엔진이 "슬라이스 밖" 스킵.
    /// </summary>
    public static class FxInterpreter
    {
        public static ScreenFxIntent ParseScreen(string value)
        {
            if (string.IsNullOrEmpty(value)) return ScreenFxIntent.Invalid;

            var parts = value.Split(':');
            string head = parts[0].Trim().ToLowerInvariant();

            ScreenFxKind kind;
            switch (head)
            {
                case "fadeout": kind = ScreenFxKind.FadeOut; break;
                case "fadein": kind = ScreenFxKind.FadeIn; break;
                case "flash": kind = ScreenFxKind.Flash; break;
                default: return ScreenFxIntent.Invalid; // 카메라/Eye/Tint/흔들기/캐릭터/매크로 = 슬라이스 밖
            }

            float duration = -1f;
            if (parts.Length >= 2 && TryParseFloat(parts[1], out float d))
                duration = d;

            return new ScreenFxIntent(kind, duration, true);
        }

        static bool TryParseFloat(string s, out float value) =>
            float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
