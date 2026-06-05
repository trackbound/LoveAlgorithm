using System;
using System.Globalization;

namespace LoveAlgo.Story
{
    /// <summary>
    /// FX 매크로 <c>Setup</c>의 순수 파서(M3 슬라이스2 후속). EventBus·UnityEngine 비의존(EditMode 테스트).
    /// 다른 연출 파서(<see cref="StageParser"/> 등)와 같은 "순수층" — 엔진(NarrativeController)이 결과를 받아
    /// <b>기존 명령을 즉시(Cut)로 재발행</b>한다(신규 뷰/이벤트 0). 매크로라 자체 동결 수치 없음.
    ///
    /// 문법(STORY_COMMANDS.md): <c>Setup:BG=…|BGM=…|Char=…[:slot]|Overlay=…|Eye=Close|Open</c> — 즉시 전환.
    /// 각 키는 선택, 순서 무관, 케이스 무시. Char 값은 <c>이름[:슬롯]</c>(슬롯 생략 시 C). 빈 값은 무시.
    /// head가 <c>Setup</c>이 아니면 <see cref="SetupIntent.IsValid"/>=false(형제 파서로 위임).
    /// </summary>
    public static class SetupMacroParser
    {
        public static SetupIntent Parse(string value)
        {
            var r = new SetupIntent();
            if (string.IsNullOrEmpty(value)) return r;

            int ci = value.IndexOf(':');
            string head = (ci >= 0 ? value.Substring(0, ci) : value).Trim();
            if (!string.Equals(head, "Setup", StringComparison.OrdinalIgnoreCase)) return r;

            string body = ci >= 0 ? value.Substring(ci + 1) : "";
            foreach (var seg in body.Split('|'))
            {
                int eq = seg.IndexOf('=');
                if (eq < 0) continue;
                string key = seg.Substring(0, eq).Trim();
                string val = seg.Substring(eq + 1).Trim();
                if (val.Length == 0) continue;

                if (Eq(key, "BG")) r.Bg = val;
                else if (Eq(key, "BGM")) r.Bgm = val;
                else if (Eq(key, "Overlay")) r.Overlay = val;
                else if (Eq(key, "Eye")) r.Eye = val;
                else if (Eq(key, "Char"))
                {
                    // 이름[:슬롯] — 마지막 ':' 뒤를 슬롯으로(이름엔 ':' 없음). 슬롯 생략 시 null(엔진이 C 기본).
                    int sc = val.LastIndexOf(':');
                    if (sc > 0)
                    {
                        r.CharName = val.Substring(0, sc).Trim();
                        r.CharSlot = val.Substring(sc + 1).Trim();
                    }
                    else r.CharName = val;
                }
            }

            r.IsValid = r.Bg != null || r.Bgm != null || r.CharName != null || r.Overlay != null || r.Eye != null;
            return r;
        }

        static bool Eq(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Setup 매크로 분해 결과. 미지정 필드는 null(엔진이 발행 생략).</summary>
    public struct SetupIntent
    {
        public bool IsValid;
        public string Bg;        // 배경 키 (Cut 즉시)
        public string Bgm;       // BGM 키
        public string CharName;  // 캐릭터 이름/ID
        public string CharSlot;  // "L"/"C"/"R" (null = 기본 C)
        public string Overlay;   // 오버레이 키
        public string Eye;       // "Close"/"Open"
    }

    /// <summary>
    /// FX 매크로 <c>Wait[:seconds]</c>의 순수 파서. head가 Wait면 true + 초(생략 시 <see cref="DefaultSeconds"/>).
    /// 단순 일시정지라 인텐트 구조체 없이 (성공여부, 초)만 반환. EventBus·UnityEngine 비의존.
    /// </summary>
    public static class WaitMacroParser
    {
        /// <summary>STORY_COMMANDS.md: Wait 인자 생략 시 기본 1.0초.</summary>
        public const float DefaultSeconds = 1.0f;

        public static bool TryParse(string value, out float seconds)
        {
            seconds = DefaultSeconds;
            if (string.IsNullOrEmpty(value)) return false;

            int ci = value.IndexOf(':');
            string head = (ci >= 0 ? value.Substring(0, ci) : value).Trim();
            if (!string.Equals(head, "Wait", StringComparison.OrdinalIgnoreCase)) return false;

            if (ci >= 0)
            {
                string arg = value.Substring(ci + 1).Trim();
                if (float.TryParse(arg, NumberStyles.Float, CultureInfo.InvariantCulture, out float s) && s >= 0f)
                    seconds = s;
            }
            return true;
        }
    }
}
