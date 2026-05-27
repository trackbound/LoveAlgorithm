namespace LoveAlgo.Contracts
{
    /// <summary>
    /// 포인트 카테고리 (기획서: 이벤트60% + 대화20% + 선물15% + 미니게임5%).
    /// C4-A에서 LoveAlgo.Modules.Affinity → LoveAlgo.Contracts로 이동.
    /// </summary>
    public enum PointCategory
    {
        Event,      // 이벤트 선택 (1차+3, 축제+4, 2차+6, MT+5, 3차+9 = 최대 27점)
        Dialogue,   // 대화 선택지 (총 15회, 전부 +1 = 최대 15점)
        Gift,       // 선물 (2차/3차에서 계층별 +1~+5, 합계 최대 +8점)
        MiniGame    // 미니게임 보너스 (1차 최대+2, 2차 최대+3 = 최대 4점 + 1점)
    }
}
