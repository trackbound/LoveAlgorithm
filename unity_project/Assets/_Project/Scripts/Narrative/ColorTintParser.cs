using System.Globalization;

namespace LoveAlgo.Story
{
    /// <summary>색 틴트 프리셋. 엔진이 ColorTintTuningSO로 RGB 해석. Clear는 별도 플래그(<see cref="ColorTintIntent.IsClear"/>).</summary>
    public enum TintPreset { Sepia, Blue, Red, Pink, Green, Sunset }

    /// <summary>
    /// 색 틴트 파싱 결과(순수). 범위 밖이면 <see cref="IsValid"/>=false → 엔진이 스킵.
    /// <see cref="IsClear"/>=true면 해제(프리셋 무의미). <see cref="Alpha"/>/<see cref="Duration"/>&lt;0이면 미지정(엔진이 동결값).
    /// </summary>
    public readonly struct ColorTintIntent
    {
        public readonly TintPreset Preset;
        public readonly bool IsClear;
        public readonly float Alpha;
        public readonly float Duration;
        public readonly bool IsValid;

        public ColorTintIntent(TintPreset preset, bool isClear, float alpha, float duration, bool isValid)
        {
            Preset = preset;
            IsClear = isClear;
            Alpha = alpha;
            Duration = duration;
            IsValid = isValid;
        }

        public static ColorTintIntent Invalid => new ColorTintIntent(TintPreset.Sepia, false, -1f, -1f, false);
    }

    /// <summary>
    /// 색 틴트 Value 순수 파서(M3 슬라이스2). EventBus·UnityEngine 비의존(EditMode 테스트).
    /// 요구사항(REWRITE_FEATURE_INVENTORY §FX 색)에서 도출 — 구 ScreenFX의 ParseTintColor 프리셋 의미만 가져오고
    /// 색 RGB·알파·지속 동결값은 ColorTintTuningSO로(ADR-012).
    ///
    /// 문법(구 동작 의미 1:1): <c>ColorTint:프리셋[:alpha[:dur]]</c> · <c>ColorTint:Clear[::dur]</c>(해제).
    /// 프리셋: Sepia/Blue/Red/Pink/Green/Sunset(케이스 무시). 알 수 없는 프리셋 = 해제(구 동작: Color.clear).
    /// </summary>
    public static class ColorTintParser
    {
        public static ColorTintIntent Parse(string value)
        {
            if (string.IsNullOrEmpty(value)) return ColorTintIntent.Invalid;

            var parts = value.Split(':');
            if (parts[0].Trim().ToLowerInvariant() != "colortint") return ColorTintIntent.Invalid;

            string presetToken = parts.Length >= 2 ? parts[1].Trim() : "";
            float alpha = parts.Length >= 3 && TryFloat(parts[2], out float a) ? a : -1f;
            float dur = parts.Length >= 4 && TryFloat(parts[3], out float d) ? d : -1f;

            // Clear 또는 알 수 없는 프리셋 → 해제(구 ParseTintColor: 미지정=Color.clear).
            if (!TryParsePreset(presetToken, out var preset))
                return new ColorTintIntent(TintPreset.Sepia, true, alpha, dur, true);

            return new ColorTintIntent(preset, false, alpha, dur, true);
        }

        static bool TryParsePreset(string token, out TintPreset preset)
        {
            switch (token.Trim().ToLowerInvariant())
            {
                case "sepia":  preset = TintPreset.Sepia;  return true;
                case "blue":   preset = TintPreset.Blue;   return true;
                case "red":    preset = TintPreset.Red;    return true;
                case "pink":   preset = TintPreset.Pink;   return true;
                case "green":  preset = TintPreset.Green;  return true;
                case "sunset": preset = TintPreset.Sunset; return true;
                default:       preset = TintPreset.Sepia;  return false; // Clear 포함
            }
        }

        static bool TryFloat(string s, out float value) =>
            float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
