using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using LoveAlgo.Events; // InlinePause, InlineEmote

namespace LoveAlgo.Story
{
    /// <summary>대사 인라인 태그 파싱 결과(순수). <see cref="Text"/>=태그 제거된 표시 텍스트, <see cref="Pauses"/>=멈춤 지점(없으면 null), <see cref="Emotes"/>=표정 변경 지점(없으면 null).</summary>
    public readonly struct ParsedDialogue
    {
        public readonly string Text;
        public readonly List<InlinePause> Pauses;
        public readonly List<InlineEmote> Emotes;
        public ParsedDialogue(string text, List<InlinePause> pauses, List<InlineEmote> emotes)
        {
            Text = text;
            Pauses = pauses;
            Emotes = emotes;
        }
    }

    /// <summary>
    /// 대사 본문의 인라인 태그를 순수하게 분해(EventBus·UnityEngine 비의존 = EditMode 테스트). 표시 텍스트
    /// (태그 제거)와 작용 지점(태그 위치의 글자 인덱스)을 분리해, 엔진이 명령에 실어 발행하면 DialogueView가
    /// 타이핑 중 해당 인덱스에서 멈추거나(<c>&lt;wait:sec&gt;</c>) 화자 표정을 바꾼다(<c>&lt;emote=표정/&gt;</c>).
    ///
    /// 지원 태그:
    /// - <c>&lt;wait:sec&gt;</c> — sec초 멈춤(미지정/0 이하면 무시).
    /// - <c>&lt;emote=표정/&gt;</c> — 화자 캐릭터 표정 변경(자기닫힘 표기의 꼬리 <c>/</c> 허용, 콜론형 <c>&lt;emote:x&gt;</c>는
    ///   비정규라 무시). 표정 키만 싣고 화자→슬롯 해석은 StageView 몫.
    /// 그 외 태그 = 제거만(무시). 닫히지 않은 <c>&lt;</c>는 리터럴로 둔다.
    /// </summary>
    public static class InlineTagParser
    {
        public static ParsedDialogue Parse(string raw)
        {
            if (string.IsNullOrEmpty(raw) || raw.IndexOf('<') < 0)
                return new ParsedDialogue(raw ?? "", null, null); // 태그 없음 — 할당 없이 그대로.

            var sb = new StringBuilder(raw.Length);
            List<InlinePause> pauses = null;
            List<InlineEmote> emotes = null;
            int i = 0;

            while (i < raw.Length)
            {
                char c = raw[i];
                if (c == '<')
                {
                    int close = raw.IndexOf('>', i + 1);
                    if (close < 0) { sb.Append(raw, i, raw.Length - i); break; } // 미완성 태그 → 리터럴
                    string tag = raw.Substring(i + 1, close - i - 1);
                    HandleTag(tag, sb.Length, ref pauses, ref emotes);
                    i = close + 1;
                }
                else
                {
                    sb.Append(c);
                    i++;
                }
            }

            return new ParsedDialogue(sb.ToString(), pauses, emotes);
        }

        static void HandleTag(string tag, int charIndex, ref List<InlinePause> pauses, ref List<InlineEmote> emotes)
        {
            string t = tag.Trim();

            // <emote=표정/> — 화자 표정 변경 지점. '=' 구분 + 자기닫힘 꼬리 '/' 허용(콜론형은 비정규 → 미지원).
            if (t.StartsWith("emote=", StringComparison.OrdinalIgnoreCase))
            {
                string v = t.Substring("emote=".Length).TrimEnd('/').Trim();
                if (!string.IsNullOrEmpty(v))
                    (emotes ??= new List<InlineEmote>()).Add(new InlineEmote(charIndex, v));
                return;
            }

            var parts = t.Split(':');
            string name = parts[0].Trim().ToLowerInvariant();

            if (name == "wait"
                && parts.Length >= 2
                && float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float sec)
                && sec > 0f)
            {
                (pauses ??= new List<InlinePause>()).Add(new InlinePause(charIndex, sec));
            }
            // 그 외 태그(<emote:...> 비정규 포함) = 미지원 → 제거(무시).
        }
    }
}
