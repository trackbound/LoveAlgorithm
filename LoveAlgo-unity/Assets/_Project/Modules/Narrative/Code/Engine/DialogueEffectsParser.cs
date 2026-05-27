using System.Collections.Generic;
using System.Text;

namespace LoveAlgo.Story.StoryEngine
{
    /// <summary>대사 본문에 적용할 시각 효과 종류 (Phase D9).</summary>
    public enum DialogueEffectKind { Shake, Wave, Emph }

    /// <summary>
    /// 효과 적용 구간 — CleanText 상의 [Start, End) (End exclusive).
    /// Intensity는 태그 값(예: shake=2)에서 추출, 기본 1.
    /// </summary>
    public readonly struct DialogueEffectRange
    {
        public readonly int Start;
        public readonly int End;
        public readonly DialogueEffectKind Kind;
        public readonly float Intensity;

        public DialogueEffectRange(int start, int end, DialogueEffectKind kind, float intensity)
        {
            Start = start;
            End = end;
            Kind = kind;
            Intensity = intensity;
        }

        public bool IsEmpty => End <= Start;
    }

    /// <summary>파싱 결과 — TMP에 그대로 쓸 CleanText + 효과 구간 리스트.</summary>
    public readonly struct ParsedDialogue
    {
        public readonly string CleanText;
        public readonly IReadOnlyList<DialogueEffectRange> Effects;

        public ParsedDialogue(string cleanText, IReadOnlyList<DialogueEffectRange> effects)
        {
            CleanText = cleanText ?? "";
            Effects = effects;
        }
    }

    /// <summary>
    /// 대사 시각 효과 인라인 태그 파서 (Phase D9).
    /// 지원 태그: &lt;shake[=N]&gt;...&lt;/shake&gt;, &lt;wave[=N]&gt;...&lt;/wave&gt;, &lt;emph[=N]&gt;...&lt;/emph&gt;.
    ///
    /// 정책:
    ///   - 태그 자체는 출력에서 제거. 내부 텍스트는 유지.
    ///   - 효과 구간 = (Start, End) 인덱스는 CleanText 기준 (TMP 소스 문자열 위치).
    ///   - 중첩 허용 — 같은 char에 여러 효과 동시 적용 가능.
    ///   - 닫지 않은 태그 → CleanText 끝까지 효과 적용 (관대 정책).
    ///   - 매칭 안 되는 닫기 태그 → 무시 (관대 정책).
    ///   - 알 수 없는 &lt;tag&gt; → 그대로 통과 (TMP가 처리하게).
    ///
    /// 기존 DialogueUI의 directive 태그(&lt;wait&gt;, &lt;sfx&gt; 등)는 별도 파서가 처리 —
    /// 이 파서는 directive 태그를 모르므로 호출 순서가 중요 (DialogueUI에서 directive 먼저 strip).
    /// </summary>
    public static class DialogueEffectsParser
    {
        /// <summary>파싱. 입력이 null/빈 문자열이어도 안전.</summary>
        public static ParsedDialogue Parse(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new ParsedDialogue("", System.Array.Empty<DialogueEffectRange>());

            var sb = new StringBuilder(text.Length);
            var effects = new List<DialogueEffectRange>();
            // 열린 효과 스택 — 같은 종류의 닫기를 만나면 가장 최근에 열린 것을 닫음.
            // (스트릭트 LIFO가 아닌, kind 매칭 → 진정한 LIFO보다 사용자 친화적)
            var openStack = new List<OpenEffect>();

            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (c != '<')
                {
                    sb.Append(c);
                    i++;
                    continue;
                }

                // '<' 발견. 닫는지(`</...>`) 여는지(`<...>`) 판정.
                if (TryMatchCloseTag(text, i, out var closeKind, out int closeEnd))
                {
                    // 매칭되는 가장 최근 open 찾아 닫기. 못 찾으면 무시.
                    int idx = FindLastOpen(openStack, closeKind);
                    if (idx >= 0)
                    {
                        var op = openStack[idx];
                        openStack.RemoveAt(idx);
                        int endPos = sb.Length;
                        if (endPos > op.Start)
                            effects.Add(new DialogueEffectRange(op.Start, endPos, closeKind, op.Intensity));
                        // empty range (e.g. <shake></shake>) → 추가 안 함
                    }
                    i = closeEnd;
                    continue;
                }

                if (TryMatchOpenTag(text, i, out var openKind, out float intensity, out int openEnd))
                {
                    openStack.Add(new OpenEffect { Kind = openKind, Intensity = intensity, Start = sb.Length });
                    i = openEnd;
                    continue;
                }

                // 알 수 없는 < — 리터럴로 통과 (TMP가 native rich tag일 수도 있고 그냥 텍스트일 수도)
                sb.Append(c);
                i++;
            }

