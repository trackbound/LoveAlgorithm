using System.Collections.Generic;

namespace LoveAlgo.Core
{
    /// <summary>
    /// 데이룹 이벤트 발생 시점
    /// </summary>
    public enum DayTiming
    {
        Morning,    // 하루 시작 시 (스케줄 선택 전)
        Evening     // 하루 종료 시 (RemainingActions == 0 이후)
    }

    /// <summary>
    /// 데이 이벤트 정의
    /// </summary>
    public class DayEvent
    {
        /// <summary>CSV 스크립트 이름 (Resources/Story/ 하위)</summary>
        public string ScriptName { get; }

        /// <summary>
        /// 조건 문자열 (GameState.EvaluateCondition 형식)
        /// 빈 문자열이면 무조건 발동
        /// 예: "Love:Roa>=20", "Flag:Met_Yeun", "Stat:Int>=30"
        /// </summary>
        public string Condition { get; }

        /// <summary>우선순위 (높을수록 먼저 평가, 같으면 먼저 등록된 것)</summary>
        public int Priority { get; }

        public DayEvent(string scriptName, string condition = "", int priority = 0)
        {
            ScriptName = scriptName;
            Condition = condition ?? "";
            Priority = priority;
        }
    }

    /// <summary>
    /// 데이룹 이벤트 테이블
    /// (일차, 시점) → 이벤트 목록 매핑
    /// 
    /// 사용법:
    ///   DayEventTable.GetEvent(day, timing) 호출 시
    ///   해당 일차+시점에 등록된 이벤트 중 조건 충족하는 첫 번째 반환
    /// </summary>
    public static class DayEventTable
    {
        /// <summary>
        /// 이벤트 등록부
        /// Key: (day, timing), Value: 우선순위 내림차순 정렬된 이벤트 목록
        /// </summary>
        static readonly Dictionary<(int day, DayTiming timing), List<DayEvent>> events = new();

        /// <summary>
        /// 특정 일차에 종속되지 않는 반복/조건부 이벤트
        /// 매일 체크되며, 조건 충족 + 미실행 시 발동
        /// </summary>
        static readonly Dictionary<DayTiming, List<DayEvent>> globalEvents = new();

        /// <summary>
        /// 이미 발동한 이벤트 스크립트 (중복 방지)
        /// </summary>
        static readonly HashSet<string> firedEvents = new();

        static DayEventTable()
        {
            BuildTable();
        }

        /// <summary>
        /// 이벤트 테이블 구성
        /// 새 이벤트 추가 시 여기에 등록
        /// </summary>
        static void BuildTable()
        {
            // ── Day 1 ── 개강
            Register(1, DayTiming.Morning, new DayEvent("Day1_Morning"));

            // ── Day 2 ── 서다은, 하예은 첫 조우
            Register(2, DayTiming.Morning, new DayEvent("Day2_Morning"));
            Register(2, DayTiming.Evening, new DayEvent("Day2_Evening"));

            // ── Day 3 ── 이봄 첫 조우, 도희원 첫 조우
            Register(3, DayTiming.Morning, new DayEvent("Day3_Morning"));
            Register(3, DayTiming.Evening, new DayEvent("Day3_Evening"));

            // ── Day 4 ── 루틴 정착
            Register(4, DayTiming.Morning, new DayEvent("Day4_Morning"));

            // ── Day 5 ── 내일 이벤트 예고
            Register(5, DayTiming.Evening, new DayEvent("Day5_Evening"));

            // ── Day 6: Event1 (GameTimeline에서 처리) ──

            // ── Day 7 ── 1차 이벤트 후 여운
            Register(7, DayTiming.Morning, new DayEvent("Day7_Morning"));

            // ── Day 9 ── 축제 전날
            Register(9, DayTiming.Evening, new DayEvent("Day9_Evening"));

            // ── Day 10~12: Festival (GameTimeline에서 처리) ──

            // ── Day 13 ── 축제 후 일상 복귀
            Register(13, DayTiming.Morning, new DayEvent("Day13_Morning"));

            // ── Day 15 ── 서다은과 도서관
            Register(15, DayTiming.Evening, new DayEvent("Day15_Evening"));

            // ── Day 16: Event2 (GameTimeline에서 처리) ──

            // ── Day 17 ── 2차 이벤트 후 감상
            Register(17, DayTiming.Morning, new DayEvent("Day17_Morning"));

            // ── Day 19 ── MT 전날
            Register(19, DayTiming.Evening, new DayEvent("Day19_Evening"));

            // ── Day 20~22: MT (GameTimeline에서 처리) ──

            // ── Day 23 ── MT 후 일상 복귀
            Register(23, DayTiming.Morning, new DayEvent("Day23_Morning"));

            // ── Day 25 ── 3차 이벤트 전날
            Register(25, DayTiming.Evening, new DayEvent("Day25_Evening"));

            // ── Day 26: Event3 (GameTimeline에서 처리) ──

            // ── Day 27 ── 마지막 자유기간
            Register(27, DayTiming.Morning, new DayEvent("Day27_Morning"));

            // ── Day 29 ── 고백 전날 밤
            Register(29, DayTiming.Evening, new DayEvent("Day29_Evening"));

            // ── Day 30: Confession → Ending (GameTimeline에서 처리) ──

            // ── 조건부 글로벌 이벤트 (매일 체크) ──
            // 예: 로아 호감도 30 이상이면 특별 이벤트
            // RegisterGlobal(DayTiming.Evening, new DayEvent("Roa_Love30", "Love:Roa>=30"));
        }

