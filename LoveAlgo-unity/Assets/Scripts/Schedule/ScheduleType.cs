namespace LoveAlgo.Schedule
{
    /// <summary>
    /// 스케줄 카테고리
    /// 기획서 기준: 알바 서브메뉴 펼쳐짐 (편의점/상하차), 운동, 공부, 투자
    /// </summary>
    public enum ScheduleCategory
    {
        PartTime,   // 아르바이트 (편의점, 상하차)
        Activity    // 운동, 공부, 투자
    }

    /// <summary>
    /// 스케줄 종류 (기획서 기준 5개)
    /// 자유행동: 하루 2회 (낮/밤)
    /// </summary>
    public enum ScheduleType
    {
        // 아르바이트
        PartTime_Store,     // 편의점: 끈기+1, 돈+20000, 피로+5
        PartTime_Loading,   // 상하차: 끈기+2, 돈+50000, 피로+15 (하루 1회 제한)

        // 활동
        Exercise,           // 운동: 체력+3, 피로+0
        Study,              // 공부: 지성+3, 피로+5
        Invest              // 투자: 돈 ±50~100%, 피로+0 (자산≥30000 필요)
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
            // 아르바이트
            ScheduleType.PartTime_Store   => new("편의점 알바",
                "기본 노동, 끈기 소량 상승.\n안정적인 수입원입니다.",
                20000, 0, 0, 0, 1, 5),

            ScheduleType.PartTime_Loading => new("상하차 알바",
                "고강도 노동, 끈기 크게 상승.\n하루 1회만 가능합니다.",
                50000, 0, 0, 0, 2, 15, limited: true),

            // 활동
            ScheduleType.Exercise => new("운동",
                "체력의 주요 성장 루트.\n아이템(프로틴)과 조합 가능.",
                0, 3, 0, 0, 0, 0),

            ScheduleType.Study => new("공부",
                "지성을 높일 수 있어요.\n피로도가 높습니다.",
                0, 0, 3, 0, 0, 5),

            ScheduleType.Invest => new("투자",
                "돈 ±50~100% (확률 분포).\n자산 30,000원 이상 시 가능.",
                0, 0, 0, 0, 0, 0),

            _ => new("알 수 없음", "", 0, 0, 0, 0, 0, 0)
        };

        public static ScheduleCategory GetCategory(ScheduleType type) => type switch
        {
            ScheduleType.PartTime_Store or
            ScheduleType.PartTime_Loading => ScheduleCategory.PartTime,

            _ => ScheduleCategory.Activity
        };
    }
}
