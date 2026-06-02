namespace LoveAlgo.Events
{
    /// <summary>
    /// 소지금이 변경되면 발행되는 통지 이벤트(EventBus). 구독자(HUD 등, M5)가 반응.
    /// 발행 경계: 소지금을 바꾸는 통합층(ScheduleController 등 — 이후 Shop 등)이 변경 후 발행한다.
    /// GameStateSO.Money 세터 자체는 발행하지 않는다(로드/리셋 시 오발행 방지, StatChanged와 동일 원칙).
    /// </summary>
    public readonly struct MoneyChangedEvent
    {
        /// <summary>변경 후 소지금(0 바닥 클램프 적용된 값).</summary>
        public readonly long NewMoney;

        public MoneyChangedEvent(long newMoney)
        {
            NewMoney = newMoney;
        }
    }
}
