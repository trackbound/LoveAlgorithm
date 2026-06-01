namespace LoveAlgo.Core
{
    /// <summary>
    /// 스토리 아크 (게임 흐름 구간)
    /// 개강 → 1차 이벤트 → 축제 → 2차 이벤트 → MT → 3차 이벤트 → 고백 → 엔딩
    /// </summary>
    public enum StoryArc
    {
        Opening,        // 개강 (프롤로그 직후)
        FreeTime1,      // 1차 이벤트 전 자유행동
        Event1,         // 1차 개인 이벤트 (데이트, +3점)
        FreeTime2,      // 축제 전 자유행동
        Festival,       // 단체이벤트: 축제 (3일, +4점)
        FreeTime3,      // 2차 이벤트 전 자유행동
        Event2,         // 2차 개인 이벤트 (데이트, +6점)
        FreeTime4,      // MT 전 자유행동
        MT,             // 단체이벤트: MT/바다 (+5점)
        FreeTime5,      // 3차 이벤트 전 자유행동
        Event3,         // 3차 개인 이벤트 (데이트, +9점)
        FreeTime6,      // 고백 전 마지막 자유행동
        Confession      // 고백 이벤트
    }

    /// <summary>
    /// 날짜별 스케줄 타입
    /// </summary>
    public enum DayType
    {
        Free,           // 자유행동 가능
        PersonalEvent,  // 개인 이벤트 (히로인 선택)
        GroupEvent,      // 단체 이벤트 (자유행동 없음)
        Confession      // 고백 이벤트 (자유행동 없음)
    }
}
