using System.Collections.Generic;
using UnityEngine;

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

        static bool _initialized;

        /// <summary>히로인 설정 목록 (인덱스 = 히로인 순서)</summary>
        public static HeroineConfig[] Heroines { get { EnsureInit(); return _heroines; } }
        static HeroineConfig[] _heroines;

        /// <summary>히로인 ID → 설정 빠른 탐색</summary>
        public static Dictionary<string, HeroineConfig> HeroineById { get { EnsureInit(); return _heroineById; } }
        static Dictionary<string, HeroineConfig> _heroineById;

        /// <summary>MonoBehaviour 생성자 밖에서 안전하게 초기화</summary>
        static void EnsureInit()
        {
            if (_initialized) return;
            _initialized = true;

            // SO 로드 시도
            var so = Resources.Load<GameBalanceSO>("Data/GameBalance");
            if (so != null && so.Heroines.Count > 0)
            {
                _heroines = new HeroineConfig[so.Heroines.Count];
                for (int i = 0; i < so.Heroines.Count; i++)
                {
                    var h = so.Heroines[i];
                    _heroines[i] = new HeroineConfig(h.id, h.displayName, h.endingThreshold, h.preferredStat);
                }
                _actionsPerDay = so.ActionsPerDay;
                _maxDay = so.MaxDay;
                _endingLoveThreshold = so.EndingLoveThreshold;
                _minInvestMoney = so.MinInvestMoney;
            }
            else
            {
                // 하드코딩 폴백
                _heroines = new HeroineConfig[]
                {
                    new("Roa",    "로아",   46, "Fatigue"),
                    new("HaYeEun",   "하예은", 32, "Str"),
                    new("SeoDaEun",  "서다은", 35, "Int"),
                    new("LeeBom",    "이봄",   39, "Soc"),
                    new("DoHeewon", "도희원", 43, "Per"),
                };
                _actionsPerDay = 2;
                _maxDay = 30;
                _endingLoveThreshold = 30;
                _minInvestMoney = 30000;
            }

            _heroineById = new Dictionary<string, HeroineConfig>(_heroines.Length);
            _heroineIds = new string[_heroines.Length];
            _heroineNames = new string[_heroines.Length];
            _endingThresholds = new int[_heroines.Length];
            _heroinePreferredStat = new string[_heroines.Length];

            for (int i = 0; i < _heroines.Length; i++)
            {
                ref readonly var h = ref _heroines[i];
                _heroineById[h.Id] = h;
                _heroineIds[i] = h.Id;
                _heroineNames[i] = h.DisplayName;
                _endingThresholds[i] = h.EndingThreshold;
                _heroinePreferredStat[i] = h.PreferredStat;
            }
        }

        // ── 레거시 호환 (기존 코드가 배열로 접근하는 곳 유지) ──

        /// <summary>히로인 ID 목록 (영문)</summary>
        public static string[] HeroineIds { get { EnsureInit(); return _heroineIds; } }
        static string[] _heroineIds;

        /// <summary>히로인 표시 이름 (한글)</summary>
        public static string[] HeroineNames { get { EnsureInit(); return _heroineNames; } }
        static string[] _heroineNames;

        /// <summary>히로인별 엔딩 임계치</summary>
        public static int[] EndingThresholds { get { EnsureInit(); return _endingThresholds; } }
        static int[] _endingThresholds;

        /// <summary>히로인별 선호 스탯 ID</summary>
        public static string[] HeroinePreferredStat { get { EnsureInit(); return _heroinePreferredStat; } }
        static string[] _heroinePreferredStat;

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

        // ── 설정 기본값 ──
        public const float DefaultMasterVolume = 0.8f;
        public const float DefaultBGMVolume    = 0.5f;
        public const float DefaultSFXVolume    = 0.5f;
        public const float DefaultVoiceVolume  = 0.5f;
        public const float DefaultTextSpeed    = 0.7f;   // 0=느림, 1=빠름 (슬라이더 정규화 값)
        public const float DefaultAutoSpeed    = 0.5f;   // 0=느림, 1=빠름 (슬라이더 정규화 값)

        // ── 게임플레이 (SO에서 로드, 폴백 있음) ──

        static int _actionsPerDay;
        static int _maxDay;
        static int _endingLoveThreshold;
        static int _minInvestMoney;

        /// <summary>하루 자유행동 횟수 (낮/밤)</summary>
        public static int ActionsPerDay { get { EnsureInit(); return _actionsPerDay; } }

        /// <summary>최대 일차 (이 일수가 지나면 엔딩 진입)</summary>
        public static int MaxDay { get { EnsureInit(); return _maxDay; } }

        /// <summary>엔딩 진입에 필요한 최소 호감도 (레거시)</summary>
        public static int EndingLoveThreshold { get { EnsureInit(); return _endingLoveThreshold; } }

        // ── 세이브 / 로드 ──

        /// <summary>세이브 슬롯 수 (오토세이브 포함)</summary>
        public const int SaveSlotCount = 30;

        /// <summary>페이지당 슬롯 수</summary>
        public const int SlotsPerPage = 6;

        // ── 투자 ──

        /// <summary>투자 최소 금액</summary>
        public static int MinInvestMoney { get { EnsureInit(); return _minInvestMoney; } }
    }
}
