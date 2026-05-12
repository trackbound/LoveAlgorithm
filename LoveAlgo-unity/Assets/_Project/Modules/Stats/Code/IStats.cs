namespace LoveAlgo.Modules.Stats
{
    /// <summary>
    /// 플레이어 스탯 모듈 외부 계약.
    /// 다른 모듈은 GameState 직접 참조 대신 이 인터페이스 사용.
    /// 구현: <see cref="StatsModule"/>.
    /// </summary>
    public interface IStats
    {
        /// <summary>스탯 값 조회. statId는 "Strength"/"Intelligence"/"Social"/"Perseverance"/"Fatigue".</summary>
        int Get(string statId);

        /// <summary>스탯 증감. <see cref="StatChangedEvent"/> 발행.</summary>
        void Add(string statId, int delta);

        /// <summary>스탯 직접 설정. <see cref="StatChangedEvent"/> 발행.</summary>
        void Set(string statId, int value);
    }
}
