namespace LoveAlgo.Modules.Stats
{
    /// <summary>스탯이 변경되면 발행. statId는 GameConstants.CombatStatIds 또는 "Fatigue".</summary>
    public readonly struct StatChangedEvent
    {
        public readonly string StatId;
        public readonly int OldValue;
        public readonly int NewValue;

        public StatChangedEvent(string statId, int oldValue, int newValue)
        {
            StatId = statId;
            OldValue = oldValue;
            NewValue = newValue;
        }

        public int Delta => NewValue - OldValue;
    }
}
