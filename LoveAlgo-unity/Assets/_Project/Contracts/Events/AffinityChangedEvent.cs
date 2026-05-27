namespace LoveAlgo.Contracts
{
    /// <summary>호감도 포인트가 변경되면 발행. 변경 후 총점은 NewTotal.</summary>
    public readonly struct AffinityChangedEvent
    {
        public readonly string HeroineId;
        public readonly PointCategory Category;
        public readonly int Delta;
        public readonly int NewTotal;

        public AffinityChangedEvent(string heroineId, PointCategory category, int delta, int newTotal)
        {
            HeroineId = heroineId;
            Category = category;
            Delta = delta;
            NewTotal = newTotal;
        }
    }
}
