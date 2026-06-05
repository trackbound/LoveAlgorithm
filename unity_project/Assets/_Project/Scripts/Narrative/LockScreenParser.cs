using System;

namespace LoveAlgo.Story
{
    using LoveAlgo.Events; // LockMode

    /// <summary>
    /// 잠금화면 Flow(LockScreen) Value 순수 파서. EventBus·UnityEngine 비의존(EditMode 테스트).
    /// 문법(STORY_COMMANDS): <c>LockScreen:&lt;mode&gt;[:Time=HH:mm][:FadeOut|NoFadeOut]</c> (옵션 순서 자유·케이스 무시).
    /// mode 미지/누락이면 IsValid=false. FadeOut 기본 false(NoFadeOut). 엔진(NarrativeController)이 결과로
    /// ShowLockScreenCommand를 발행한다.
    /// </summary>
    public static class LockScreenParser
    {
        public static LockScreenIntent Parse(string value)
        {
            var r = new LockScreenIntent();
            if (string.IsNullOrEmpty(value)) return r;

            var parts = value.Split(':');
            if (!string.Equals(parts[0].Trim(), "LockScreen", StringComparison.OrdinalIgnoreCase)) return r;
            if (parts.Length < 2 || !TryParseMode(parts[1], out LockMode mode)) return r;

            r.Mode = mode;
            for (int i = 2; i < parts.Length; i++)
            {
                string t = parts[i].Trim();
                if (t.Length == 0) continue;
                if (string.Equals(t, "FadeOut", StringComparison.OrdinalIgnoreCase)) r.FadeOut = true;
                else if (string.Equals(t, "NoFadeOut", StringComparison.OrdinalIgnoreCase)) r.FadeOut = false;
                else if (t.StartsWith("Time=", StringComparison.OrdinalIgnoreCase))
                {
                    string tv = t.Substring(5).Trim();
                    // HH:mm은 ':' split으로 "Time=07"+"30"처럼 쪼개졌을 수 있어 다음 토큰(mm)을 합친다.
                    if (i + 1 < parts.Length && IsMinutes(parts[i + 1]))
                    {
                        tv += ":" + parts[i + 1].Trim();
                        i++;
                    }
                    r.TimeOverride = tv;
                }
            }

            r.IsValid = true;
            return r;
        }

        static bool TryParseMode(string s, out LockMode mode)
        {
            switch (s.Trim().ToLowerInvariant())
            {
                case "firstsetup": mode = LockMode.FirstSetup; return true;
                case "normal":     mode = LockMode.Normal; return true;
                case "reset":      mode = LockMode.Reset; return true;
                case "auto":       mode = LockMode.Auto; return true;
                case "gamestart":  mode = LockMode.GameStart; return true;
                default:           mode = LockMode.FirstSetup; return false;
            }
        }

        static bool IsMinutes(string s)
        {
            s = s.Trim();
            return s.Length == 2 && char.IsDigit(s[0]) && char.IsDigit(s[1]);
        }
    }

    /// <summary>LockScreen 분해 결과. Mode·FadeOut·TimeOverride(없으면 null).</summary>
    public struct LockScreenIntent
    {
        public bool IsValid;
        public LockMode Mode;
        public bool FadeOut;
        public string TimeOverride;
    }
}
