using System.Globalization;
using LoveAlgo.Events; // ShakeTarget, CharSlot

namespace LoveAlgo.Story
{
    /// <summary>강도 프리셋(약/중/강). 엔진이 ShakeTuningSO로 px 해석. CSV가 숫자를 직접 주면 <see cref="ShakeIntent.StrengthPx"/>로.</summary>
    public enum ShakeStrength { Weak, Medium, Strong }

    /// <summary>
    /// 흔들기 FX 파싱 결과(순수). 범위 밖이면 <see cref="IsValid"/>=false → 엔진이 스킵.
    /// <see cref="StrengthPx"/>&lt;0이면 "CSV 미지정/프리셋" — 엔진이 SO로 해석(Stage/Dialogue=<see cref="Preset"/>, Char=캐릭터 기본강도).
    /// <see cref="Duration"/>&lt;0이면 미지정 — 엔진이 기본 지속으로 해석.
    /// </summary>
    public readonly struct ShakeIntent
    {
        public readonly ShakeTarget Target;
        public readonly CharSlot Slot;
        public readonly float StrengthPx;
        public readonly ShakeStrength Preset;
        public readonly float Duration;
        public readonly bool IsValid;

        public ShakeIntent(ShakeTarget target, CharSlot slot, float strengthPx, ShakeStrength preset, float duration, bool isValid)
        {
            Target = target;
            Slot = slot;
            StrengthPx = strengthPx;
            Preset = preset;
            Duration = duration;
            IsValid = isValid;
        }

        public static ShakeIntent Invalid => new ShakeIntent(ShakeTarget.Stage, CharSlot.C, -1f, ShakeStrength.Medium, -1f, false);
    }

    /// <summary>
    /// 흔들기 FX Value 순수 파서(M3 슬라이스2). EventBus·UnityEngine 비의존(EditMode 테스트).
    /// 요구사항(REWRITE_FEATURE_INVENTORY §FX 흔들기)에서 도출 — 구 ScreenFX/CharacterLayer의 분산 처리를
    /// 한 파서로 모으되 구조는 답습하지 않는다. 매직넘버(프리셋 px·기본 지속)는 전부 ShakeTuningSO로(ADR-012).
    ///
    /// 문법(구 동작 의미 1:1):
    ///   Stage/Dialogue: <c>StageShake</c> · <c>StageShake:Strong</c>(프리셋) · <c>StageShake:0.5</c>(지속) ·
    ///                   <c>StageShake:0.5:Strong</c>(지속+프리셋) · <c>StageShake:0.5:40</c>(지속+숫자강도).
    ///                   → pos1은 프리셋이면 강도, 숫자면 지속. pos2가 강도(프리셋/숫자).
    ///   Char: <c>CharShake</c> · <c>CharShake:L</c>(슬롯) · <c>CharShake:L:30</c>(슬롯+강도) ·
    ///         <c>CharShake:L:30:0.4</c>(슬롯+강도+지속). 강도는 숫자만(프리셋 없음). 슬롯 생략 시 C.
    ///   CamShake: UI 무대엔 월드 카메라가 없으므로 StageShake(콘텐츠 래퍼)와 동일 처리.
    /// 그 외(스크린/Eye/Tint/매크로) = <see cref="ShakeIntent.Invalid"/>.
    /// </summary>
    public static class ShakeParser
    {
        public static ShakeIntent Parse(string value)
        {
            if (string.IsNullOrEmpty(value)) return ShakeIntent.Invalid;

            var parts = value.Split(':');
            string head = parts[0].Trim().ToLowerInvariant();

            switch (head)
            {
                case "stageshake":    return ParseScreenShake(ShakeTarget.Stage, parts);
                case "camshake":      return ParseScreenShake(ShakeTarget.Stage, parts); // UI 무대엔 카메라 없음 → 스테이지 흔들기
                case "dialogueshake": return ParseScreenShake(ShakeTarget.Dialogue, parts);
                case "charshake":     return ParseCharShake(parts);
                default:              return ShakeIntent.Invalid; // 흔들기 외 FX = 슬라이스 밖
            }
        }

        // Stage/Dialogue: pos1 = 프리셋(강도) 또는 숫자(지속), pos2 = 강도(프리셋/숫자).
        static ShakeIntent ParseScreenShake(ShakeTarget target, string[] parts)
        {
            var preset = ShakeStrength.Medium;
            float strengthPx = -1f; // -1 = 프리셋 사용
            float duration = -1f;

            if (parts.Length >= 2)
            {
                string p1 = parts[1].Trim();
                if (TryParsePreset(p1, out var pre1))
                {
                    preset = pre1; // 강도 프리셋, 지속은 기본
                }
                else if (TryFloat(p1, out float d))
                {
                    duration = d; // pos1은 지속
                    if (parts.Length >= 3)
                    {
                        string p2 = parts[2].Trim();
                        if (TryParsePreset(p2, out var pre2)) preset = pre2;
                        else if (TryFloat(p2, out float s)) strengthPx = s;
                    }
                }
            }

            return new ShakeIntent(target, CharSlot.C, strengthPx, preset, duration, true);
        }

        // Char: pos1 = 슬롯(생략 가능), 이어서 숫자강도, 지속.
        static ShakeIntent ParseCharShake(string[] parts)
        {
            var slot = CharSlot.C;
            float strengthPx = -1f; // -1 = 캐릭터 기본강도(SO)
            float duration = -1f;

            int i = 1;
            if (parts.Length > i && TryParseSlot(parts[i], out var s)) { slot = s; i++; }
            if (parts.Length > i && TryFloat(parts[i], out float str)) { strengthPx = str; i++; }
            if (parts.Length > i && TryFloat(parts[i], out float dur)) { duration = dur; }

            return new ShakeIntent(ShakeTarget.Char, slot, strengthPx, ShakeStrength.Medium, duration, true);
        }

        static bool TryParsePreset(string token, out ShakeStrength preset)
        {
            switch (token.Trim().ToLowerInvariant())
            {
                case "weak":   preset = ShakeStrength.Weak;   return true;
                case "medium": preset = ShakeStrength.Medium; return true;
                case "strong": preset = ShakeStrength.Strong; return true;
                default:       preset = ShakeStrength.Medium; return false;
            }
        }

        static bool TryParseSlot(string token, out CharSlot slot)
        {
            switch (token.Trim().ToLowerInvariant())
            {
                case "l": case "left":   slot = CharSlot.L; return true;
                case "c": case "center": slot = CharSlot.C; return true;
                case "r": case "right":  slot = CharSlot.R; return true;
                default:                 slot = CharSlot.C; return false;
            }
        }

        static bool TryFloat(string s, out float value) =>
            float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
