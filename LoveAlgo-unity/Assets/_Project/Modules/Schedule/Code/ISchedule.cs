namespace LoveAlgo.Schedule
{
    /// <summary>
    /// 스케줄(자유행동) 모듈 외부 계약.
    /// 효과 데이터 조회, 카테고리 타입 목록 제공.
    /// 구현: <see cref="ScheduleModule"/>.
    /// </summary>
    public interface ISchedule
    {
        /// <summary>해당 타입의 스케줄 효과 데이터 조회.</summary>
        ScheduleEffect GetEffect(ScheduleType type);

        /// <summary>카테고리에 속한 스케줄 타입 목록.</summary>
        ScheduleType[] GetTypes(ScheduleCategory category);

        /// <summary>해당 카테고리 한글 표시명.</summary>
        string GetCategoryName(ScheduleCategory category);

        /// <summary>해당 타입의 카테고리 반환.</summary>
        ScheduleCategory GetCategory(ScheduleType type);

        /// <summary>ScheduleUI 인스턴스 (lazy spawn).</summary>
        ScheduleUI ScheduleUI { get; }
    }
}
