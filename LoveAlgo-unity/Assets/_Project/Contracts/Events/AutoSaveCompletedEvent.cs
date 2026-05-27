namespace LoveAlgo.Contracts
{
    /// <summary>
    /// 자동저장이 성공적으로 끝났을 때 발행.
    /// Reason: SessionController.AutoSaveAsync(reason) 인자 그대로 — 어디서 트리거됐는지 추적.
    /// 예: "day-end", "phase:DayLoop", "macro:DayEnd", "scripted".
    /// </summary>
    public readonly struct AutoSaveCompletedEvent
    {
        public readonly string Reason;
        public readonly int Slot;

        public AutoSaveCompletedEvent(string reason, int slot)
        {
            Reason = reason ?? "";
            Slot = slot;
        }
    }
}