            // 닫지 않은 효과들 — CleanText 끝까지 적용 (관대 정책)
            for (int k = 0; k < openStack.Count; k++)
            {
                var op = openStack[k];
                if (sb.Length > op.Start)
                    effects.Add(new DialogueEffectRange(op.Start, sb.Length, op.Kind, op.Intensity));
            }

            return new ParsedDialogue(sb.ToString(), effects);
        }

        struct OpenEffect
        {
            public DialogueEffectKind Kind;
            public float Intensity;
            public int Start;
        }

        static int FindLastOpen(List<OpenEffect> stack, DialogueEffectKind kind)
        {
            for (int i = stack.Count - 1; i >= 0; i--)
                if (stack[i].Kind == kind) return i;
            return -1;
        }

        /// <summary>"&lt;/shake&gt;" 같은 닫기 태그 매칭. 매칭되면 closeEnd = 닫기 직후 인덱스.</summary>
        static bool TryMatchCloseTag(string text, int start, out DialogueEffectKind kind, out int closeEnd)
        {
            kind = default; closeEnd = start;
            // 최소 길이: "</shake>" = 8, "</wave>" = 7, "</emph>" = 7
            if (start + 1 >= text.Length || text[start + 1] != '/') return false;

            int nameStart = start + 2;
            int nameEnd = nameStart;
            while (nameEnd < text.Length && IsTagNameChar(text[nameEnd])) nameEnd++;
            if (nameEnd == nameStart) return false;
            if (nameEnd >= text.Length || text[nameEnd] != '>') return false;

            if (TryResolveKind(text, nameStart, nameEnd, out kind))
            {
                closeEnd = nameEnd + 1;
                return true;
            }
            return false;
        }

        /// <summary>"&lt;shake[=N]&gt;" 같은 열기 태그 매칭. 매칭되면 openEnd = 열기 직후 인덱스.</summary>
        static bool TryMatchOpenTag(string text, int start, out DialogueEffectKind kind, out float intensity, out int openEnd)
        {
            kind = default; intensity = 1f; openEnd = start;
            int nameStart = start + 1;
            int nameEnd = nameStart;
            while (nameEnd < text.Length && IsTagNameChar(text[nameEnd])) nameEnd++;
            if (nameEnd == nameStart) return false;
            if (!TryResolveKind(text, nameStart, nameEnd, out kind)) return false;

            // 이름 뒤에 '=N' 옵션 or 바로 '>'
            int p = nameEnd;
            if (p < text.Length && text[p] == '=')
            {
                int valStart = p + 1;
                int valEnd = valStart;
                while (valEnd < text.Length && text[valEnd] != '>') valEnd++;
                if (valEnd >= text.Length) return false;
                if (float.TryParse(text.Substring(valStart, valEnd - valStart),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var v))
                {
                    intensity = v;
                }
                p = valEnd;
            }

            if (p >= text.Length || text[p] != '>') return false;
            openEnd = p + 1;
            return true;
        }

        static bool TryResolveKind(string text, int nameStart, int nameEnd, out DialogueEffectKind kind)
        {
            // 짧은 이름이라 직접 비교 (대소문자 무시).
            int len = nameEnd - nameStart;
            if (len == 5 && EqIgnoreCase(text, nameStart, "shake")) { kind = DialogueEffectKind.Shake; return true; }
            if (len == 4 && EqIgnoreCase(text, nameStart, "wave"))  { kind = DialogueEffectKind.Wave;  return true; }
            if (len == 4 && EqIgnoreCase(text, nameStart, "emph"))  { kind = DialogueEffectKind.Emph;  return true; }
            kind = default;
            return false;
        }

        static bool EqIgnoreCase(string text, int start, string target)
        {
            for (int i = 0; i < target.Length; i++)
            {
                int idx = start + i;
                if (idx >= text.Length) return false;
                char a = text[idx], b = target[i];
                if (a >= 'A' && a <= 'Z') a = (char)(a + 32);
                // target은 이미 소문자
                if (a != b) return false;
            }
            return true;
        }

        static bool IsTagNameChar(char c)
            => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
    }
}
