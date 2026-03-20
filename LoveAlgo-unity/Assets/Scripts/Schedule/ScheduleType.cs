namespace LoveAlgo.Schedule
{
    /// <summary>
    /// 스케줄 카테고리 (탭 3개)
    /// </summary>
    public enum ScheduleCategory
    {
        PartTime,   // 알바 (편의점, 상하차, 투자)
        Exercise,   // 운동 (임시 A, B, C)
        Study       // 공부 (임시 D, E, F)
    }

    /// <summary>
    /// 스케줄 종류 (탭 3개 × 3개 = 9개)
    /// 자유행동: 하루 2회 (낮/밤)
    /// </summary>
    public enum ScheduleType
    {
        // 알바
        PartTime_Store,     // 편의점: 끈기+1, 돈+20000, 피로+5
        PartTime_Loading,   // 상하차: 끈기+2, 돈+50000, 피로+15 (하루 1회 제한)
        Invest,             // 코인 투자: 돈 ±50~100%, 피로+0 (자산≥30000 필요)

        // 운동 (기획 추가 예정 — 임시)
        Exercise_A,
        Exercise_B,
        Exercise_C,

        // 공부 (기획 추가 예정 — 임시)
        Study_D,
        Study_E,
        Study_F
    }

    /// <summary>
    /// 스케줄 효과 데이터
    /// </summary>
    public struct ScheduleEffect
    {
        public string displayName;
        public string description;     // 3줄 설명
        public int moneyChange;        // + 수입, - 지출
        public int strengthChange;     // 체력
        public int intelligenceChange; // 지성
        public int socialChange;       // 사교성
        public int perseveranceChange; // 끈기
        public int fatigueChange;      // 피로
        public bool isLimited;         // 하루 1회 제한 여부

        public ScheduleEffect(string name, string desc, int money, int str, int intel, int soc, int per, int fatigue, bool limited = false)
        {
            displayName = name;
            description = desc;
            moneyChange = money;
            strengthChange = str;
            intelligenceChange = intel;
            socialChange = soc;
            perseveranceChange = per;
            fatigueChange = fatigue;
            isLimited = limited;
        }
    }

    /// <summary>
    /// 스케줄 데이터 정적 테이블 (기획서 기준)
    /// </summary>
    public static class ScheduleTable
    {
        public static ScheduleEffect Get(ScheduleType type) => type switch
        {
            // 알바
            ScheduleType.PartTime_Store   => new("편의점",
                "어쩌구 편의점에서 아르바이트를 합니다.\n10,000원의 수익을 획득합니다.",
                20000, 0, 0, 0, 1, 5),

            ScheduleType.PartTime_Loading => new("상하차 알바",
                "상하차를 하면 돈도 벌고 힘도 세지고\n50,000원을 버는데\n밤은 새야할지도 어쩌구 저쩌구 3줄까지 입니다.",
                50000, 0, 0, 0, 2, 15, limited: true),

            ScheduleType.Invest => new("코인투자",
                "영차영차\n다같이 외쳐 영차영차",
                0, 0, 0, 0, 0, 0),

            // 운동 (임시)
            ScheduleType.Exercise_A => new("운동 A",
                "(기획 추가 예정)",
                0, 3, 0, 0, 0, 0),

            ScheduleType.Exercise_B => new("운동 B",
                "(기획 추가 예정)",
                0, 2, 0, 0, 0, 5),

            ScheduleType.Exercise_C => new("운동 C",
                "(기획 추가 예정)",
                0, 1, 0, 0, 1, 3),

            // 공부 (임시)
            ScheduleType.Study_D => new("공부 D",
                "(기획 추가 예정)",
                0, 0, 3, 0, 0, 5),

            ScheduleType.Study_E => new("공부 E",
                "(기획 추가 예정)",
                0, 0, 2, 0, 0, 3),

            ScheduleType.Study_F => new("공부 F",
                "(기획 추가 예정)",
                0, 0, 1, 1, 0, 2),

            _ => new("알 수 없음", "", 0, 0, 0, 0, 0, 0)
        };

        public static ScheduleCategory GetCategory(ScheduleType type) => type switch
        {
            ScheduleType.PartTime_Store or
            ScheduleType.PartTime_Loading or
            ScheduleType.Invest => ScheduleCategory.PartTime,

            ScheduleType.Exercise_A or
            ScheduleType.Exercise_B or
            ScheduleType.Exercise_C => ScheduleCategory.Exercise,

            ScheduleType.Study_D or
            ScheduleType.Study_E or
            ScheduleType.Study_F => ScheduleCategory.Study,

            _ => ScheduleCategory.PartTime
        };

        /// <summary>해당 카테고리에 속하는 모든 ScheduleType 반환</summary>
        public static ScheduleType[] GetTypes(ScheduleCategory category) => category switch
        {
            ScheduleCategory.PartTime => new[] { ScheduleType.PartTime_Store, ScheduleType.PartTime_Loading, ScheduleType.Invest },
            ScheduleCategory.Exercise => new[] { ScheduleType.Exercise_A, ScheduleType.Exercise_B, ScheduleType.Exercise_C },
            ScheduleCategory.Study    => new[] { ScheduleType.Study_D, ScheduleType.Study_E, ScheduleType.Study_F },
            _ => System.Array.Empty<ScheduleType>()
        };

        /// <summary>카테고리 한글 이름</summary>
        public static string GetCategoryName(ScheduleCategory cat) => cat switch
        {
            ScheduleCategory.PartTime => "알바",
            ScheduleCategory.Exercise => "운동",
            ScheduleCategory.Study    => "공부",
            _ => ""
        };

        /// <summary>카테고리 설명</summary>
        public static string GetCategoryDescription(ScheduleCategory cat) => cat switch
        {
            ScheduleCategory.PartTime => "돈을 벌 수 있어요. 피로도가 오릅니다.",
            ScheduleCategory.Exercise => "체력을 올릴 수 있어요.",
            ScheduleCategory.Study    => "지성을 올릴 수 있어요. 피로도가 오릅니다.",
            _ => ""
        };
    }
}
