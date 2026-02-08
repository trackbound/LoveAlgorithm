using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoveAlgo.Story
{
    /// <summary>
    /// CSV 스토리 스크립트 파서
    /// </summary>
    public static class ScriptParser
    {
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

            var rows = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i].Trim();

                // 빈 줄 스킵
                if (string.IsNullOrEmpty(row)) continue;

                // 주석 스킵
                if (row.StartsWith("#")) continue;

                // 헤더 스킵 (첫 줄이 LineID,Type... 인 경우)
                if (row.StartsWith("LineID,")) continue;

                // CSV 파싱
                var line = ParseLine(row, i + 1);
                if (line != null)
                {
                    lines.Add(line);
                }
            }

            Debug.Log($"[ScriptParser] {lines.Count}개 라인 파싱 완료");
            return lines;
        }

        /// <summary>
        /// CSV 한 줄 파싱
        /// </summary>
        static ScriptLine ParseLine(string row, int lineNumber)
        {
            var columns = SplitCsv(row);

            // Option 타입은 Next 컬럼이 필요 없으므로 4개 허용
            int minColumns = 4;
            if (columns.Length >= 2 && columns[1].Trim().Equals("Option", StringComparison.OrdinalIgnoreCase))
            {
                minColumns = 4;
            }
            else
            {
                minColumns = 5;
            }

            if (columns.Length < minColumns)
            {
                Debug.LogWarning($"[ScriptParser] Line {lineNumber}: 컬럼 부족 ({columns.Length}/{minColumns}) - {row}");
                return null;
            }

            // 컬럼 추출
            string lineId = columns[0].Trim();
            string typeStr = columns[1].Trim();
            string speaker = columns[2].Trim();
            string value = columns[3].Trim();
            string nextStr = columns.Length >= 5 ? columns[4].Trim() : "";

            // Type 파싱
            if (!TryParseType(typeStr, out LineType type))
            {
                Debug.LogWarning($"[ScriptParser] Line {lineNumber}: 알 수 없는 Type '{typeStr}'");
                return null;
            }

            // Next 파싱
            ParseNext(nextStr, out NextType nextType, out float delay);

            return new ScriptLine(lineId, type, speaker, value, nextType, delay);
        }

        /// <summary>
        /// CSV 문자열을 컬럼으로 분리 (따옴표 처리 포함)
        /// </summary>
        static string[] SplitCsv(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var current = new System.Text.StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            result.Add(current.ToString());
            return result.ToArray();
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
            if (float.TryParse(nextStr, out delay))
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
                        Debug.LogWarning($"[ScriptParser] 중복 LineID: {lines[i].LineID}");
                    }
                    else
                    {
                        index[lines[i].LineID] = i;
                    }
                }
            }

            return index;
        }
    }
}
