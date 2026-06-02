using System;
using System.Globalization;

namespace LoveAlgo.Story
{
    /// <summary>오디오 카테고리(Sound 라인 1번째 토큰).</summary>
    public enum SoundCategory { Bgm, Sfx, Voice }

    /// <summary>
    /// Sound 명령 파싱 결과(순수). <see cref="IsStop"/>=true면 정지(BGM/Voice). <see cref="Fade"/>&lt;0이면
    /// AudioManager 기본 페이드. SFX는 항상 재생(Stop/Fade 없음).
    /// </summary>
    public readonly struct SoundIntent
    {
        public readonly SoundCategory Category;
        public readonly string Name;
        public readonly bool IsStop;
        public readonly float Fade;
        public readonly bool IsValid;

        public SoundIntent(SoundCategory category, string name, bool isStop, float fade, bool isValid)
        {
            Category = category;
            Name = name;
            IsStop = isStop;
            Fade = fade;
            IsValid = isValid;
        }

        public static SoundIntent Invalid => new SoundIntent(SoundCategory.Sfx, null, false, -1f, false);
    }

    /// <summary>
    /// Sound 명령 Value 순수 파서(M3 슬라이스2). EventBus·UnityEngine 비의존(EditMode 테스트). 구 AudioManager.
    /// ExecuteAsync의 디스패치를 결정 로직만 추려 이식 — 실제 재생은 기존 오디오 명령(PlayBgm/StopBgm/PlaySfx/
    /// PlayVoice/StopVoice) 발행으로 AudioManager가 수행(ADR-007, 뷰/매니저 신설 없음).
    ///
    /// 문법: <c>BGM:{name}[:Fade:{d}]</c> · <c>BGM:Stop[:Fade:{d}]</c> · <c>SFX:{name}</c> ·
    ///       <c>Voice:{name}</c> · <c>Voice:Stop</c>. 카테고리/Stop/Fade 토큰은 케이스 무시.
    /// 별칭(한글 BGM명→파일)은 이번 슬라이스 밖 — 키 그대로 통과(데모 CSV는 실파일 키).
    /// </summary>
    public static class SoundInterpreter
    {
        public static SoundIntent Parse(string value)
        {
            if (string.IsNullOrEmpty(value)) return SoundIntent.Invalid;

            var parts = value.Split(':');
            if (parts.Length < 2) return SoundIntent.Invalid;

            if (!TryParseCategory(parts[0], out SoundCategory category))
                return SoundIntent.Invalid;

            string name = parts[1].Trim();
            bool isStop = name.Equals("Stop", StringComparison.OrdinalIgnoreCase);

            // SFX는 정지/페이드가 없다 — Stop이면 무효.
            if (category == SoundCategory.Sfx && isStop)
                return SoundIntent.Invalid;

            float fade = -1f;
            // [:Fade:{d}] — parts[2]="Fade", parts[3]=초.
            if (parts.Length >= 4 && parts[2].Trim().Equals("Fade", StringComparison.OrdinalIgnoreCase)
                && TryParseFloat(parts[3], out float f))
                fade = f;

            return new SoundIntent(category, isStop ? null : name, isStop, fade, true);
        }

        static bool TryParseCategory(string s, out SoundCategory category)
        {
            category = SoundCategory.Sfx;
            switch (s.Trim().ToLowerInvariant())
            {
                case "bgm": category = SoundCategory.Bgm; return true;
                case "sfx": category = SoundCategory.Sfx; return true;
                case "voice": category = SoundCategory.Voice; return true;
                default: return false;
            }
        }

        static bool TryParseFloat(string s, out float value) =>
            float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
