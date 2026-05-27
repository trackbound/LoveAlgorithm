namespace LoveAlgo.Contracts
{
    /// <summary>
    /// 호감도/엔딩 분기 모듈 외부 계약.
    /// 다른 모듈은 AffinityCalculator/HeroinePointTracker 직접 참조 대신 이 인터페이스 사용.
    /// 구현: <see cref="AffinityModule"/>.
    /// </summary>
    public interface IAffinity
    {
        /// <summary>히로인 호감도 정보 (총점·임계치·등급·자격 포함).</summary>
        AffinityInfo Get(string heroineId);

        /// <summary>전체 히로인 호감도 일괄 조회.</summary>
        AffinityInfo[] GetAll();

        /// <summary>카테고리 포인트 추가. <see cref="AffinityChangedEvent"/> 발행.</summary>
        void AddPoint(string heroineId, PointCategory category, int amount);

        /// <summary>이벤트 선택 기록 + 기본 포인트 추가.</summary>
        void RecordEventChoice(string heroineId, string eventTag, int basePoints);

        /// <summary>고백 시점에 엔딩 대상 히로인 결정. null이면 노멀 엔딩.</summary>
        string DetermineEndingHeroine();

        /// <summary>해당 히로인 해피엔딩 자격 여부 (총점 ≥ 임계치).</summary>
        bool IsHappyEnding(string heroineId);

        /// <summary>
        /// 고백 이벤트에서 플레이어가 선택한 히로인을 기준으로 최종 엔딩 종류 결정.
        /// confessedHeroineId가 null/빈 문자열이면 NoConfession.
        /// </summary>
        EndingType GetEnding(string confessedHeroineId);

        /// <summary>로아 고백 가능 여부 (피로 ≥ 70). 미충족 시 고백 버튼 흑백 처리.</summary>
        bool IsRoaConfessionUnlocked();
    }
}
