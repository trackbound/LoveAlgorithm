using System;
using System.Collections.Generic;

namespace LoveAlgo.Contracts
{
    /// <summary>
    /// Option(선택지) 파싱 데이터.
    /// C4-Phase B-7b 에서 LoveAlgo.Story → LoveAlgo.Contracts 로 이동 (IChoicePopup 표면 의존성).
    /// </summary>
    public class OptionData
    {
        public string ButtonText;
        public string JumpTarget;
        public List<string> Effects = new();
        public string Condition;

        /// <summary>
        /// D10: 중요 선택지 마커. 기획자가 ButtonText 앞에 '*' 또는 안에 '[important]'를
        /// 적으면 true. 파싱 시 마커는 ButtonText에서 strip됨.
        /// </summary>
        public bool IsImportant;

        /// <summary>
        /// Option Value 파싱
        /// 형식: 버튼텍스트|점프대상|효과1|효과2|...|if:조건
        /// 중요 마커: 버튼텍스트 앞 '*' 또는 텍스트 내 '[important]'
        /// </summary>
        public static OptionData Parse(string value)
        {
            var data = new OptionData();
            var parts = value.Split('|');

            if (parts.Length >= 1)
                data.ButtonText = ExtractImportantMarker(parts[0], out data.IsImportant);

            if (parts.Length >= 2)
                data.JumpTarget = parts[1];

            // 3번째부터: 효과 또는 조건
            for (int i = 2; i < parts.Length; i++)
            {
                string part = parts[i].Trim();

                if (part.StartsWith("if:", StringComparison.OrdinalIgnoreCase))
                {
                    // 조건
                    data.Condition = part.Substring(3);
                }
                else
                {
                    // 효과
                    data.Effects.Add(part);
                }
            }

            return data;
        }

        /// <summary>
        /// D10: 버튼텍스트에서 중요 마커('*' 접두사 또는 '[important]' 토큰)를 추출.
        /// 마커가 발견되면 isImportant=true, 마커 strip된 텍스트 반환. 양쪽 공백 trim.
        /// </summary>
        // C4-Phase C-1b: asmdef 분리 후 cross-assembly 접근 위해 internal → public.
        public static string ExtractImportantMarker(string raw, out bool isImportant)
        {
            isImportant = false;
            if (string.IsNullOrEmpty(raw)) return raw ?? "";

            string s = raw;

            // [important] 토큰 (대소문자 무시) — 텍스트 어디든
            const string TokenLower = "[important]";
            int idx = s.IndexOf(TokenLower, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                isImportant = true;
                s = s.Remove(idx, TokenLower.Length);
            }

            // '*' 접두사 (공백 허용)
            string trimmed = s.TrimStart();
            if (trimmed.StartsWith("*"))
            {
                isImportant = true;
                trimmed = trimmed.Substring(1);
            }

            return trimmed.Trim();
        }
    }
}
