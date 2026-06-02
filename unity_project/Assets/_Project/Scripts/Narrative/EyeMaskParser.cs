using System.Globalization;
using LoveAlgo.Events; // EyeMaskAction

namespace LoveAlgo.Story
{
    /// <summary>
    /// 아이마스크 파싱 결과(순수). 범위 밖이면 <see cref="IsValid"/>=false → 엔진이 스킵.
    /// 지속 필드가 음수면 미지정 — 엔진이 동결값으로 해석. 동작별로 사용하는 필드가 다르다.
    /// </summary>
    public readonly struct EyeMaskIntent
    {
        public readonly EyeMaskAction Action;
        public readonly float CloseDuration;
        public readonly float OpenDuration;
        public readonly float HoldDuration;
        public readonly bool IsValid;

        public EyeMaskIntent(EyeMaskAction action, float closeDuration, float openDuration, float holdDuration, bool isValid)
        {
            Action = action;
            CloseDuration = closeDuration;
            OpenDuration = openDuration;
            HoldDuration = holdDuration;
            IsValid = isValid;
        }

        public static EyeMaskIntent Invalid => new EyeMaskIntent(EyeMaskAction.Open, -1f, -1f, -1f, false);
    }

    /// <summary>
    /// 아이마스크 FX Value 순수 파서(M3 슬라이스2). EventBus·UnityEngine 비의존(EditMode 테스트).
    /// 요구사항(REWRITE_FEATURE_INVENTORY §FX 아이마스크)의 기능만 도출 — 검은 바로 눈을 감고/뜨는 연출.
    /// 동결 지속값은 EyeMaskTuningSO로(ADR-012).
    ///
    /// 문법: <c>EyeClose[:dur]</c> · <c>EyeOpen[:dur]</c> · <c>EyeCloseImmediate</c> ·
    ///       <c>EyeBlink[:closeDur:openDur[:hold]]</c>(케이스 무시). 그 외 = <see cref="EyeMaskIntent.Invalid"/>.
    /// </summary>
    public static class EyeMaskParser
    {
        public static EyeMaskIntent Parse(string value)
        {
            if (string.IsNullOrEmpty(value)) return EyeMaskIntent.Invalid;

            var parts = value.Split(':');
            string head = parts[0].Trim().ToLowerInvariant();

            switch (head)
            {
                case "eyeclose":
                    return new EyeMaskIntent(EyeMaskAction.Close, Arg(parts, 1), -1f, -1f, true);
                case "eyeopen":
                    return new EyeMaskIntent(EyeMaskAction.Open, -1f, Arg(parts, 1), -1f, true);
                case "eyecloseimmediate":
                    return new EyeMaskIntent(EyeMaskAction.CloseImmediate, -1f, -1f, -1f, true);
                case "eyeblink":
                    return new EyeMaskIntent(EyeMaskAction.Blink, Arg(parts, 1), Arg(parts, 2), Arg(parts, 3), true);
                default:
                    return EyeMaskIntent.Invalid;
            }
        }

        static float Arg(string[] parts, int i) =>
            parts.Length > i && float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : -1f;
    }
}
