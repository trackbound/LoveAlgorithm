using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace LoveAlgo.Story.StoryEngine
{
    /// <summary>
    /// 대사 본문의 D9/D13 인라인 태그를 점검 — preflight 검증용 (Phase D18).
    /// DialogueEffectsParser / DialogueColorPalette가 런타임에서 관대하게 통과시키는 항목들을,
    /// 이 inspector는 모두 issue로 surface해서 작가에게 의도 어긋남을 알림.
    ///
    /// 검출 항목:
    ///   - UnbalancedOpen   : &lt;shake&gt;/&lt;wave&gt;/&lt;emph&gt;/&lt;color=…&gt; 열고 안 닫음
    ///   - UnbalancedClose  : &lt;/shake&gt; 등 매칭 open 없는 close
    ///   - BadIntensity     : &lt;shake=foo&gt; 처럼 숫자 파싱 실패 또는 음수
    ///   - UnknownColor     : palette 있을 때 &lt;color=name&gt; name 미정의 (hex는 통과)
    ///
    /// 알 수 없는 &lt;tag&gt; (TMP native &lt;b&gt;/&lt;i&gt; 등)는 통과 — 우리 책임 아님.
    /// </summary>
    public static class DialogueTagInspector
    {
        public enum IssueKind { UnbalancedOpen, UnbalancedClose, BadIntensity, UnknownColor }

        public readonly struct Issue
        {
            public readonly IssueKind Kind;
            public readonly string TagName;       // "shake", "wave", "emph", "color"
            public readonly string Detail;        // 사람-친화 메시지

            public Issue(IssueKind kind, string tagName, string detail)
            {
                Kind = kind;
                TagName = tagName ?? "";
                Detail = detail ?? "";
            }

            public override string ToString() => $"[{Kind}] {TagName}: {Detail}";
        }

        /// <summary>
        /// 본문 점검. palette null이면 color name 검증 스킵 (hex는 항상 통과).
        /// 입력 null/빈 문자열이면 empty list 반환.
        /// </summary>
        public static List<Issue> Inspect(string body, IDictionary<string, Color> colorPalette)
        {
            var issues = new List<Issue>();
            if (string.IsNullOrEmpty(body)) return issues;

            // 우리가 인식하는 태그 이름들 (case-insensitive 비교 시 lower 캐스팅).
            // color는 별도 처리 — value가 필요하니까.
            int shakeStack = 0, waveStack = 0, emphStack = 0, colorStack = 0;

            int i = 0;
            while (i < body.Length)
            {
                char c = body[i];
                if (c != '<') { i++; continue; }

                // </name> 패턴
                if (i + 1 < body.Length && body[i + 1] == '/')
                {
                    int nameStart = i + 2;
                    int nameEnd = nameStart;
                    while (nameEnd < body.Length && IsTagNameChar(body[nameEnd])) nameEnd++;
                    if (nameEnd >= body.Length || body[nameEnd] != '>') { i++; continue; }
                    string name = body.Substring(nameStart, nameEnd - nameStart).ToLowerInvariant();
                    switch (name)
                    {
                        case "shake": shakeStack = DecOrReport(shakeStack, "shake", issues); break;
                        case "wave":  waveStack  = DecOrReport(waveStack,  "wave",  issues); break;
                        case "emph":  emphStack  = DecOrReport(emphStack,  "emph",  issues); break;
                        case "color": colorStack = DecOrReport(colorStack, "color", issues); break;
                        // 우리가 모르는 close는 통과 (TMP native, 예: </b>)
                    }
                    i = nameEnd + 1;
                    continue;
                }

                // <name[=value]> 패턴
                {
                    int nameStart = i + 1;
                    int nameEnd = nameStart;
                    while (nameEnd < body.Length && IsTagNameChar(body[nameEnd])) nameEnd++;
                    if (nameEnd == nameStart) { i++; continue; }

                    string name = body.Substring(nameStart, nameEnd - nameStart).ToLowerInvariant();

                    // value 추출 — '=' 다음부터 '>' 까지
                    int p = nameEnd;
                    string value = null;
                    if (p < body.Length && body[p] == '=')
                    {
                        int valStart = p + 1;
                        int valEnd = valStart;
                        while (valEnd < body.Length && body[valEnd] != '>') valEnd++;
                        if (valEnd >= body.Length) { i++; continue; } // 닫힘 없음 — 통과
                        value = body.Substring(valStart, valEnd - valStart);
                        p = valEnd;
                    }
                    if (p >= body.Length || body[p] != '>') { i++; continue; }

                    switch (name)
                    {
                        case "shake": shakeStack++; CheckIntensity(value, "shake", issues); break;
                        case "wave":  waveStack++;  CheckIntensity(value, "wave",  issues); break;
                        case "emph":  emphStack++;  CheckIntensity(value, "emph",  issues); break;
                        case "color": colorStack++; CheckColorValue(value, colorPalette, issues); break;
                        // 기타 <pause>/<wait>/<sfx> 등 directive: stack 추적 안 함 (한 줄 직접 명령)
                        // 그 외 모르는 <tag>: TMP native일 수 있으니 통과
                    }
                    i = p + 1;
                    continue;
                }
            }

            // 닫지 않은 open
            if (shakeStack > 0) issues.Add(new Issue(IssueKind.UnbalancedOpen, "shake", $"{shakeStack}개 안 닫힘 — 끝까지 효과 적용됨"));
            if (waveStack  > 0) issues.Add(new Issue(IssueKind.UnbalancedOpen, "wave",  $"{waveStack}개 안 닫힘 — 끝까지 효과 적용됨"));
            if (emphStack  > 0) issues.Add(new Issue(IssueKind.UnbalancedOpen, "emph",  $"{emphStack}개 안 닫힘 — 끝까지 효과 적용됨"));
            if (colorStack > 0) issues.Add(new Issue(IssueKind.UnbalancedOpen, "color", $"{colorStack}개 안 닫힘 — TMP가 시각적으로 끝까지 색칠"));

            return issues;
        }

        static int DecOrReport(int stack, string tagName, List<Issue> issues)
        {
            if (stack <= 0)
            {
                issues.Add(new Issue(IssueKind.UnbalancedClose, tagName, "매칭되는 open 없음 (오타 또는 잘못된 위치)"));
                return 0;
            }
            return stack - 1;
        }

        static void CheckIntensity(string value, string tagName, List<Issue> issues)
        {
            if (string.IsNullOrEmpty(value)) return; // intensity 생략 OK
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
            {
                issues.Add(new Issue(IssueKind.BadIntensity, tagName, $"intensity '{value}' 숫자 파싱 실패 — 런타임에선 1.0 폴백"));
                return;
            }
            if (f < 0f)
                issues.Add(new Issue(IssueKind.BadIntensity, tagName, $"intensity {f} 음수 — 의도된 값인지 확인"));
            if (float.IsNaN(f) || float.IsInfinity(f))
                issues.Add(new Issue(IssueKind.BadIntensity, tagName, $"intensity NaN/Infinity '{value}'"));
        }

        static void CheckColorValue(string value, IDictionary<string, Color> palette, List<Issue> issues)
        {
            if (string.IsNullOrEmpty(value)) return;
            string stripped = value.Trim().Trim('"', '\'');
            if (stripped.Length == 0) return;
            if (stripped[0] == '#') return; // hex — 무조건 통과
            if (palette == null) return;    // palette 미사용 환경 — 검증 스킵
            if (palette.ContainsKey(stripped)) return;
            issues.Add(new Issue(IssueKind.UnknownColor, "color", $"이름 '{stripped}' palette 미등록 — TMP가 검정으로 처리 가능"));
        }

        static bool IsTagNameChar(char c)
            => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
    }
}
