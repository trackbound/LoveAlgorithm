namespace LoveAlgo.Contracts
{
    /// <summary>
    /// 호감도 등급 (실시간 조회용).
    /// C4-A에서 LoveAlgo.Modules.Affinity → LoveAlgo.Contracts로 이동.
    /// 데이터/enum이므로 인터페이스 시그니처에 쓰여도 Contracts 자체가 leaf 유지.
    /// </summary>
    public enum AffinityTier
    {
        Stranger,       // 0~9점: 모르는 사이
        Acquaintance,   // 10~19점: 아는 사이
        Friend,         // 20~29점: 친구
        CloseFriend,    // 30~39점: 가까운 친구
        Love            // 40점 이상: 연인 후보
    }

    /// <summary>
    /// 히로인별 호감도 요약 정보 (불변 스냅샷).
    /// C4-A에서 LoveAlgo.Modules.Affinity → LoveAlgo.Contracts로 이동.
    /// </summary>
    public readonly struct AffinityInfo
    {
        /// <summary>히로인 ID</summary>
        public readonly string HeroineId;

        /// <summary>기본 포인트 합계 (이벤트+대화+선물+미니게임)</summary>
        public readonly int BasePoints;

        /// <summary>스탯 보정값 (선호스탯 1등=+3, 공동1등=+1)</summary>
        public readonly int StatBonus;

        /// <summary>특수 보정값 (로아 피로보정 등)</summary>
        public readonly int SpecialBonus;

        /// <summary>최종 총점 (BasePoints + StatBonus + SpecialBonus)</summary>
        public readonly int TotalScore;

        /// <summary>해당 히로인의 엔딩 필요 임계치</summary>
        public readonly int Threshold;

        /// <summary>임계치까지 남은 포인트 (음수면 초과 달성)</summary>
        public readonly int Remaining;

        /// <summary>현재 호감도 등급</summary>
        public readonly AffinityTier Tier;

        /// <summary>이벤트 선택 횟수</summary>
        public readonly int EventSelections;

        /// <summary>엔딩 자격 충족 여부</summary>
        public readonly bool IsEndingEligible;

        public AffinityInfo(string heroineId, int basePoints, int statBonus, int specialBonus,
            int threshold, int eventSelections)
        {
            HeroineId = heroineId;
            BasePoints = basePoints;
            StatBonus = statBonus;
            SpecialBonus = specialBonus;
            TotalScore = basePoints + statBonus + specialBonus;
            Threshold = threshold;
            Remaining = threshold - TotalScore;
            Tier = ScoreToTier(TotalScore);
            EventSelections = eventSelections;

            // 로아: 피로≥70 필수 / 나머지: 이벤트 1회 이상
            IsEndingEligible = TotalScore >= threshold && eventSelections >= 1;
        }

        static AffinityTier ScoreToTier(int score)
        {
            if (score >= 40) return AffinityTier.Love;
            if (score >= 30) return AffinityTier.CloseFriend;
            if (score >= 20) return AffinityTier.Friend;
            if (score >= 10) return AffinityTier.Acquaintance;
            return AffinityTier.Stranger;
        }
    }
}
