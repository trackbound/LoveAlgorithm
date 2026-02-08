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
    }
}
