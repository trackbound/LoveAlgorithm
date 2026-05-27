namespace LoveAlgo.Contracts
{
    // C4-Phase A: LoveAlgo.Modules.DayLoop → LoveAlgo.Contracts 로 이동.

    /// <summary>
    /// 게임 진행 페이즈 — 학기 흐름.
    /// 기획서: 개강 → 1차 → 축제 → 2차 → MT → 3차 → 고백 → 엔딩
    ///
    /// 일자 매핑 (DayEventTable 기준):
    ///   Day 1-5    : Opening
    ///   Day 6      : Event1
    ///   Day 7-9    : OpeningAfterEvent1 (Event1 이후 자유행동)
    ///   Day 10-12  : Festival
    ///   Day 13-15  : AfterFestival
    ///   Day 16     : Event2
    ///   Day 17-19  : AfterEvent2
    ///   Day 20-22  : MT
    ///   Day 23-25  : AfterMT
    ///   Day 26     : Event3
    ///   Day 27-29  : AfterEvent3
    ///   Day 30     : Confession
    ///   Day 30+    : Ending
    /// </summary>
    public enum EventPhase
    {
        Opening,
        Event1,
        AfterEvent1,
        Festival,
        AfterFestival,
        Event2,
        AfterEvent2,
        MT,
        AfterMT,
        Event3,
        AfterEvent3,
        Confession,
        Ending,
    }
}
