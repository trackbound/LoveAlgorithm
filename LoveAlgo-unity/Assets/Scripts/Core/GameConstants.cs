namespace LoveAlgo
{
    /// <summary>
    /// 프로젝트 전역 상수 정의
    /// 히로인 목록, 스탯 종류 등 여러 곳에서 참조하는 값들
    /// </summary>
    public static class GameConstants
    {
        /// <summary>
        /// 히로인 ID 목록 (영문, 코드에서 사용)
        /// </summary>
        public static readonly string[] HeroineIds = 
        { 
            "Roa", "Yeun", "Daeun", "Bom", "Heewon" 
        };

        /// <summary>
        /// 히로인 표시 이름 (한글, UI에 표시)
        /// HeroineIds와 인덱스 동일
        /// </summary>
        public static readonly string[] HeroineNames = 
        { 
            "로아", "하예은", "서다은", "이봄", "도희원" 
        };

        /// <summary>
        /// 플레이어 스탯 ID 목록
        /// </summary>
        public static readonly string[] StatIds = 
        { 
            "Str", "Int", "Soc", "Per", "Fatigue" 
        };

        /// <summary>
        /// 플레이어 스탯 표시 이름 (한글)
        /// StatIds와 인덱스 동일
        /// </summary>
        public static readonly string[] StatNames = 
        { 
            "체력", "지성", "사교성", "끈기", "피로" 
        };

        /// <summary>
        /// 캐릭터 슬롯 위치
        /// </summary>
        public static readonly string[] SlotPositions = { "L", "C", "R" };

        /// <summary>
        /// 기본 표정 목록
        /// </summary>
        public static readonly string[] DefaultEmotes = 
        { 
            "Default", "Happy", "Sad", "Angry", "Blush", "Surprise", "Think", "Shock" 
        };

        /// <summary>
        /// 히로인 수
        /// </summary>
        public const int HeroineCount = 5;

        /// <summary>
        /// 스탯 수
        /// </summary>
        public const int StatCount = 5;

        /// <summary>
        /// 스탯 최대값
        /// </summary>
        public const int MaxStat = 100;

        /// <summary>
        /// 지원 해상도 목록 (오름차순)
        /// </summary>
        public static readonly (int w, int h)[] Resolutions =
        {
            (800, 450), (960, 540), (1280, 720), (1440, 810), (1920, 1080)
        };

        /// <summary>
        /// 기본 해상도 인덱스 (1920x1080)
        /// </summary>
        public const int DefaultResolutionIndex = 4;

        /// <summary>
        /// 하루 자유행동 횟수 (낮/밤)
        /// </summary>
        public const int ActionsPerDay = 2;

        /// <summary>
        /// 최대 일차 (이 일수가 지나면 엔딩 진입)
        /// </summary>
        public const int MaxDay = 30;

        /// <summary>
        /// 히로인별 엔딩 임계치 (포인트)
        /// HeroineIds 인덱스와 동일: Roa=46, Yeun=32, Daeun=35, Bom=39, Heewon=43
        /// </summary>
        public static readonly int[] EndingThresholds =
        {
            46, 32, 35, 39, 43
        };

        /// <summary>
        /// 히로인별 선호 스탯 ID
        /// Roa="Fatigue", Yeun="Str", Daeun="Int", Bom="Soc", Heewon="Per"
        /// </summary>
        public static readonly string[] HeroinePreferredStat =
        {
            "Fatigue", "Str", "Int", "Soc", "Per"
        };

        /// <summary>
        /// 엔딩 진입에 필요한 최소 호감도 (레거시 호환용, 개별 임계치 사용 권장)
        /// </summary>
        public const int EndingLoveThreshold = 30;
    }
}
