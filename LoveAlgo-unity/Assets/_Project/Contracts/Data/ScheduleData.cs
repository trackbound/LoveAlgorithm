namespace LoveAlgo.Contracts
{
    /// <summary>
    /// 스케줄 카테고리 (탭 3개).
    /// C4-Phase A에서 LoveAlgo.Schedule → LoveAlgo.Contracts 로 이동.
    /// </summary>
    public enum ScheduleCategory
    {
        PartTime,   // 알바 (편의점, 상하차, 투자)
        Exercise,   // 운동 (임시 A, B, C)
        Study       // 공부 (임시 D, E, F)
    }

    /// <summary>
    /// 스케줄 종류 (탭 3개 × 3개 = 9개).
    /// 자유행동: 하루 2회 (낮/밤).
    /// C4-Phase A에서 LoveAlgo.Schedule → LoveAlgo.Contracts 로 이동.
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
    /// 스케줄 효과 데이터. [Serializable] — ScheduleDataSO 인스펙터 직렬화 호환.
    /// C4-Phase A에서 LoveAlgo.Schedule → LoveAlgo.Contracts 로 이동.
    /// </summary>
    [System.Serializable]
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
}
