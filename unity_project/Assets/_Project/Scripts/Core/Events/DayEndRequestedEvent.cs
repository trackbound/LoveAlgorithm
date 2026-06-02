namespace LoveAlgo.Events
{
    /// <summary>
    /// 그날 자유행동을 모두 소진해 하루 종료가 필요함을 알리는 통지 이벤트(EventBus).
    /// 발행 경계: 자유행동을 소비한 통합층이 <c>DayLoop.ConsumeAction</c>이 소진 신호(true)를 주면 발행한다.
    /// 실제 하루 전환(저녁 이벤트·페이드·오토세이브·<c>DayLoop.AdvanceDay</c>)은 오케스트레이션이므로
    /// GameManager(M4/M5) 구독자가 수행한다. 여기선 "하루가 끝났다"는 사실만 전파(ADR-007).
    /// </summary>
    public readonly struct DayEndRequestedEvent
    {
        /// <summary>하루 종료가 요청된 시점의 현재 일차.</summary>
        public readonly int Day;

        public DayEndRequestedEvent(int day)
        {
            Day = day;
        }
    }
}
