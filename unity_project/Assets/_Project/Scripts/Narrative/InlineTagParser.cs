using System.Collections.Generic;
using System.Globalization;
using System.Text;
using LoveAlgo.Events; // InlinePause

namespace LoveAlgo.Story
{
    /// <summary>대사 인라인 태그 파싱 결과(순수). <see cref="Text"/>=태그 제거된 표시 텍스트, <see cref="Pauses"/>=멈춤 지점(없으면 null).</summary>
    public readonly struct ParsedDialogue
    {
        public readonly string Text;
        public readonly List<InlinePause> Pauses;
        public ParsedDialogue(string text, List<InlinePause> pauses) { Text = text; Pauses = pauses; }
    }

    /// <summary>
    /// 대사 본문의 인라인 태그를 순수하게 분해(M3 슬라이스2: <c>&lt;wait:sec&gt;</c>). EventBus·UnityEngine 비의존
    /// (EditMode 테스트). 표시 텍스트(태그 제거)와 멈춤 지점(태그 위치의 글자 인덱스)을 분리해, 엔진이 명령에
    /// 실어 발행하면 DialogueView가 타이핑 중 해당 인덱스에서 멈춘다(UI는 Narrative 파서를 직접 못 보므로 컨트롤러 경유).
    ///
    /// 이번 슬라이스: <c>&lt;wait:sec&gt;</c>만 작용(sec 미지정/0 이하면 무시). 기타 태그(<c>&lt;emote&gt;</c> 등)는
    /// 제거만(후속 슬라이스). 닫히지 않은 <c>&lt;</c>는 리터럴로 둔다.
    /// </summary>
    public static class InlineTagParser
    {
        public static ParsedDialogue Parse(string raw)
        {
            if (string.IsNullOrEmpty(raw) || raw.IndexOf('<') < 0)
                return new ParsedDialogue(raw ?? "", null); // 태그 없음 — 할당 없이 그대로.

            var sb = new StringBuilder(raw.Length);
            List<InlinePause> pauses = null;
            int i = 0;

            while (i < raw.Length)
            {
                char c = raw[i];
                if (c == '<')
                {
                    int close = raw.IndexOf('>', i + 1);
                    if (close < 0) { sb.Append(raw, i, raw.Length - i); break; } // 미완성 태그 → 리터럴
                    string tag = raw.Substring(i + 1, close - i - 1);
                    HandleTag(tag, sb.Length, ref pauses);
                    i = close + 1;
                }
                else
                {
                    sb.Append(c);
                    i++;
                }
            }

            return new ParsedDialogue(sb.ToString(), pauses);
        }

        static void HandleTag(string tag, int charIndex, ref List<InlinePause> pauses)
        {
            var parts = tag.Split(':');
            string name = parts[0].Trim().ToLowerInvariant();

            if (name == "wait"
                && parts.Length >= 2
                && float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float sec)
                && sec > 0f)
            {
                (pauses ??= new List<InlinePause>()).Add(new InlinePause(charIndex, sec));
            }
            // 그 외 태그(<emote> 등) = 이번 슬라이스 미지원 → 제거(무시).
        }
    }
}
