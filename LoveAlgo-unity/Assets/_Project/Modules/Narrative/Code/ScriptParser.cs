using System;
using LoveAlgo.Contracts;
using System.Collections.Generic;
using UnityEngine;

namespace LoveAlgo.Story
{
    /// <summary>
    /// CSV 스토리 스크립트 파서
    /// </summary>
    public static class ScriptParser
    {
        // ── :Fade 생략 시 UX 기본 duration (LineType별) ──
        static readonly Dictionary<LineType, float> DefaultFadeDuration = new()
        {
            { LineType.BG,    0.5f },
            { LineType.Char,  0.3f },
            { LineType.CG,    0.5f },
            { LineType.Sound, 1.0f },
        };

        /// <summary>
        /// LineType별 기본 페이드 duration 조회
        /// :Fade 파라미터가 생략된 경우 이 값으로 자동 보충
        /// </summary>
        public static float GetDefaultFadeDuration(LineType type)
        {
            return DefaultFadeDuration.TryGetValue(type, out var d) ? d : 0f;
        }

        /// <summary>
        /// 전역 엄격 파싱 토글. true면 모든 LogWarning이 LogError로 격상되고,
        /// 빌드/테스트 파이프라인이 콘솔 에러를 게이트로 사용해 잘못된 CSV가
        /// 무음으로 통과하는 것을 막을 수 있다. 기본값 false — 기존 동작과 동일.
        /// </summary>
        public static bool Strict { get; set; }

        /// <summary>Reload Domain Off 가드 — PlayMode 진입 시 Strict 토글 기본값(false)로 복원.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticStateOnLoad() => Strict = false;

        /// <summary>
        /// TextAsset에서 스크립트 파싱
        /// </summary>
        public static List<ScriptLine> Parse(TextAsset asset)
        {
            if (asset == null)
            {
                Debug.LogError("[ScriptParser] TextAsset이 null입니다.");
                return new List<ScriptLine>();
            }
            return Parse(asset.text);
        }

        /// <summary>
        /// CSV 문자열에서 스크립트 파싱
        /// </summary>
        public static List<ScriptLine> Parse(string csv)
        {
            var lines = new List<ScriptLine>();
            if (string.IsNullOrEmpty(csv)) return lines;

            var rows = CsvUtility.SplitRecords(csv);

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i].Text.Trim();
                int lineNumber = rows[i].StartLine;

                // 빈 줄 스킵
                if (string.IsNullOrEmpty(row)) continue;

                // 주석 스킵
                if (row.StartsWith("#")) continue;

                // 헤더 스킵 (첫 줄이 LineID,Type... 인 경우)
                if (row.StartsWith("LineID,", StringComparison.OrdinalIgnoreCase)) continue;

                // CSV 파싱
                var line = ParseLine(row, lineNumber);
                if (line != null)
                {
                    lines.Add(line);
                }
            }