        /// <summary>
        /// 특정 일차+시점에 이벤트 등록
        /// </summary>
        static void Register(int day, DayTiming timing, DayEvent evt)
        {
            var key = (day, timing);
            if (!events.ContainsKey(key))
                events[key] = new List<DayEvent>();
            events[key].Add(evt);
        }

        /// <summary>
        /// 매일 체크되는 글로벌 이벤트 등록
        /// </summary>
        static void RegisterGlobal(DayTiming timing, DayEvent evt)
        {
            if (!globalEvents.ContainsKey(timing))
                globalEvents[timing] = new List<DayEvent>();
            globalEvents[timing].Add(evt);
        }

        /// <summary>
        /// 해당 일차+시점에서 발동 가능한 이벤트 반환
        /// 일차 고정 이벤트 → 글로벌 이벤트 순으로 검색
        /// 조건 충족 + 미발동인 첫 이벤트 반환
        /// </summary>
        public static DayEvent GetEvent(int day, DayTiming timing)
        {
            var gs = Story.GameState.Instance;

            // 1) 일차 고정 이벤트
            if (events.TryGetValue((day, timing), out var dayList))
            {
                foreach (var evt in dayList)
                {
                    if (firedEvents.Contains(evt.ScriptName)) continue;
                    if (string.IsNullOrEmpty(evt.Condition) || (gs != null && gs.EvaluateCondition(evt.Condition)))
                        return evt;
                }
            }

            // 2) 글로벌 조건부 이벤트
            if (globalEvents.TryGetValue(timing, out var globalList))
            {
                foreach (var evt in globalList)
                {
                    if (firedEvents.Contains(evt.ScriptName)) continue;
                    if (string.IsNullOrEmpty(evt.Condition) || (gs != null && gs.EvaluateCondition(evt.Condition)))
                        return evt;
                }
            }

            return null;
        }

        /// <summary>
        /// 이벤트 발동 기록 (중복 방지)
        /// </summary>
        public static void MarkFired(string scriptName)
        {
            firedEvents.Add(scriptName);
        }

        /// <summary>
        /// 발동 기록 초기화 (새 게임 시)
        /// </summary>
        public static void ResetFired()
        {
            firedEvents.Clear();
        }

        /// <summary>
        /// 발동 기록 반환 (세이브용)
        /// </summary>
        public static HashSet<string> GetFiredEvents() => new(firedEvents);

        /// <summary>
        /// 발동 기록 복원 (로드용)
        /// </summary>
        public static void RestoreFiredEvents(IEnumerable<string> fired)
        {
            firedEvents.Clear();
            if (fired != null)
            {
                foreach (var s in fired)
                    firedEvents.Add(s);
            }
        }
    }
}
