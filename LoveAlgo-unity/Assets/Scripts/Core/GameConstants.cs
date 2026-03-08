using System.Collections.Generic;

namespace LoveAlgo
{
    /// <summary>
    /// 히로인 설정 구조체 — 병렬 배열 대신 하나의 구조로 관리
    /// </summary>
    public readonly struct HeroineConfig
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly int EndingThreshold;
        public readonly string PreferredStat;

        public HeroineConfig(string id, string displayName, int endingThreshold, string preferredStat)
        {
            Id = id;
            DisplayName = displayName;
            EndingThreshold = endingThreshold;
            PreferredStat = preferredStat;
        }
    }

    /// <summary>
    /// 프로젝트 전역 상수 정의
    /// 히로인 목록, 스탯 종류 등 여러 곳에서 참조하는 값들
    /// </summary>
    public static class GameConstants
    {
        // ── 히로인 통합 설정 ──

        /// <summary>히로인 설정 목록 (인덱스 = 히로인 순서)</summary>
        public static readonly HeroineConfig[] Heroines =
        {
            new("Roa",    "로아",   46, "Fatigue"),
            new("Yeun",   "하예은", 32, "Str"),
            new("Daeun",  "서다은", 35, "Int"),
            new("Bom",    "이봄",   39, "Soc"),
            new("Heewon", "도희원", 43, "Per"),
        };

        /// <summary>히로인 ID → 설정 빠른 탐색</summary>
        public static readonly Dictionary<string, HeroineConfig> HeroineById;

        static GameConstants()
        {
            HeroineById = new Dictionary<string, HeroineConfig>(Heroines.Length);
            HeroineIds = new string[Heroines.Length];
            HeroineNames = new string[Heroines.Length];
            EndingThresholds = new int[Heroines.Length];
            HeroinePreferredStat = new string[Heroines.Length];

            for (int i = 0; i < Heroines.Length; i++)
            {
                ref readonly var h = ref Heroines[i];
                HeroineById[h.Id] = h;
                HeroineIds[i] = h.Id;
                HeroineNames[i] = h.DisplayName;
                EndingThresholds[i] = h.EndingThreshold;
                HeroinePreferredStat[i] = h.PreferredStat;
            }
        }

        // ── 레거시 호환 (기존 코드가 배열로 접근하는 곳 유지) ──

        /// <summary>히로인 ID 목록 (영문)</summary>
        public static readonly string[] HeroineIds;

        /// <summary>히로인 표시 이름 (한글)</summary>
        public static readonly string[] HeroineNames;

        /// <summary>히로인별 엔딩 임계치</summary>
        public static readonly int[] EndingThresholds;

        /// <summary>히로인별 선호 스탯 ID</summary>
        public static readonly string[] HeroinePreferredStat;

        // ── 스탯 ──

        /// <summary>플레이어 스탯 ID 목록</summary>
        public static readonly string[] StatIds =
        {
            "Str", "Int", "Soc", "Per", "Fatigue"
        };

        /// <summary>플레이어 스탯 표시 이름 (한글)</summary>
        public static readonly string[] StatNames =
        {
            "체력", "지성", "사교성", "끈기", "피로"
        };

        /// <summary>전투 스탯 ID (피로 제외, 스탯 보정 계산용)</summary>
        public static readonly string[] CombatStatIds = { "Str", "Int", "Soc", "Per" };

        // ── 캐릭터 / 표정 ──

        /// <summary>캐릭터 슬롯 위치</summary>
        public static readonly string[] SlotPositions = { "L", "C", "R" };

        /// <summary>기본 표정 목록</summary>
        public static readonly string[] DefaultEmotes =
        {
            "Default", "Happy", "Sad", "Angry", "Blush", "Surprise", "Think", "Shock"
        };

        // ── 수량 상수 ──

        /// <summary>히로인 수</summary>
        public const int HeroineCount = 5;

        /// <summary>스탯 수</summary>
        public const int StatCount = 5;

        /// <summary>스탯 최대값</summary>
        public const int MaxStat = 100;

        // ── UI / 해상도 ──

        /// <summary>지원 해상도 목록 (오름차순)</summary>
        public static readonly (int w, int h)[] Resolutions =
        {
            (800, 450), (960, 540), (1280, 720), (1440, 810), (1920, 1080)
        };

        /// <summary>기본 해상도 인덱스 (1920×1080)</summary>
        public const int DefaultResolutionIndex = 4;

        // ── 게임플레이 ──

        /// <summary>하루 자유행동 횟수 (낮/밤)</summary>
        public const int ActionsPerDay = 2;

        /// <summary>최대 일차 (이 일수가 지나면 엔딩 진입)</summary>
        public const int MaxDay = 30;

        /// <summary>엔딩 진입에 필요한 최소 호감도 (레거시)</summary>
        public const int EndingLoveThreshold = 30;

        // ── 세이브 / 로드 ──

        /// <summary>세이브 슬롯 수 (오토세이브 포함)</summary>
        public const int SaveSlotCount = 30;

        /// <summary>페이지당 슬롯 수</summary>
        public const int SlotsPerPage = 6;

        // ── 투자 ──

        /// <summary>투자 최소 금액</summary>
        public const int MinInvestMoney = 30000;
    }
}
