using LoveAlgo.Core; // ScreenPhase

namespace LoveAlgo.Events
{
    /// <summary>
    /// 화면 페이즈 전환 *의도*(EventBus). 피처는 이것만 발행하고 직접 화면을 토글하지 않는다 —
    /// 유일 권위자 <c>PhaseController</c>가 순수 <c>PhaseService</c>로 검증 후 적용·통지(ADR-013).
    /// 예: 내러티브 시작 → RequestPhaseCommand(Story), 30일 종료 → RequestPhaseCommand(Ending).
    /// </summary>
    public readonly struct RequestPhaseCommand
    {
        public readonly ScreenPhase Target;
        public RequestPhaseCommand(ScreenPhase target) { Target = target; }
    }

    /// <summary>
    /// 화면 페이즈가 실제로 바뀐 *사실* 통지. <c>PhaseController</c>만 발행한다. UIManager(그룹 토글)·
    /// 화면 뷰들이 구독해 반응한다(ADR-007). 거부된 전환에는 발행되지 않는다.
    /// </summary>
    public readonly struct ScreenPhaseChangedEvent
    {
        public readonly ScreenPhase From;
        public readonly ScreenPhase To;
        public ScreenPhaseChangedEvent(ScreenPhase from, ScreenPhase to) { From = from; To = to; }
    }
}
