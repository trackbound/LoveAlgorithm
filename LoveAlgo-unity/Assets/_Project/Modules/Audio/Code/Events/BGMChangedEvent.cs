namespace LoveAlgo.Modules.Audio
{
    /// <summary>BGM이 변경되거나 정지될 때 발행. Name이 null/빈문자열이면 정지.</summary>
    public readonly struct BGMChangedEvent
    {
        public readonly string Name;

        public BGMChangedEvent(string name)
        {
            Name = name;
        }

        public bool IsStop => string.IsNullOrEmpty(Name);
    }
}
