namespace LoveAlgo.Contracts
{
    /// <summary>
    /// 게임 엔딩 종류 — 총 10가지 (기획서 기준).
    /// C4-A에서 LoveAlgo.Modules.Affinity → LoveAlgo.Contracts로 이동.
    ///
    /// 4 일반 히로인 × (해피/새드) = 8
    /// 로아 메리배드(전용) = 1
    /// 노 고백 = 1
    /// </summary>
    public enum EndingType
    {
        None,           // 미정 / 기본값

        // 일반 히로인 — 임계치 충족 시 해피, 미충족 시 새드
        HappyYeun,
        HappyDaeun,
        HappyBom,
        HappyHeewon,

        SadYeun,
        SadDaeun,
        SadBom,
        SadHeewon,

        // 로아 — 전 이벤트 로아 선택 + 피로≥70 + 임계치 충족 시 진입
        RoaMeriBad,

        // 고백 이벤트에서 누구에게도 고백하지 않음
        NoConfession,
    }
}
