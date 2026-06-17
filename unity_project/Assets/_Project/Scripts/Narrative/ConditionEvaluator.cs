using System;
using System.Globalization;
using LoveAlgo.Core; // GameStateSO

namespace LoveAlgo.Story
{
    /// <summary>
    /// 순수 조건 평가기 — CSV 조건 문자열을 <see cref="GameStateSO"/>에 대해 bool로 평가한다(EventBus/MonoBehaviour
    /// 무관 → EditMode 테스트). Flow <c>If</c>·선택지 조건 등 분기 게이트가 공유한다. 빈/널 조건은 true(무조건 통과).
    ///
    /// 원자 조건(구 <c>GameState.EvaluateCondition</c> 의미 1:1):
    ///   <c>Flag:이름</c> / <c>!Flag:이름</c> · <c>Chose:태그</c> / <c>!Chose:태그</c>(선택 이력) · <c>Love:히로인{op}N</c> · <c>Stat:스탯{op}N</c> · <c>스탯{op}N</c>(베어)
    ///   op ∈ { &gt;=  &lt;=  ==  &gt;  &lt; }
    /// 복합: AND = <c>&amp;</c>, OR = <c>|</c>. **AND가 OR보다 우선**(OR항들 중 하나라도 참 + 각 OR항의 AND들이 모두 참).
    ///   예: <c>Flag:a&amp;Stat:Int&gt;=20</c>(둘 다) · <c>Flag:a|Flag:b</c>(하나) · <c>Flag:a&amp;Int&gt;=20|Flag:vip</c>((a∧Int)∨vip)
    /// 미지원 원자(비교식/접두사 아님, 숫자 파싱 실패) = false.
    /// </summary>
    public static class ConditionEvaluator
    {
        static readonly string[] Operators = { ">=", "<=", "==", ">", "<" }; // 복합 연산자 먼저(>= 가 > 보다 우선).

        public static bool Evaluate(GameStateSO gs, string condition)
        {
            if (string.IsNullOrWhiteSpace(condition)) return true; // 빈 조건 = 무조건 통과(상태 무관).
            if (gs == null) return false;

            // OR(|) 중 하나라도 참이면 전체 참. 각 OR항은 AND(&)가 모두 참이어야 참(AND가 OR보다 우선).
            foreach (var orTerm in condition.Split('|'))
                if (EvaluateAnd(gs, orTerm)) return true;
            return false;
        }

        static bool EvaluateAnd(GameStateSO gs, string term)
        {
            foreach (var atom in term.Split('&'))
                if (!EvaluateAtom(gs, atom.Trim())) return false;
            return true;
        }

        static bool EvaluateAtom(GameStateSO gs, string atom)
        {
            if (string.IsNullOrWhiteSpace(atom)) return true; // 빈 원자(후행 &/| 등) = 무조건.
            if (atom.StartsWith("!Flag:", StringComparison.Ordinal)) return !gs.GetFlag(atom.Substring(6));
            if (atom.StartsWith("Flag:",  StringComparison.Ordinal)) return  gs.GetFlag(atom.Substring(5));
            if (atom.StartsWith("!Chose:", StringComparison.Ordinal)) return !gs.HasChosen(atom.Substring(7));
            if (atom.StartsWith("Chose:",  StringComparison.Ordinal)) return  gs.HasChosen(atom.Substring(6));
            if (atom.StartsWith("Love:",  StringComparison.Ordinal)) return Compare(atom.Substring(5), gs.GetLove);
            if (atom.StartsWith("Stat:",  StringComparison.Ordinal)) return Compare(atom.Substring(5), gs.GetStat);
            return Compare(atom, gs.GetStat); // 베어 스탯 비교(예: Int>=20)
        }

        static bool Compare(string expr, Func<string, int> getValue)
        {
            foreach (var op in Operators)
            {
                int idx = expr.IndexOf(op, StringComparison.Ordinal);
                if (idx <= 0) continue;

                string name = expr.Substring(0, idx).Trim();
                string valueStr = expr.Substring(idx + op.Length).Trim();
                if (!int.TryParse(valueStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int target))
                    return false;

                int cur = getValue(name);
                return op switch
                {
                    ">=" => cur >= target,
                    "<=" => cur <= target,
                    "==" => cur == target,
                    ">"  => cur > target,
                    "<"  => cur < target,
                    _    => false
                };
            }
            return false; // 비교식 아님 → 미지원 조건
        }
    }
}
