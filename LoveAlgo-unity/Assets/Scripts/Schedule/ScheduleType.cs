namespace LoveAlgo.Schedule
{
    /// <summary>
    /// 스케줄 카테고리 (탭 3개)
    /// </summary>
    public enum ScheduleCategory
    {
        PartTime,   // 아르바이트
        Exercise,   // 운동
        Study       // 공부
    }

    /// <summary>
    /// 스케줄 종류 (총 9개, 고정)
    /// </summary>
    public enum ScheduleType
    {
        // 아르바이트
        PartTime_Cafe,      // 카페 알바
        PartTime_Tutor,     // 과외
        PartTime_Delivery,  // 배달

        // 운동
        Exercise_Gym,       // 헬스장
        Exercise_Running,   // 러닝
        Exercise_Swimming,  // 수영

        // 공부
        Study_Library,      // 도서관
        Study_Lecture,      // 강의
        Study_Group         // 스터디그룹
    }

    /// <summary>
    /// 스케줄 효과 데이터
    /// </summary>
    public struct ScheduleEffect
    {
        public string displayName;
        public int moneyChange;     // + 수입, - 지출
        public int strengthChange;  // 체력
        public int intelligenceChange; // 지성
        public int socialChange;    // 사교성
        public int perseveranceChange; // 끈기
        public int fatigueChange;   // 피로

        public ScheduleEffect(string name, int money, int str, int intel, int soc, int per, int fatigue)
        {
            displayName = name;
            moneyChange = money;
            strengthChange = str;
            intelligenceChange = intel;
            socialChange = soc;
            perseveranceChange = per;
            fatigueChange = fatigue;
        }
    }

    /// <summary>
    /// 스케줄 데이터 정적 테이블
    /// </summary>
    public static class ScheduleTable
    {
        public static ScheduleEffect Get(ScheduleType type) => type switch
        {
            // 아르바이트: 돈+, 피로+
            ScheduleType.PartTime_Cafe     => new("카페 알바",   15000, 0, 0, 1, 0, 10),
            ScheduleType.PartTime_Tutor    => new("과외",       30000, 0, 1, 0, 0, 15),
            ScheduleType.PartTime_Delivery => new("배달",       20000, 1, 0, 0, 1, 20),

            // 운동: 체력+, 끈기+, 피로+
            ScheduleType.Exercise_Gym      => new("헬스장",     -5000, 3, 0, 0, 1, 15),
            ScheduleType.Exercise_Running  => new("러닝",           0, 2, 0, 0, 2, 10),
            ScheduleType.Exercise_Swimming => new("수영",      -10000, 2, 0, 1, 1, 12),

            // 공부: 지성+, 피로+
            ScheduleType.Study_Library     => new("도서관",         0, 0, 3, 0, 1, 12),
            ScheduleType.Study_Lecture     => new("강의",      -20000, 0, 4, 0, 0, 10),
            ScheduleType.Study_Group       => new("스터디그룹",     0, 0, 2, 2, 0, 8),

            _ => new("알 수 없음", 0, 0, 0, 0, 0, 0)
        };

        public static ScheduleCategory GetCategory(ScheduleType type) => type switch
        {
            ScheduleType.PartTime_Cafe or 
            ScheduleType.PartTime_Tutor or 
            ScheduleType.PartTime_Delivery => ScheduleCategory.PartTime,

            ScheduleType.Exercise_Gym or 
            ScheduleType.Exercise_Running or 
            ScheduleType.Exercise_Swimming => ScheduleCategory.Exercise,

            ScheduleType.Study_Library or 
            ScheduleType.Study_Lecture or 
            ScheduleType.Study_Group => ScheduleCategory.Study,

            _ => ScheduleCategory.PartTime
        };
    }
}
