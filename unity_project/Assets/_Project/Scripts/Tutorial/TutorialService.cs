using System.Text.RegularExpressions;

namespace LoveAlgo.Tutorial
{
    /// <summary>
    /// 튜토리얼 진행 규칙(순수 static — EditMode 테스트). 기획서 조건:
    /// 클릭할 때마다 다음 / 특정 스텝은 지정 버튼만 클릭 가능("그냥 넘어가기 안됨") / 오토 진행 불가
    /// (자동 진행은 autoAdvanceSeconds 명시 스텝만 — 마지막 인사). 표시·타이밍은 뷰 몫.
    /// </summary>
    public static class TutorialService
    {
        static readonly Regex PlayerToken = new(@"\{\{player\}\}", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>이 스텝이 화면 전체 클릭으로 진행되는가(클릭 제한 없음).</summary>
        public static bool AdvancesOnAnyClick(TutorialSequenceSO.Step step)
            => step != null && string.IsNullOrEmpty(step.requiredClickAnchor);

        /// <summary>클릭 제한 스텝에서 이 앵커 클릭이 진행 트리거인가(케이스 무시).</summary>
        public static bool IsRequiredClick(TutorialSequenceSO.Step step, string anchorId)
            => step != null
               && !string.IsNullOrEmpty(step.requiredClickAnchor)
               && !string.IsNullOrEmpty(anchorId)
               && string.Equals(step.requiredClickAnchor.Trim(), anchorId.Trim(), System.StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 클릭 제한 스텝에서 이 앵커로의 클릭 통과를 허용하는가 — 지정 앵커만 실제 버튼으로 패스스루
        /// (기획: "아이템 구매 버튼만 누를 수 있게 제한"), 그 외는 전부 차단.
        /// </summary>
        public static bool AllowsClickThrough(TutorialSequenceSO.Step step, string anchorId)
            => IsRequiredClick(step, anchorId);

        /// <summary>{{Player}} 토큰 치환(케이스 무시, 스토리 엔진 선례). 이름이 비면 폴백 '플레이어'.</summary>
        public static string ResolveText(string text, string playerName)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string name = string.IsNullOrEmpty(playerName) ? "플레이어" : playerName;
            return PlayerToken.Replace(text, name);
        }
    }
}
