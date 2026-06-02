namespace LoveAlgo.Events
{
    /// <summary>
    /// 마지막 날(MaxDay)을 넘겨 엔딩 구간에 진입했음을 알리는 통지 이벤트(EventBus). 구독자(엔딩 화면·페이즈 전환, M5)가 반응.
    ///
    /// 발행 경계: 하루전환 오케스트레이션(GameManager)이 <c>DayLoop.AdvanceDay</c> 결과 EnteredEnding=true일 때 발행한다.
    /// 이 경우 정상 하루 진행(<see cref="DayChangedEvent"/>·오토세이브)은 수행하지 않는다 —
    /// 구 <c>DayLoopController.EndDayAsync</c>도 엔딩 진입 시 오토세이브 전에 return 했다.
    /// </summary>
    public readonly struct EnteredEndingEvent
    {
        /// <summary>엔딩에 진입한 시점의 일차(= MaxDay + 1).</summary>
        public readonly int Day;

        public EnteredEndingEvent(int day)
        {
            Day = day;
        }
    }
}
