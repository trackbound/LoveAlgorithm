using System;
using System.Collections.Generic;
using LoveAlgo.Core; // GameStateSO (조건 필터링)

namespace LoveAlgo.Story
{
    /// <summary>
    /// 선택지 1개(Option 라인)의 파싱 결과. 구 <c>OptionData</c> 1:1 이식 — 필드 동일, 파서만 순수 분리.
    /// <see cref="Condition"/>은 <see cref="ChoiceParser.VisibleOptions"/>가 <c>ConditionEvaluator</c>로 평가해 표시 필터링(Flow If와 동일 문법).
    /// </summary>
    public sealed class ChoiceOption
    {
        public string ButtonText;
        public string JumpTarget;
        public readonly List<string> Effects = new();
        public string Condition;
        public string Mark; // 선택 시 choiceHistory에 기록할 태그(조건 Chose:태그가 조회). null=미기록.
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
                else if (part.StartsWith("mark:", StringComparison.OrdinalIgnoreCase))
                    data.Mark = part.Substring(5);
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

        /// <summary>
        /// 조건(<see cref="ChoiceOption.Condition"/>)을 <paramref name="gs"/>에 평가해 표시 가능한 선택지만 반환(순수).
        /// 조건이 없으면 항상 표시. 분기 게이트는 <c>ConditionEvaluator</c> 공유(Flow If와 동일 문법) — EditMode 테스트.
        /// </summary>
        public static List<ChoiceOption> VisibleOptions(IReadOnlyList<ChoiceOption> options, GameStateSO gs)
        {
            var visible = new List<ChoiceOption>();
            if (options == null) return visible;
            for (int i = 0; i < options.Count; i++)
                if (ConditionEvaluator.Evaluate(gs, options[i].Condition))
                    visible.Add(options[i]);
            return visible;
        }
    }
}
