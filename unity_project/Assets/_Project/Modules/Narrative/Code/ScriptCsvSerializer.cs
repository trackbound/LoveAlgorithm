using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace LoveAlgo.Story
{
    /// <summary>
    /// ScriptLine 리스트 → CSV 텍스트 직렬화.
    /// <see cref="ScriptParser"/> / <see cref="CsvUtility"/>의 파싱 규칙을 역으로 적용.
    ///
    /// 라운드트립 보장 (ScriptLine 단위):
    ///   Parse(Serialize(lines)) ≡ lines  — LineID/Type/Speaker/Value/NextType/Delay 5항
    ///
    /// 손실 허용:
    ///   - 원본의 주석(#)·빈 줄
    ///   - 원본의 quote 여부 (필요 시에만 quote)
    ///   - 원본의 멀티라인 Value 표기 — 직렬화 시 항상 `\\n` 이스케이프로 단일 라인화
    ///   - SourceLine (재계산됨)
    /// </summary>
    public static class ScriptCsvSerializer
    {
        const string Header = "LineID,Type,Speaker,Value,Next";
        const string Newline = "\n"; // LF 통일 — git/Unity 모두 무난

        /// <summary>전체 라인을 CSV 문자열로 직렬화.</summary>
        public static string Serialize(IReadOnlyList<ScriptLine> lines, bool includeHeader = true)
        {
            var sb = new StringBuilder(EstimateCapacity(lines));
            if (includeHeader)
            {
                sb.Append(Header);
                sb.Append(Newline);
            }
            if (lines == null) return sb.ToString();
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line == null) continue;
                AppendLine(sb, line);
                sb.Append(Newline);
            }
            return sb.ToString();
        }

        /// <summary>한 라인을 CSV 한 줄로 직렬화 (개행 미포함).</summary>
        public static string SerializeLine(ScriptLine line)
        {
            if (line == null) return "";
            var sb = new StringBuilder(128);
            AppendLine(sb, line);
            return sb.ToString();
        }

        static void AppendLine(StringBuilder sb, ScriptLine line)
        {
            AppendField(sb, line.LineID ?? "");                  sb.Append(',');
            AppendField(sb, line.Type.ToString());               sb.Append(',');
            AppendField(sb, line.Speaker ?? "");                 sb.Append(',');
            AppendField(sb, EncodeValue(line.Value ?? ""));      sb.Append(',');
            AppendField(sb, EncodeNext(line.NextType, line.DelaySeconds));
        }

        /// <summary>
        /// 실제 newline → 리터럴 `\n` (parser의 `Replace("\\n", "\n")` 역).
        /// 단일 라인 CSV 표현으로 통일 — 멀티라인 quoted 필드를 피해 git diff 깔끔하게.
        /// </summary>
        static string EncodeValue(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            // \r\n / \r / \n 모두 \\n으로 통일
            // 순서 중요: \r\n 먼저 처리
            return raw.Replace("\r\n", "\\n").Replace("\r", "\\n").Replace("\n", "\\n");
        }

        /// <summary>NextType + Delay → CSV Next 컬럼.</summary>
        static string EncodeNext(NextType type, float delay)
        {
            switch (type)
            {
                case NextType.Immediate: return ">";
                case NextType.Click:     return "click";
                case NextType.Await:     return "await";
                case NextType.Delay:     return delay.ToString("0.##", CultureInfo.InvariantCulture);
                default:                 return ">";
            }
        }

        /// <summary>
        /// 한 필드를 CSV로 인코딩 (필요 시 quote + 내부 quote 이스케이프).
        /// 인용 필요 조건: 쉼표, 따옴표, CR, LF 포함 시. (EncodeValue로 CR/LF는 이미 제거되지만 안전망)
        /// </summary>
        static void AppendField(StringBuilder sb, string field)
        {
            if (field == null) field = "";
            bool needQuote = NeedsQuote(field);
            if (!needQuote)
            {
                sb.Append(field);
                return;
            }
            sb.Append('"');
            for (int i = 0; i < field.Length; i++)
            {
                char c = field[i];
                if (c == '"') sb.Append('"'); // double-up for escape
                sb.Append(c);
            }
            sb.Append('"');
        }

        static bool NeedsQuote(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == ',' || c == '"' || c == '\r' || c == '\n') return true;
            }
            return false;
        }

        static int EstimateCapacity(IReadOnlyList<ScriptLine> lines)
        {
            if (lines == null) return 64;
            // 평균 80자/라인 추정 + 헤더
            return 64 + lines.Count * 80;
        }
    }
}
