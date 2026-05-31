namespace LoveAlgo.Contracts
{
    /// <summary>일자가 바뀌면 발행.</summary>
    public readonly struct DayChangedEvent
    {
        public readonly int OldDay;
        public readonly int NewDay;

        public DayChangedEvent(int oldDay, int newDay)
        {
            OldDay = oldDay;
            NewDay = newDay;
        }
    }
}
