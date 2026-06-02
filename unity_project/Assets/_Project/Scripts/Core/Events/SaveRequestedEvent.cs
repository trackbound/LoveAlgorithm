namespace LoveAlgo.Events
{
    /// <summary>
    /// 세이브를 요청하는 명령 이벤트(EventBus). 발행자(GameManager 등)는 저장 '의도'만 알리고,
    /// 실제 직렬화/IO는 <c>SaveManager</c> 구독자가 수행한다(ADR-007, Service Locator 없음).
    /// </summary>
    public readonly struct SaveRequestedEvent
    {
        /// <summary>대상 슬롯(0=자동저장, 1+=유저 슬롯).</summary>
        public readonly int Slot;
        /// <summary>트리거 출처(로그/디버그용, 예: "day-end").</summary>
        public readonly string Reason;

        public SaveRequestedEvent(int slot, string reason)
        {
            Slot = slot;
            Reason = reason;
        }
    }
}