            Debug.Log($"[ScriptParser] {lines.Count}개 라인 파싱 완료");
            return lines;
        }

        /// <summary>
        /// Strict 모드면 LogError, 아니면 LogWarning. 메시지는 동일하게 노출.
        /// </summary>
        static void LogParseIssue(string message)
        {
            if (Strict) Debug.LogError(message);
            else Debug.LogWarning(message);
        }

        /// <summary>
        /// CSV 한 줄 파싱
        /// </summary>
        static ScriptLine ParseLine(string row, int lineNumber)
        {
            var columns = CsvUtility.SplitCsv(row);

            // 5컬럼 필수: LineID, Type, Speaker, Value, Next
            const int minColumns = 5;

            if (columns.Length < minColumns)
            {
                LogParseIssue($"[ScriptParser] Line {lineNumber}: 컬럼 부족 ({columns.Length}/{minColumns}) - \"{TruncateForLog(row)}\"");
                return null;
            }

            // 컬럼 추출
            string lineId = columns[0].Trim();
            string typeStr = columns[1].Trim();
            string speaker = columns[2].Trim();
            string value = columns[3].Trim();
            string nextStr = columns[4].Trim();

            // 리터럴 \n을 실제 줄바꿈으로 치환 (타이핑 효과에서 \가 잠깐 보이는 버그 방지)
            value = value.Replace("\\n", "\n");

            // Type 파싱
            if (!TryParseType(typeStr, out LineType type))
            {
                LogParseIssue($"[ScriptParser] Line {lineNumber}: 알 수 없는 Type '{typeStr}'");
                return null;
            }

            // Next 파싱 (엄격 모드: 빈 Next는 오류)
            ParseNext(nextStr, out NextType nextType, out float delay);

            // 빈 Next → Option/Choice만 허용, 나머지는 오류 (Strict 무관 — 이미 LogError)
            if (string.IsNullOrEmpty(nextStr))
            {
                if (type != LineType.Option && type != LineType.Choice)
                {
                    Debug.LogError($"[ScriptParser] Line {lineNumber}: Next 컬럼이 비어있습니다 (Type={type}). "
                        + "Next를 명시하세요: >(즉시), click(클릭대기), await(완료대기), 숫자(딜레이). "
                        + $"Immediate로 대체합니다. — \"{TruncateForLog(row)}\"");
                }
            }

            // BG 전환 타입 생략 검증
            if (type == LineType.BG && !string.IsNullOrEmpty(value))
            {
                var bgParts = value.Split(':');
                if (bgParts.Length < 2)
                {
                    LogParseIssue($"[ScriptParser] Line {lineNumber}: BG 전환 타입(Cut/Fade/Cross) 생략됨. "
                        + $"명시적으로 지정하세요 — 예: {value}:Cross");
                }
            }

            return new ScriptLine(lineId, type, speaker, value, nextType, delay, lineNumber);
        }

        /// <summary>
        /// Type 문자열 파싱
        /// </summary>
        static bool TryParseType(string typeStr, out LineType type)
        {
            type = LineType.Text;

            if (string.IsNullOrEmpty(typeStr))
                return false;

            return Enum.TryParse(typeStr, true, out type);
        }

        /// <summary>
        /// Next 컬럼 파싱
        /// </summary>
        static void ParseNext(string nextStr, out NextType nextType, out float delay)
        {
            delay = 0f;

            if (string.IsNullOrEmpty(nextStr) || nextStr == ">")
            {
                nextType = NextType.Immediate;
                return;
            }

            if (nextStr.Equals("click", StringComparison.OrdinalIgnoreCase))
            {
                nextType = NextType.Click;
                return;
            }

            if (nextStr.Equals("await", StringComparison.OrdinalIgnoreCase))
            {
                nextType = NextType.Await;
                return;
            }

            // 숫자인 경우 Delay
            if (float.TryParse(nextStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out delay))
            {
                nextType = NextType.Delay;
                return;
            }

            // 기본값
            nextType = NextType.Immediate;
        }

        /// <summary>
        /// LineID로 인덱스 찾기
        /// </summary>
        public static int FindLineIndex(List<ScriptLine> lines, string lineId)
        {
            if (string.IsNullOrEmpty(lineId)) return -1;

            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].LineID == lineId)
                    return i;
            }

            Debug.LogWarning($"[ScriptParser] LineID '{lineId}'를 찾을 수 없습니다.");
            return -1;
        }

        /// <summary>
        /// LineID → 인덱스 딕셔너리 생성 (빠른 점프용)
        /// </summary>
        public static Dictionary<string, int> BuildLineIndex(List<ScriptLine> lines)
        {
            var index = new Dictionary<string, int>();

            for (int i = 0; i < lines.Count; i++)
            {
                if (!string.IsNullOrEmpty(lines[i].LineID))
                {
                    if (index.ContainsKey(lines[i].LineID))
                    {
                        Debug.LogError($"[ScriptParser] 중복 LineID: '{lines[i].LineID}' (index {index[lines[i].LineID]} vs {i}). 첫 번째만 사용됩니다.");
                    }
                    else
                    {
                        index[lines[i].LineID] = i;
                    }
                }
            }

            return index;
        }

        /// <summary>
        /// 로그 출력용 문자열 잘라내기
        /// </summary>
        static string TruncateForLog(string s, int max = 80)
        {
            if (s == null) return "";
            return s.Length <= max ? s : s.Substring(0, max) + "…";
        }
    }
}
