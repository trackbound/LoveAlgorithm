namespace LoveAlgo.Events
{
    /// <summary>
    /// 세이브가 완료(또는 실패)됐음을 알리는 통지 이벤트(EventBus). 구독자(저장 토스트 UI 등, M5)가 반응.
    /// <c>SaveManager</c>가 <c>SaveService.Save</c> 결과로 발행한다.
    /// </summary>
    public readonly struct SaveCompletedEvent
    {
        public readonly int Slot;
        public readonly bool Success;

        public SaveCompletedEvent(int slot, bool success)
        {
            Slot = slot;
            Success = success;
        }
    }
}
