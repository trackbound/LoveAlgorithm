using LoveAlgo.Modules.DayLoop;

namespace LoveAlgo.Contracts
{
    /// <summary>EventPhase가 바뀌면 발행.</summary>
    public readonly struct PhaseChangedEvent
    {
        public readonly EventPhase OldPhase;
        public readonly EventPhase NewPhase;
        public readonly int Day;

        public PhaseChangedEvent(EventPhase oldPhase, EventPhase newPhase, int day)
        {
            OldPhase = oldPhase;
            NewPhase = newPhase;
            Day = day;
        }
    }
}
