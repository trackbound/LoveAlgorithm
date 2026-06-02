namespace LoveAlgo.Events
{
    /// <summary>
    /// 하루가 전환되어 새 일차로 진입했음을 알리는 통지 이벤트(EventBus). 구독자(HUD·페이즈 UI 등, M5)가 반응.
    ///
    /// 발행 경계: 하루전환 오케스트레이션(GameManager)이 <c>DayLoop.AdvanceDay</c> 후 발행한다.
    /// 새 일차가 MaxDay를 넘겨 엔딩 구간에 들어선 경우엔 이 이벤트 대신 <see cref="EnteredEndingEvent"/>가 발행된다.
    /// 구 <c>LoveAlgo.Modules.DayLoop.DayChangedEvent</c>의 의미 이식 — 매 프레임 폴링 대신 전환 시점 명시적 발행(ADR-007).
    /// Core asmdef에 두는 이유: 발행자(GameManager)와 구독자(UI)가 공통 참조하는 최하위 계층.
    /// </summary>
    public readonly struct DayChangedEvent
    {
        public readonly int PreviousDay;
        public readonly int NewDay;

        public DayChangedEvent(int previousDay, int newDay)
        {
            PreviousDay = previousDay;
            NewDay = newDay;
        }
    }
}
