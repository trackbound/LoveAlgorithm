using LoveAlgo.Core; // ScreenPhase

namespace LoveAlgo.Game
{
    /// <summary>
    /// <see cref="PhaseService.Resolve"/> 결과 — 전환 허용 여부와 거부 사유. 적용/통지는 어댑터(PhaseController) 몫이라
    /// 서비스 자체는 EventBus/상태를 모른다(FlowCommandResult 패턴 미러).
    /// </summary>
    public readonly struct PhaseTransitionResult
    {
        public readonly bool Ok;
        public readonly ScreenPhase From;
        public readonly ScreenPhase To;
        public readonly string Reason; // 거부 사유(Ok면 null)

        public PhaseTransitionResult(bool ok, ScreenPhase from, ScreenPhase to, string reason)
        {
            Ok = ok; From = from; To = to; Reason = reason;
        }
    }

    /// <summary>
    /// 화면 페이즈 전환 규칙(순수 FSM, ADR-013). 유효 전환만 허용하고 결과를 구조체로 반환 — EventBus/MonoBehaviour를
    /// 모른다(EditMode 테스트 가능, FlowCommandInterpreter 패턴 미러). 전환 적용(state.phase 쓰기)·통지는 PhaseController.
    ///
    /// 전환표(인게임 3페이즈): <c>Story↔Schedule</c>, <c>*→Ending</c>. Ending은 인게임 종착(나가는 전환 없음 —
    /// Ending→Title은 씬로드=GameManager 소관, 범위 밖). 동일 페이즈 재요청은 무효(no-op 거부).
    /// </summary>
    public static class PhaseService
    {
        public static PhaseTransitionResult Resolve(ScreenPhase from, ScreenPhase to)
        {
            if (from == to)
                return new PhaseTransitionResult(false, from, to, $"동일 페이즈 재요청: {from}");
            if (!IsValid(from, to))
                return new PhaseTransitionResult(false, from, to, $"무효 전환: {from}→{to}");
            return new PhaseTransitionResult(true, from, to, null);
        }

        // *→Ending 허용(Ending 자기 자신은 from==to에서 이미 거부). 그 외엔 Story↔Schedule만.
        static bool IsValid(ScreenPhase from, ScreenPhase to)
        {
            if (to == ScreenPhase.Ending) return true; // Story/Schedule → Ending
            switch (from)
            {
                case ScreenPhase.Story:    return to == ScreenPhase.Schedule;
                case ScreenPhase.Schedule: return to == ScreenPhase.Story;
                default:                 return false; // Ending → X 없음
            }
        }
    }
}
