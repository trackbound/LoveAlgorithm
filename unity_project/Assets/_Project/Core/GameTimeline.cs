using System.Collections.Generic;
using UnityEngine;

namespace LoveAlgo.Core
{
    // StoryArc / DayType enum은 LoveAlgo.Core asmdef의 GameFlowTypes.cs로 이전됨 (M2 slice2).
    // 구 Assembly-CSharp 코드는 asmdef auto-reference로 동일 네임스페이스에서 계속 참조한다.

    /// <summary>
    /// 하루 일정 정보
    /// </summary>
    public class DayInfo
    {
        public int Day { get; }
        public DayType Type { get; }
        public StoryArc Arc { get; }

        /// <summary>이벤트명 (이벤트 날에만, CSV 스크립트 이름 등)</summary>
        public string EventTag { get; }

        /// <summary>이벤트 기본 포인트 (히로인 선택 시 부여)</summary>
        public int EventPoints { get; }

        public DayInfo(int day, DayType type, StoryArc arc, string eventTag = null, int eventPoints = 0)
        {
            Day = day;
            Type = type;
            Arc = arc;
            EventTag = eventTag;
            EventPoints = eventPoints;
        }
    }

    /// <summary>
    /// 게임 타임라인 정적 테이블
    /// SO(GameBalanceSO)에서 데이터를 로드하며, SO가 없으면 하드코딩 폴백 사용
    /// </summary>
    public static class GameTimeline
    {
        static readonly Dictionary<int, DayInfo> timeline = new();
        static bool _initialized;

        /// <summary>MonoBehaviour 생성자 밖에서 안전하게 초기화</summary>
        static void EnsureInit()
        {
            if (_initialized) return;
            _initialized = true;

            // SO 로드 시도
            var so = Resources.Load<GameBalanceSO>("Data/GameBalance");
            if (so != null && so.Timeline.Count > 0)
            {
                foreach (var e in so.Timeline)
                    timeline[e.day] = new DayInfo(e.day, e.type, e.arc, e.eventTag, e.eventPoints);
            }
            else
            {
                BuildTimeline();
            }
        }

        /// <summary>
        /// 타임라인 구성
        /// Day 1~30 각 날짜별 스토리 아크 및 이벤트 정의
        /// </summary>
        static void BuildTimeline()
        {
            // ── 개강 (Day 1~2) ──
            Add(1, DayType.Free, StoryArc.Opening);
            Add(2, DayType.Free, StoryArc.Opening);

            // ── 자유행동 구간 1 (Day 3~5) ──
            Add(3, DayType.Free, StoryArc.FreeTime1);
            Add(4, DayType.Free, StoryArc.FreeTime1);
            Add(5, DayType.Free, StoryArc.FreeTime1);

            // ── 1차 개인 이벤트 (Day 6) ── 히로인 선택 +3점
            Add(6, DayType.PersonalEvent, StoryArc.Event1, "Event1", 3);

            // ── 자유행동 구간 2 (Day 7~9) ──
            Add(7, DayType.Free, StoryArc.FreeTime2);
            Add(8, DayType.Free, StoryArc.FreeTime2);
            Add(9, DayType.Free, StoryArc.FreeTime2);

            // ── 축제 (Day 10~12, 3일) ── 집에 데려다줌 +4점
            Add(10, DayType.GroupEvent, StoryArc.Festival, "Festival_Day1");
            Add(11, DayType.GroupEvent, StoryArc.Festival, "Festival_Day2");
            Add(12, DayType.GroupEvent, StoryArc.Festival, "Festival_Day3", 4);

            // ── 자유행동 구간 3 (Day 13~15) ──
            Add(13, DayType.Free, StoryArc.FreeTime3);
            Add(14, DayType.Free, StoryArc.FreeTime3);
            Add(15, DayType.Free, StoryArc.FreeTime3);

            // ── 2차 개인 이벤트 (Day 16) ── 히로인 선택 +6점
            Add(16, DayType.PersonalEvent, StoryArc.Event2, "Event2", 6);

            // ── 자유행동 구간 4 (Day 17~19) ──
            Add(17, DayType.Free, StoryArc.FreeTime4);
            Add(18, DayType.Free, StoryArc.FreeTime4);
            Add(19, DayType.Free, StoryArc.FreeTime4);

            // ── MT (Day 20~22) ── 밤바다 산책 상대 +5점
            Add(20, DayType.GroupEvent, StoryArc.MT, "MT_Day1");
            Add(21, DayType.GroupEvent, StoryArc.MT, "MT_Day2", 5);
            Add(22, DayType.GroupEvent, StoryArc.MT, "MT_Day3");

            // ── 자유행동 구간 5 (Day 23~25) ──
            Add(23, DayType.Free, StoryArc.FreeTime5);
            Add(24, DayType.Free, StoryArc.FreeTime5);
            Add(25, DayType.Free, StoryArc.FreeTime5);

            // ── 3차 개인 이벤트 (Day 26) ── 히로인 선택 +9점
            Add(26, DayType.PersonalEvent, StoryArc.Event3, "Event3", 9);

            // ── 자유행동 구간 6 (Day 27~29) ──
            Add(27, DayType.Free, StoryArc.FreeTime6);
            Add(28, DayType.Free, StoryArc.FreeTime6);
            Add(29, DayType.Free, StoryArc.FreeTime6);

            // ── 고백 이벤트 (Day 30) ──
            Add(30, DayType.Confession, StoryArc.Confession, "Confession");
        }

        static void Add(int day, DayType type, StoryArc arc, string tag = null, int points = 0)
        {
            timeline[day] = new DayInfo(day, type, arc, tag, points);
        }

        /// <summary>해당 일차의 일정 정보 반환</summary>
        public static DayInfo GetDayInfo(int day)
        {
            EnsureInit();
            return timeline.TryGetValue(day, out var info) ? info : null;
        }

        /// <summary>해당 일차가 자유행동 가능한지</summary>
        public static bool IsFreeDay(int day)
        {
            var info = GetDayInfo(day);
            return info == null || info.Type == DayType.Free;
        }

        /// <summary>해당 일차가 이벤트 날인지 (개인 또는 단체)</summary>
        public static bool IsEventDay(int day)
        {
            var info = GetDayInfo(day);
            if (info == null) return false;
            return info.Type == DayType.PersonalEvent || info.Type == DayType.GroupEvent;
        }

        /// <summary>해당 일차의 스토리 아크</summary>
        public static StoryArc GetArc(int day)
        {
            var info = GetDayInfo(day);
            return info?.Arc ?? StoryArc.FreeTime1;
        }

        /// <summary>
        /// 이벤트 기본 포인트 값 조회
        /// 기획서: 1차+3, 축제+4, 2차+6, MT+5, 3차+9 = 총 27점
        /// </summary>
        public static int GetEventPoints(int day)
        {
            var info = GetDayInfo(day);
            return info?.EventPoints ?? 0;
        }
    }
}
