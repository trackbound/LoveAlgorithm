namespace LoveAlgo.Schedule
{
    /// <summary>
    /// 스케줄 선택 커맨드(EventBus). UI(M5)가 발행 → <see cref="ScheduleController"/>가 구독·처리.
    /// "명령"도 EventBus로 흘려 Service Locator 없이 디커플한다(ADR-007). 직접 메서드 호출/인터페이스 없음.
    /// </summary>
    public readonly struct ScheduleSelectedCommand
    {
        public readonly ScheduleType Type;
        public ScheduleSelectedCommand(ScheduleType type) { Type = type; }
    }

    /// <summary>
    /// 스케줄 효과가 적용 완료됐음을 알리는 통지(EventBus). 토스트/피드백 UI(M5)가 구독해 연출.
    /// 구 <c>DayLoopController.BuildScheduleFeedbackList</c>의 문자열 조립은 이 이벤트를 받은 UI 책임으로 이전.
    /// </summary>
    public readonly struct ScheduleAppliedEvent
    {
        public readonly ScheduleType Type;
        public readonly ScheduleEffect Effect;
        /// <summary>실제 반영된 소지금 변화액(0 바닥/투자 RNG 반영 후 = 적용 후 − 적용 전).</summary>
        public readonly long MoneyDelta;

        public ScheduleAppliedEvent(ScheduleType type, ScheduleEffect effect, long moneyDelta)
        {
            Type = type;
            Effect = effect;
            MoneyDelta = moneyDelta;
        }
    }

    /// <summary>스케줄 거부 사유(게이트 미충족).</summary>
    public enum ScheduleRejection
    {
        None,
        InsufficientFunds,   // 투자 자금 < GameConstants.MinInvestMoney
        DailyLimitReached    // isLimited 스케줄을 오늘 이미 수행함
    }

    /// <summary>
    /// 스케줄이 게이트에서 거부됐음을 알리는 통지(EventBus). UI(M5)가 "자금 부족" 등 피드백에 사용.
    /// </summary>
    public readonly struct ScheduleRejectedEvent
    {
        public readonly ScheduleType Type;
        public readonly ScheduleRejection Reason;

        public ScheduleRejectedEvent(ScheduleType type, ScheduleRejection reason)
        {
            Type = type;
            Reason = reason;
        }
    }
}
