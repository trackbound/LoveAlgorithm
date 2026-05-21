namespace LoveAlgo.Core
{
    /// <summary>
    /// GameManager.CurrentPhase가 바뀔 때 EventBus로 발행. UI/모듈이 강타입 참조 없이 phase 전환을 감지.
    /// </summary>
    public readonly struct GamePhaseChangedEvent
    {
        public readonly GamePhase Previous;
        public readonly GamePhase Current;
        public GamePhaseChangedEvent(GamePhase previous, GamePhase current)
        {
            Previous = previous;
            Current = current;
        }
    }
}
