using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using LoveAlgo.Events; // InlinePause, InlineEmote

namespace LoveAlgo.Story
{
    /// <summary>대사 인라인 태그 파싱 결과(순수). <see cref="Text"/>=태그 제거된 표시 텍스트, <see cref="Pauses"/>=멈춤 지점(없으면 null), <see cref="Emotes"/>=표정 변경 지점(없으면 null), <see cref="Sfx"/>=효과음 지점(없으면 null).</summary>
    public readonly struct ParsedDialogue
    {
        public readonly string Text;
        public readonly List<InlinePause> Pauses;
        public readonly List<InlineEmote> Emotes;
        public readonly List<InlineSfx> Sfx;
        public ParsedDialogue(string text, List<InlinePause> pauses, List<InlineEmote> emotes, List<InlineSfx> sfx)
        {
            Text = text;
            Pauses = pauses;
            Emotes = emotes;
            Sfx = sfx;
        }
    }

    /// <summary>
    /// 대사 본문의 인라인 태그를 순수하게 분해(EventBus·UnityEngine 비의존 = EditMode 테스트). 표시 텍스트
    /// (태그 제거)와 작용 지점(태그 위치의 글자 인덱스)을 분리해, 엔진이 명령에 실어 발행하면 DialogueView가
    /// 타이핑 중 해당 인덱스에서 멈추거나(<c>&lt;wait:sec&gt;</c>) 화자 표정을 바꾼다(<c>&lt;emote=표정/&gt;</c>).
    ///
    /// 통일 문법(모든 인라인 태그 = <c>&lt;이름=값/&gt;</c>, 꼬리 <c>/</c>는 선택):
    /// - <c>&lt;emote=표정/&gt;</c> — 그 줄 화자의 표정 변경. 표정 키만 싣고 화자→슬롯 해석은 StageView 몫.
    /// - <c>&lt;emote=대상:표정/&gt;</c> — 지정 대상(코드 ID/별칭)의 표정 변경. 내레이션 줄에서도 동작.
    /// - <c>&lt;wait=초/&gt;</c> — 초만큼 멈춤(0 이하/파싱 실패면 무시). 레거시 <c>&lt;wait:초&gt;</c>도 허용.
    /// - <c>&lt;sfx=이름/&gt;</c> — 그 지점에서 효과음 1회 재생(이름 해석은 Sound 행과 동일, 엔진이 채움).
    /// '='도 ':'도 없는 단일 토큰 <c>&lt;웃음&gt;</c>은 화자 표정으로 본다(레거시). 미지원 이름/태그 = 제거만(무시).
    /// 닫히지 않은 <c>&lt;</c>는 리터럴로 둔다.
    /// </summary>
    public static class InlineTagParser
    {
        public static ParsedDialogue Parse(string raw)
        {
            if (string.IsNullOrEmpty(raw) || raw.IndexOf('<') < 0)
                return new ParsedDialogue(raw ?? "", null, null, null); // 태그 없음 — 할당 없이 그대로.

            var sb = new StringBuilder(raw.Length);
            List<InlinePause> pauses = null;
            List<InlineEmote> emotes = null;
            List<InlineSfx> sfx = null;
            int i = 0;

            while (i < raw.Length)
            {
                char c = raw[i];
                if (c == '<')
                {
                    int close = raw.IndexOf('>', i + 1);
                    if (close < 0) { sb.Append(raw, i, raw.Length - i); break; } // 미완성 태그 → 리터럴
                    string tag = raw.Substring(i + 1, close - i - 1);
                    HandleTag(tag, sb.Length, ref pauses, ref emotes, ref sfx);
                    i = close + 1;
                }
                else
                {
                    sb.Append(c);
                    i++;
                }
            }

            return new ParsedDialogue(sb.ToString(), pauses, emotes, sfx);
        }

        static void HandleTag(string tag, int charIndex, ref List<InlinePause> pauses, ref List<InlineEmote> emotes, ref List<InlineSfx> sfx)
        {
            // 자기닫힘 표기의 꼬리 '/' 허용: <emote=기본/> == <emote=기본>.
            string t = tag.Trim();
            if (t.EndsWith("/", StringComparison.Ordinal)) t = t.Substring(0, t.Length - 1).Trim();
            if (t.Length == 0) return;

            // 통일 문법 <이름=값>. 첫 '='만 이름/값 분리(값에 ':' 대상 구분이 들어갈 수 있음).
            int eq = t.IndexOf('=');
            if (eq > 0)
            {
                string name = t.Substring(0, eq).Trim().ToLowerInvariant();
                string value = t.Substring(eq + 1).Trim();
                if (value.Length == 0) return;
                switch (name)
                {
                    case "emote":
                        AddEmote(charIndex, value, ref emotes);
                        return;
                    case "wait":
                        if (TryParseSeconds(value, out float secEq))
                            (pauses ??= new List<InlinePause>()).Add(new InlinePause(charIndex, secEq));
                        return;
                    case "sfx":
                        (sfx ??= new List<InlineSfx>()).Add(new InlineSfx(charIndex, value));
                        return;
                    default:
                        return; // 미지원 이름 = 무시(제거).
                }
            }

            // 레거시 콜론형: <wait:초>만 허용(그 외 콜론 태그는 비정규 → 무시).
            int colon = t.IndexOf(':');
            if (colon > 0)
            {
                string name = t.Substring(0, colon).Trim().ToLowerInvariant();
                if (name == "wait" && TryParseSeconds(t.Substring(colon + 1).Trim(), out float secColon))
                    (pauses ??= new List<InlinePause>()).Add(new InlinePause(charIndex, secColon));
                return;
            }

            // '='도 ':'도 없는 단일 토큰 → 화자 표정(레거시 <웃음>).
            AddEmote(charIndex, t, ref emotes);
        }

        // <emote=대상:표정> 지정형이면 대상/표정 분리, 아니면 화자 표정(Target=null).
        static void AddEmote(int charIndex, string value, ref List<InlineEmote> emotes)
        {
            string target = null;
            string emote = value;
            int colon = value.IndexOf(':');
            if (colon > 0 && colon < value.Length - 1)
            {
                target = value.Substring(0, colon).Trim();
                emote = value.Substring(colon + 1).Trim();
            }
            if (emote.Length == 0) return;
            (emotes ??= new List<InlineEmote>()).Add(new InlineEmote(charIndex, emote, string.IsNullOrEmpty(target) ? null : target));
        }

        static bool TryParseSeconds(string s, out float sec) =>
            float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out sec) && sec > 0f;
    }
}
