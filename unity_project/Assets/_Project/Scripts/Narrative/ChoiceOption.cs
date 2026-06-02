using System;
using System.Collections.Generic;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 선택지 1개(Option 라인)의 파싱 결과. 구 <c>OptionData</c> 1:1 이식 — 필드 동일, 파서만 순수 분리.
    /// <see cref="Condition"/>은 이번 슬라이스에서 보관만(필터링 미적용 — HANDOFF 슬라이스 범위).
    /// </summary>
    public sealed class ChoiceOption
    {
        public string ButtonText;
        public string JumpTarget;
        public readonly List<string> Effects = new();
        public string Condition;
    }

    /// <summary>
    /// Option 라인 Value 순수 파서. 형식: <c>버튼텍스트|점프대상|효과1|효과2|...|if:조건</c>.
    /// EventBus·UnityEngine 비의존(EditMode 테스트). 구 <c>ChoicePopup.OptionData.Parse</c>와 동일 규칙.
    /// </summary>
    public static class ChoiceParser
    {
        public static ChoiceOption ParseOption(string value)
        {
            var data = new ChoiceOption();
            if (string.IsNullOrEmpty(value)) return data;

            var parts = value.Split('|');
            if (parts.Length >= 1) data.ButtonText = parts[0];
            if (parts.Length >= 2) data.JumpTarget = parts[1];

            // 3번째 토큰부터: "if:"로 시작하면 조건, 아니면 효과.
            for (int i = 2; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                if (part.StartsWith("if:", StringComparison.OrdinalIgnoreCase))
                    data.Condition = part.Substring(3);
                else if (part.Length > 0)
                    data.Effects.Add(part);
            }
            return data;
        }

        /// <summary>여러 Option 라인 Value를 일괄 파싱.</summary>
        public static List<ChoiceOption> ParseOptions(IEnumerable<string> values)
        {
            var list = new List<ChoiceOption>();
            if (values == null) return list;
            foreach (var v in values) list.Add(ParseOption(v));
            return list;
        }
    }
}
