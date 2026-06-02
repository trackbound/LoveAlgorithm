using System;
using System.Globalization;
using LoveAlgo.Events; // BgIntent, CharIntent, BgTransition, CharSlot, CharAction

namespace LoveAlgo.Story
{
    /// <summary>
    /// 스테이지 명령(BG/Char) Value 문자열 순수 파서(M3 슬라이스2). EventBus·UnityEngine 비의존(EditMode 테스트).
    /// 슬라이스1 <see cref="ChoiceParser"/>·<see cref="ScriptCursor"/>와 같은 "순수층" 위치 — 엔진(NarrativeController)이
    /// 결과 인텐트를 받아 동결 수치(StageTuningSO)로 duration을 해석한 뒤 명령을 발행한다.
    ///
    /// 문법(STORY_COMMANDS.md):
    ///   BG   : <c>배경키[:Cut|Fade|Cross[:duration]]</c>   (전환 생략 시 Cross, duration 생략 시 -1=기본)
    ///   Char : <c>[slot:]Enter:캐릭터[:표정]</c> / <c>[slot:]Emote:표정</c> / <c>[slot:]Exit</c> / <c>[slot:]Clear</c>
    ///          (slot 생략 시 C; Char은 duration을 싣지 않음 — 항상 기본값)
    /// 별칭/카탈로그(한글명→ID, Default→코드)는 이번 슬라이스 밖 — 키는 그대로 통과(데모 CSV는 실파일 키 사용).
    /// </summary>
    public static class StageInterpreter
    {
        /// <summary>BG Value 파싱. 이름이 비면 <see cref="BgIntent.IsValid"/>=false.</summary>
        public static BgIntent ParseBackground(string value)
        {
            if (string.IsNullOrEmpty(value))
                return new BgIntent(null, BgTransition.Cut, -1f);

            var parts = value.Split(':');
            string name = parts[0].Trim();
            BgTransition transition = parts.Length >= 2 ? ParseTransition(parts[1]) : BgTransition.Cross;
            float duration = -1f;
            if (parts.Length >= 3 && TryParseFloat(parts[2], out float d))
                duration = d;

            return new BgIntent(name, transition, duration);
        }

        /// <summary>Char Value 파싱. 형식 오류면 <see cref="CharIntent.IsValid"/>=false(Enter인데 캐릭터 없음).</summary>
        public static CharIntent ParseCharacter(string value)
        {
            if (string.IsNullOrEmpty(value))
                return new CharIntent(CharSlot.C, CharAction.Enter, null, "", -1f);

            var parts = value.Split(':');
            int i = 0;

            // 1번째 토큰이 슬롯(L/C/R)이면 사용, 아니면 C 기본.
            CharSlot slot = CharSlot.C;
            if (TryParseSlot(parts[0], out CharSlot parsedSlot))
            {
                slot = parsedSlot;
                i = 1;
            }

            // 액션 토큰 없거나 미지원 → 무효(Enter+캐릭터null).
            if (i >= parts.Length || !TryParseAction(parts[i], out CharAction action))
                return new CharIntent(slot, CharAction.Enter, null, "", -1f);
            i++;

            string character = null;
            string emote = "";
            switch (action)
            {
                case CharAction.Enter:
                    if (i < parts.Length) character = parts[i++].Trim();
                    if (i < parts.Length) emote = parts[i].Trim();
                    break;
                case CharAction.Emote:
                    if (i < parts.Length) emote = parts[i].Trim();
                    break;
                // Exit/Clear: 추가 인자 없음.
            }

            return new CharIntent(slot, action, character, emote, -1f);
        }

        static BgTransition ParseTransition(string s)
        {
            switch (s.Trim().ToLowerInvariant())
            {
                case "cut": return BgTransition.Cut;
                case "fade": return BgTransition.Fade;
                case "cross":
                case "crossfade": return BgTransition.Cross;
                default: return BgTransition.Cross; // 미지정/오타 → Cross(구 기본)
            }
        }

        static bool TryParseSlot(string s, out CharSlot slot)
        {
            slot = CharSlot.C;
            switch (s.Trim().ToLowerInvariant())
            {
                case "l": case "left": slot = CharSlot.L; return true;
                case "c": case "center": slot = CharSlot.C; return true;
                case "r": case "right": slot = CharSlot.R; return true;
                default: return false;
            }
        }

        static bool TryParseAction(string s, out CharAction action)
        {
            action = CharAction.Enter;
            switch (s.Trim().ToLowerInvariant())
            {
                case "enter": action = CharAction.Enter; return true;
                case "exit": action = CharAction.Exit; return true;
                case "emote": action = CharAction.Emote; return true;
                case "clear": action = CharAction.Clear; return true;
                default: return false;
            }
        }

        static bool TryParseFloat(string s, out float value) =>
            float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
